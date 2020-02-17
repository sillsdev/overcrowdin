using System;
using System.IO;
using System.IO.Abstractions;
using System.Net;
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

			[Option('e', Required = false, Default = false, HelpText = "Export all translations before downloading. This generates the .zip files" +
				"\non the Crowdin server. If false, the most-recently-exported translations will be downloaded.")]
			public bool ExportFirst { get; set; }

			[Option('f', Required = true, HelpText = "Path and filename relative to the configured basepath for the zip file.")]
			public string Filename { get; set; }
		}


		public static async Task<int> DownloadFromCrowdin(IConfiguration config, Options opts, IFileSystem fs)
		{
			var crowdin = CrowdinCommand.GetClient();

			var status = 1;
			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials { ProjectKey = projectKey };

			var outputFile = Path.Combine(config["base_path"], opts.Filename);
			if (!string.IsNullOrEmpty(projectKey))
			{
				try
				{
					dynamic exportResponse = new {IsSuccessStatusCode = true};
					if (opts.ExportFirst)
					{
						exportResponse = await crowdin.ExportTranslation(projectId, projectCredentials, new ExportTranslationParameters());
					}
					if (exportResponse.IsSuccessStatusCode)
					{
						var downloadResponse = await crowdin.DownloadTranslation(projectId,
							projectCredentials, new DownloadTranslationParameters { Package = opts.Language });
						if (downloadResponse.IsSuccessStatusCode)
						{
							using (var downloadedFile = fs.FileStream.Create(outputFile, FileMode.Create))
							{
								downloadedFile.Write(await downloadResponse.Content.ReadAsByteArrayAsync());
							}
							Console.WriteLine("Translations file downloaded to {0}", outputFile);
							status = 0;
						}
						else
						{
							Console.WriteLine("Failed to download translations.");
							if (downloadResponse.StatusCode == HttpStatusCode.NotFound && !opts.ExportFirst)
							{
								Console.WriteLine("Did you export your translations?");
							}
							if (opts.Verbose)
							{
								var error = await downloadResponse.Content.ReadAsStringAsync();
								Console.WriteLine(error);
							}
						}
					}
					else
					{
						Console.WriteLine("Failed to export translations.");
						if (opts.Verbose)
						{
							var error = await exportResponse.Content.ReadAsStringAsync();
							Console.WriteLine(error);
						}
					}
				}
				catch (CrowdinException e)
				{
					Console.WriteLine("Failed to download translations. Check your project id and project key.");
					if (opts.Verbose)
					{
						Console.WriteLine($"{e.ErrorCode}: {e.Message}");
						Console.WriteLine(e.StackTrace);
					}
				}
			}
			else
			{
				Console.WriteLine("{0} did not contain the API Key for your Crowdin project.", config["api_key_env"]);
			}
			return status;
		}
	}
}
