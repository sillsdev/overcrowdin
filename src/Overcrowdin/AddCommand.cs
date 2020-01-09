using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public sealed class AddCommand
	{
		[Verb("addfiles", HelpText = "Add files to Crowdin.")]
		public class Options : GlobalOptions, IFileOptions
		{
			[Option('f', "file", Required = false, HelpText = "Path(s) to a file to upload")]
			public IEnumerable<string> Files { get; set; }
		}

		public static async Task<int> AddFilesToCrowdin(IConfiguration config, Options opts, AutoResetEvent gate, IFileSystem fs)
		{
			var crowdin = CrowdinCommand.GetClient();
			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials { ProjectKey = projectKey };
			var addFileParams = new AddFileParameters();
			var foldersToCreate = new SortedSet<string>();
			CommandUtilities.GetFileList(config, opts, fs, addFileParams, foldersToCreate);

			// create folders
			if (0 != await CreateFolderCommand.CreateFoldersInCrowdin(config, opts, foldersToCreate, fs))
			{
				gate.Set();
				return 1;
			}

			// Group files into batches
			var fileBatches = CommandUtilities.BatchFiles(addFileParams);

			// Add files to Crowdin
			var fileCount = addFileParams.Files.Count;
			Console.WriteLine($"Adding {fileCount} files...");
			HttpResponseMessage result;
			var i = 0;
			do
			{
				addFileParams.Files = fileBatches[i].Files;
				addFileParams.ExportPatterns = fileBatches[i].ExportPatterns;
				result = await crowdin.AddFile(projectId, projectCredentials, addFileParams);
			} while (++i < fileBatches.Length && result.IsSuccessStatusCode);

			// Give results
			if (result.IsSuccessStatusCode)
			{
				Console.WriteLine("Finished Adding files.");
				if (opts.Verbose)
				{
					var info = await result.Content.ReadAsStringAsync();
					Console.WriteLine(info);
				}
			}
			else
			{
				Console.WriteLine("Failure adding files.");
				// Crowdin adds all files before the problem file. There is no rollback.
				// Alert the user to the potential state of the project in Crowdin.
				if (i > 1)
				{
					var successFileCount = (i - 1) * CommandUtilities.BatchSize;
					Console.WriteLine($"Successfully added at least {successFileCount} files.");
				}
				else if (fileCount > 1)
				{
					Console.WriteLine("Some files may have been added.");
				}
				if (opts.Verbose)
				{
					var error = await result.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}
			gate.Set();
			return result.IsSuccessStatusCode ? 0 : 1;
		}
	}
}