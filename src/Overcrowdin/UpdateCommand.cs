using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public class UpdateCommand 
	{
		[Verb("updatefiles", HelpText = "Update files in Crowdin. Will use crowdin.json or files passed in as arguments.")]
		public class Options : GlobalOptions, IFileOptions
		{
			[Option('f', "file", Required = false, HelpText = "Path to a file to upload")]
			public IEnumerable<string> Files { get; set; }
			// TODO: Add option for update approval -- default to Update_as_unapproved
		}

		public static async Task<int> UpdateFilesInCrowdin(IConfiguration config, Options opts, AutoResetEvent gate, IFileSystem fileSystem)
		{
			var crowdin = CrowdinCommand.GetClient();

			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			int result = 1;
			if (!string.IsNullOrEmpty(projectKey))
			{
				var projectCredentials = new ProjectCredentials {ProjectKey = projectKey};
				var updateFileParametersList = new List<UpdateFileParameters>();
				CommandUtilities.GetFileList(config, opts, fileSystem, updateFileParametersList, new SortedSet<string>());

				// TODO (Hasso) 2020.01: check for no files to update

				Console.WriteLine($"Updating {updateFileParametersList.Sum(ufp => ufp.Files.Count)} files...");
				HttpResponseMessage crowdinResult;
				var i = 0;
				do
				{
					var updateFileParameters = updateFileParametersList[i];
					crowdinResult = await crowdin.UpdateFile(projectId, projectCredentials, updateFileParameters);
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
						string error = await crowdinResult.Content.ReadAsStringAsync();
						Console.WriteLine(error);
					}
				}
				result = crowdinResult.IsSuccessStatusCode ? 0 : 1;
			}
			else
			{
				Console.WriteLine("{0} did not contain the API Key for your Crowdin project.", config["api_key_env"]);
			}

			gate.Set();
			return result;
		}
	}
}