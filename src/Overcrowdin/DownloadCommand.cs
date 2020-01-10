using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public class DownloadCommand
	{
		[Verb("download", HelpText = "Download the latest translations from Crowdin")]
		public class Options : GlobalOptions
		{
			[Option('l', Required = false, Default ="all", HelpText = "The language to download the translations for or 'all' to download for every language.")]
			public string Language { get; set; }

			[Option('e', Required = false, Default = false, HelpText = "Export all translations before downloading. This generates the .zip files on the Crowdin server.")]
			public bool ExportFirst { get; set; }

			[Option('f', Required = true, HelpText = "Path and filename relative to the configured basepath for the zip file.")]
			public string Filename { get; set; }
		}


		public static async Task<int> DownloadFromCrowdin(IConfiguration config, Options opts, AutoResetEvent gate, IFileSystem fs)
		{
			var crowdin = CrowdinCommand.GetClient();

			var success = 1;
			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials { ProjectKey = projectKey };

			var outputFile = Path.Combine(config["base_path"], opts.Filename);
			if (!string.IsNullOrEmpty(projectKey))
			{
				try
				{
					var exportFailed = false;
					if (opts.ExportFirst)
					{
						var exportResponse = await crowdin.ExportTranslation(projectId, projectCredentials, new ExportTranslationParameters());
						exportFailed = !exportResponse.IsSuccessStatusCode;
					}
					if (!exportFailed)
					{
						var downloadResponse = await crowdin.DownloadTranslation(projectId,
							projectCredentials, new DownloadTranslationParameters { Package = opts.Language });
						if (downloadResponse.IsSuccessStatusCode)
						{
							using (var downloadedFile = fs.FileStream.Create(outputFile, FileMode.Create))
							{
								downloadedFile.Write(await downloadResponse.Content.ReadAsByteArrayAsync());
							}
						}
						success = 0;
					}
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
			}
			else
			{
				Console.WriteLine("{0} did not contain the API Key for your Crowdin project.", config["api_key_env"]);
			}
			gate.Set();
			return success;
		}
	}
}
