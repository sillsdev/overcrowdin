using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommandLine;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public sealed class AddCommand
	{
		[Verb("addfiles", HelpText = "Add files to Crowdin.")]
		public class Options : GlobalOptions, IFileOptions
		{
			[Option('b', "branch", Required = false, HelpText = "Name of the version branch")]
			public string Branch { get; set; }

			[Option('f', "file", Required = false, HelpText = "Path(s) to a file to upload")]
			public IEnumerable<string> Files { get; set; }
		}

		public static async Task<int> AddFilesToCrowdin(IConfiguration config, Options opts, IFileSystem fs)
		{
			var crowdin = CrowdinCommand.GetClient();
			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			if (credentials == null)
			{
				return 1;
			}

			var addFileParamsList = new List<AddFileParameters>();
			var foldersToCreate = new SortedSet<string>();
			CommandUtilities.GetFileList(config, opts, fs, addFileParamsList, foldersToCreate);

			if (!addFileParamsList.Any())
			{
				Console.WriteLine("No files to add.");
				return 0;
			}

			// create folders
			if (0 != await CreateFolderCommand.CreateFoldersInCrowdin(config, opts, foldersToCreate))
			{
				return 1;
			}

			// Add files to Crowdin
			Console.WriteLine($"Adding {addFileParamsList.Sum(afp => afp.Files.Count)} files...");
			HttpResponseMessage result;
			var i = 0;
			do
			{
				var addFileParams = addFileParamsList[i];
				result = await crowdin.AddFile(projectId, credentials, addFileParams);
			} while (++i < addFileParamsList.Count && result.IsSuccessStatusCode);

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
				if (i > 1 || addFileParamsList[0].Files.Count > 1)
				{
					Console.WriteLine("Some files may have been added.");
				}

				if (opts.Verbose)
				{
					var error = await result.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}
			return result.IsSuccessStatusCode ? 0 : 1;
		}
	}
}