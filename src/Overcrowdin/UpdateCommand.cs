using System;
using System.Collections.Generic;
using System.IO.Abstractions;
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
				var updateFileParameters = new UpdateFileParameters { Files = CommandUtilities.GetFileList(config, opts, fileSystem) };
				Console.WriteLine("Updating {0} files...", updateFileParameters.Files.Count);
				var crowdinResult = await crowdin.UpdateFile(projectId,
					projectCredentials, updateFileParameters);
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