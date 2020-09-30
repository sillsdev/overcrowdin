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

		public static async Task<int> UpdateFilesInCrowdin(IConfiguration config, Options opts, IFileSystem fileSystem)
		{
			var crowdin = CrowdinCommand.GetClient();

			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			if (credentials == null)
			{
				return 1;
			}

			var updateFileParametersList = new List<UpdateFileParameters>();
			CommandUtilities.GetFileList(config, opts, fileSystem, updateFileParametersList, new SortedSet<string>());

			if (!updateFileParametersList.Any())
			{
				Console.WriteLine("No files to add.");
				return 0;
			}


			Console.WriteLine($"Updating {updateFileParametersList.Sum(ufp => ufp.Files.Count)} files...");
			HttpResponseMessage crowdinResult;
			var i = 0;
			do
			{
				var updateFileParameters = updateFileParametersList[i];
				crowdinResult = await crowdin.UpdateFile(projectId, credentials, updateFileParameters);
			} while (++i < updateFileParametersList.Count && crowdinResult.IsSuccessStatusCode);

			// Give results
			if (crowdinResult.IsSuccessStatusCode)
			{
				Console.WriteLine("Finished Updating files.");
				if (opts.Verbose)
				{
					var info = await crowdinResult.Content.ReadAsStringAsync();
					Console.WriteLine(info);
				}
			}
			else
			{
				Console.WriteLine("Failure updating files.");
				// A problem file does not cause Crowdin to roll back a batch. Alert the user if some files may have been updated.
				if (i > 1 || updateFileParametersList[0].Files.Count > 1)
				{
					Console.WriteLine("Some files may have been updated.");
				}

				if (opts.Verbose)
				{
					var error = await crowdinResult.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}

			return crowdinResult.IsSuccessStatusCode ? 0 : 1;
		}
	}
}