using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Overcrowdin
{
	class DownloadCommand
	{
		[Verb("download", HelpText = "Download the latest translations from Crowdin")]
		public class Options : GlobalOptions
		{
			[Option('l', Required = false, Default ="all", HelpText = "The language to download the translations for or 'all' to download for every language.")]
			public string Language { get; set; }

			[Option('e', Required = false, Default = false, HelpText = "Export all translations before downloading. This generates the .zip files on the Crowdin server.")]
			public bool ExportFirst { get; set; }

			[Option('f', Required = true, Default = false, HelpText = "Path and filename relative to the configured basepath for the zip file.")]
			public string Filename { get; set; }
		}


		public static async Task<int> DownloadFromCrowdin(IConfiguration config, Options opts, AutoResetEvent gate)
		{
			var crowdin = CrowdinCommand.GetClient();

			var success = 1;
			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials { ProjectKey = projectKey };

			var outputFile = Path.Combine(config["base_path"], opts.Filename);
			try
			{
				if (opts.ExportFirst)
				{
					await crowdin.ExportTranslation(projectId, projectCredentials, new ExportTranslationParameters());
				}
				var downloadResponse = await crowdin.DownloadTranslation(projectId,
					projectCredentials, new DownloadTranslationParameters { Package = opts.Language });
				if (downloadResponse.IsSuccessStatusCode)
				{
					using (var downloadedFile = new FileStream(outputFile, FileMode.Create))
					{
						downloadedFile.Write(await downloadResponse.Content.ReadAsByteArrayAsync());
					}
				}
				success = 0;
			}
			catch (CrowdinException e)
			{
				Console.WriteLine("Failed to download translations. Check your project id and project key.");
				if (opts.Verbose)
				{
					Console.WriteLine(e.ErrorCode);
					Console.WriteLine(e.StackTrace);
				}
			}
			Console.WriteLine("Translations file downloaded to {0}", outputFile);
			gate.Set();
			return success;
		}
	}
}
