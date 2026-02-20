using System;
using System.Collections.Generic;
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
			[Option('b', "branch", Required = false, HelpText = "Name of the Crowdin branch (overrides any branch in crowdin.json)")]
			public string Branch { get; set; }

			[Option('f', "file", Required = false, HelpText = "Path to a file to upload")]
			public IEnumerable<string> Files { get; set; }
			// TODO: Add option for update approval -- default to Update_as_unapproved
		}

		public static async Task<int> UpdateFilesInCrowdin(IConfiguration config, Options opts, IFileSystem fileSystem, ICrowdinClientFactory apiFactory)
		{
			var updateFileParametersList = new List<FileParameters>();
			var nonLocalizableFiles = new List<string>();
			CommandUtilities.GetFileList(config, opts, fileSystem, updateFileParametersList, new SortedSet<string>(), nonLocalizableFiles);

			if (!updateFileParametersList.Any() && !nonLocalizableFiles.Any())
			{
				Console.WriteLine("No files to add.");
				return 0;
			}
			var credentials = CommandUtilities.GetProjectSettingsFromConfiguration(config, opts.Branch, apiFactory);
			if (credentials == null)
			{
				return 1;
			}
			var uploadHelper = await CrowdinUploadHelper.Create(credentials, fileSystem, apiFactory);

			if (updateFileParametersList.Any())
			{
				Console.WriteLine($"Updating {updateFileParametersList.Sum(ufp => ufp.FilesToExportPatterns.Count)} files...");
				var i = 0;
				do
				{
					var updateFileParameters = updateFileParametersList[i];
					foreach (var file in updateFileParameters.FilesToExportPatterns.Keys)
					{
						await uploadHelper.UploadFile(fileSystem.File.ReadAllText(file), file, updateFileParameters);
					}
				} while (++i < updateFileParametersList.Count);
			}

			if (nonLocalizableFiles.Any())
			{
				var deleted = await uploadHelper.DeleteFiles(nonLocalizableFiles);
				if (deleted > 0 && opts.Verbose)
				{
					Console.WriteLine($"Removed {deleted} files with no localizable content.");
				}
			}

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
				Console.WriteLine("An error occurred while updating files. Some files may still have been updated.");
			}

			return uploadHelper.FileErrorCount;
		}
	}
}
