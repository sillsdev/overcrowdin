using System;
using System.Collections.Generic;
using System.IO;
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
		[Verb("updatefiles", HelpText = "Update files in Crowdin. Will use crowdin.json or files passed in as arguments")]
		public class Options : GlobalOptions
		{
			[Option('f', "file", Required = false, HelpText = "Path to a file to upload")]
			public IEnumerable<string> Files { get; set; }
			// TODO: Add option for update approval -- default to Update_as_unapproved
		}

		public static async Task<int> UpdateFilesInCrowdin(IConfiguration config, Options opts, AutoResetEvent gate)
		{
			var crowdin = CrowdinCommand.GetClient();

			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials { ProjectKey = projectKey };
			var updateFileParameters = BuildUpdateFileParameters(config, opts);
			Console.WriteLine("Updating {0} files...", updateFileParameters.Files.Count);
			var result = await crowdin.UpdateFile(projectId,
				projectCredentials, updateFileParameters);
			if (result.IsSuccessStatusCode)
			{
				Console.WriteLine("Finished Updating files.");
				if (opts.Verbose)
				{
					var info = await result.Content.ReadAsStringAsync();
					Console.WriteLine(info);
				}
			}
			else
			{
				Console.WriteLine("Failure updating files.");
				if (opts.Verbose)
				{
					string error = await result.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}
			gate.Set();
			return result.IsSuccessStatusCode ? 0 : 1;
		}


		private static UpdateFileParameters BuildUpdateFileParameters(IConfiguration config, Options opts)
		{
			var files = new Dictionary<string, FileInfo>();
			// handle files specified on the command line
			if (opts.Files.Any())
			{
				foreach (var file in opts.Files)
				{
					files[Path.GetFileName(file)] = new FileInfo(file);
				}
			}
			else
			{
				// handle configuration files section that resembles:
				// files : [
				//  {
				//    "source" : "resources/en/*.json",
				//    "translation" : "resources/%two_letters_code%/%original_file_name"
				//  }
				// ]
				var filesSection = config.GetSection("files").AsEnumerable();
				foreach (var file in filesSection)
				{
					if (file.Key == "source")
					{
						var matchedFiles = Directory.GetFiles(".", file.Value);
						foreach (var sourceFile in matchedFiles)
						{
							files[Path.GetFileName(sourceFile)] = new FileInfo(sourceFile);
						}
					}
				}
			}

			return new UpdateFileParameters { Files = files };
		}
	}
}