using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Crowdin.Api;
using Crowdin.Api.Protocol;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Overcrowdin
{
	/// <summary>
	///    This class will handle the command line arg for generating a configuration file for a Crowdin project
	/// </summary>
	public class GenerateCommand
	{
		public static async Task<int> GenerateConfigFromCrowdin(IConfiguration config, Options opts, IFileSystem fs)
		{
			var success = 1;
			var key = Environment.GetEnvironmentVariable(opts.Key);
			if (!string.IsNullOrEmpty(key))
			{
				var crowdin = CrowdinCommand.GetClient();
				// var projectCredentials = new ProjectCredentials {ProjectKey = key};
				var accountCredentials = new AccountCredentials { AccountKey = key, LoginName = opts.User };
				try
				{
					// var project = await crowdin.GetProjectInfo(opts.Identifier, projectCredentials);
					var project = await crowdin.GetProjectInfo(opts.Identifier, accountCredentials);
					if (project == null)
						return success;

					dynamic jsonObject = new JObject();
					jsonObject.project_identifier = opts.Identifier;
					jsonObject.api_key_env = opts.Key;
					jsonObject.base_path = opts.BasePath;
					var jsonFiles = new JArray();
					foreach (var file in project.Files)
					{
						AddFileOrDirectory(opts.BasePath, file, jsonFiles);
					}
					jsonObject.files = jsonFiles;
					fs.File.WriteAllText(opts.OutputFile, jsonObject.ToString());
					success = 0;
				}
				catch (CrowdinException)
				{
					Console.WriteLine("Failed to retrieve project. Check your project id and project key.");
				}
			}
			else
			{
				Console.WriteLine("{0} did not contain the API Key for your Crowdin project.", opts.Key);
			}

			return success;
		}

		private static void AddFileOrDirectory(string path, ProjectNode node, JArray jsonFiles)
		{
			switch (node)
			{
				case ProjectFile projectFile:
				{
					dynamic file = new JObject();
					// Not using Path.Combine because I want the same behavior on Linux and Windows
					file.source = path + "/" + projectFile.Name;
					file.translation = "/yourlocalefolder/%use_some_crowdin_lang_code%/%original_file_name%";
					jsonFiles.Add(file);
					break;
				}
				case ProjectFolder projectFolder:
				{
					foreach (var file in projectFolder.Files) AddFileOrDirectory(path + "/" + projectFolder.Name, file, jsonFiles);
					break;
				}
			}
		}

		[Verb("generate", HelpText = "Generate a config file from Crowdin.")]
		public class Options : GlobalOptions
		{
			[Option('t', Required = false, Default = ConfigType.json,
				HelpText = "The configuration type. (Currently only JSON is supported)")]
			public ConfigType Type { get; set; }

			[Option('k', Required = false, Default = "CROWDIN_API_KEY",
				HelpText = "The environment variable holding the API key for your Crowdin project")]
			public string Key { get; set; }

			[Option('u', Required = true, HelpText = "The user for Crowdin project API access")]
			public string User { get; set; }

			[Option('i', Required = true, HelpText = "The Project Identifier for your Crowdin project")]
			public string Identifier { get; set; }

			[Option('b', Required = false, Default = ".", HelpText = "The base path to use for file references")]
			public string BasePath { get; set; }

			[Option('f', Required = false, Default = "crowdin.json",
				HelpText = "The filename to save the configuration to.")]
			public string OutputFile { get; set; }
		}
	}

	public enum ConfigType
	{
		json
		// Future support: yaml
	}
}