using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommandLine;
using Crowdin.Api.SourceFiles;
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

		public static async Task<int> AddFilesToCrowdin(IConfiguration config, Options opts, IFileSystem fs, ICrowdinClientFactory apiFactory)
		{
			var projectId = config["project_identifier"];
			var credentials = await CommandUtilities.GetProjectSettingsFromConfiguration(config, opts.Branch, apiFactory);
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

			var uploadHelper = await CrowdInUploadHelper.Create(credentials, fs, apiFactory);
			// Add files to Crowdin
			Console.WriteLine($"Adding {addFileParamsList.Sum(afp => afp.Files.Count)} files...");
			var i = 0;
			do
			{
				var addFileParams = addFileParamsList[i];
				foreach (var file in addFileParams.Files)
				{
					await uploadHelper.UploadFile(fs.File.ReadAllText(file.Key), Path.GetDirectoryName(file.Key), file.Value.Name);
				}
			} while (++i < addFileParamsList.Count && uploadHelper.FileErrorCount == 0);

			// Give results
			if (uploadHelper.FileErrorCount == 0)
			{
				Console.WriteLine("Finished Adding files.");
				if (opts.Verbose)
				{
					Console.WriteLine($"Uploaded {uploadHelper.FileUploadCount} files.");
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
					Console.WriteLine($"Encountered {uploadHelper.FileErrorCount} errors while uploading files.");
				}
			}
			return uploadHelper.FileErrorCount;
		}
	}
}