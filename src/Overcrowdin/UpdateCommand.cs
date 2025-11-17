using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public class UpdateCommand
	{
		[Verb("updatefiles", HelpText = "Update files in Crowdin. Will use crowdin.json or files passed in as arguments.")]
		public class Options : GlobalOptions, IFileOptions
		{
			[Option('b', "branch", Required = false, HelpText = "Name of the version branch")]
			public string Branch { get; set; }

			[Option('f', "file", Required = false, HelpText = "Path to a file to upload")]
			public IEnumerable<string> Files { get; set; }
			// TODO: Add option for update approval -- default to Update_as_unapproved
		}

		public static async Task<int> UpdateFilesInCrowdin(IConfiguration config, Options opts, IFileSystem fileSystem, ICrowdinClientFactory apiFactory)
		{
			var updateFileParametersList = new List<FileParameters>();
			CommandUtilities.GetFileList(config, opts, fileSystem, updateFileParametersList, new SortedSet<string>());

			if (!updateFileParametersList.Any())
			{
				Console.WriteLine("No files to add.");
				return 0;
			}
			var credentials = await CommandUtilities.GetProjectSettingsFromConfiguration(config, opts.Branch, apiFactory);
			if (credentials == null)
			{
				return 1;
			}
			var uploadHelper = await CrowdInUploadHelper.Create(credentials, fileSystem, apiFactory);

			Console.WriteLine($"Updating {updateFileParametersList.Sum(ufp => ufp.Files.Count)} files...");
			var i = 0;
			do
			{
				var updateFileParameters = updateFileParametersList[i];
				foreach (var file in updateFileParameters.Files)
				{
					await uploadHelper.UploadFile(fileSystem.File.ReadAllText(file.Key), Path.GetDirectoryName(file.Key), file.Value.Name, updateFileParameters);
				}
			} while (++i < updateFileParametersList.Count);

			// Give results
			if (uploadHelper.FileErrorCount == 0)
			{
				Console.WriteLine("Finished Updating files.");
				if (opts.Verbose)
				{
					Console.WriteLine($"Updated {uploadHelper.FileUploadCount} files.");
				}
			}
			else
			{
				Console.WriteLine("A failure occurred while updating the following:\n");
				foreach (var f in updateFileParametersList[i - 1].Files)
				{
					Console.WriteLine("  " + f.Value.FullName);
				}

				// A problem file does not cause Crowdin to roll back a batch. Alert the user if some files may have been updated.
				if (i > 1 || updateFileParametersList[0].Files.Count > 1)
				{
					Console.WriteLine("Some files may have been updated.");
				}
			}

			return uploadHelper.FileErrorCount;
		}
	}
}