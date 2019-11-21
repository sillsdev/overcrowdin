using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
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
		public class Options : GlobalOptions
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
			var addFileParams = BuildAddFileParameters(config, opts, fs);
			Console.WriteLine("Adding {0} files...", addFileParams.Files.Count);
			var result = await crowdin.AddFile(projectId,
				projectCredentials, addFileParams);
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
				if (opts.Verbose)
				{
					var error = await result.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}
			gate.Set();
			return result.IsSuccessStatusCode ? 0 : 1;
		}

		/// <summary>
		/// TODO: If a common IFileParameters is added to the Crowdin API then make a generic method to do both Add and Update
		/// </summary>
		private static AddFileParameters BuildAddFileParameters(IConfiguration config, Options opts, IFileSystem fs)
		{
			var files = new Dictionary<string, FileInfo>();
			// handle files specified on the command line
			if (opts.Files != null && opts.Files.Any())
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
				// ENHANCE: put the translation destination into Crowdin
				var valuesSection = config.GetSection("files");
				foreach (IConfigurationSection section in valuesSection.GetChildren())
				{
					var filePattern = section.GetValue<string>("source");
					var matchedFiles = fs.Directory.GetFiles(fs.Directory.GetCurrentDirectory(), filePattern);
					foreach (var sourceFile in matchedFiles)
					{
						files[Path.GetFileName(sourceFile)] = new FileInfo(sourceFile);
					}
				}
			}

			return new AddFileParameters { Files = files };
		}
	}
}