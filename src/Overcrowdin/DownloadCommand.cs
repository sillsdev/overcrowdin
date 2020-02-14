using System;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
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

			[Option('r', Required = false, Default = 0, HelpText = "Number of times to retry failed downloads (in four-minute intervals)")]
			public int Retries { get; set; }
		}

		/// <summary>time to wait before retrying failed downloads (four minutes = 240,000ms)</summary>
		private const int RetryWaitTime = 240000;

		/// <summary>Number of milliseconds in a minute (60,000ms)</summary>
		private const int MillisPerMinute = 60000;

		public static async Task<int> DownloadFromCrowdin(IConfiguration config, Options opts, IFileSystem fs)
		{
			var crowdin = CrowdinCommand.GetClient();

			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials {ProjectKey = projectKey};

			var outputFile = Path.Combine(config["base_path"], opts.Filename);
			if (string.IsNullOrEmpty(projectKey))
			{
				Console.WriteLine("{0} did not contain the API Key for your Crowdin project.", config["api_key_env"]);
				return 1;
			}

			try
			{
				// Export translations if requested
				if (opts.ExportFirst)
				{
					var exportResponse = await crowdin.ExportTranslation(projectId, projectCredentials, new ExportTranslationParameters());
					if (!exportResponse.IsSuccessStatusCode)
					{
						Console.WriteLine("Failed to export translations.");
						WriteResponseIf(opts.Verbose, exportResponse);
						return 1;
					}
				}

				// Download translations
				for (var retries = opts.Retries; retries >= 0; retries --)
				{
					try
					{
						var downloadResponse = await crowdin.DownloadTranslation(projectId,
							projectCredentials, new DownloadTranslationParameters {Package = opts.Language});
						if (downloadResponse.IsSuccessStatusCode)
						{
							using (var downloadedFile = fs.FileStream.Create(outputFile, FileMode.Create))
							{
								downloadedFile.Write(await downloadResponse.Content.ReadAsByteArrayAsync());
							}

							Console.WriteLine("Translations file downloaded to {0}", outputFile);
							return 0;
						}
						Console.WriteLine("Failed to download translations.");
						if (IsPermanent(downloadResponse.StatusCode))
						{
							if (downloadResponse.StatusCode == HttpStatusCode.NotFound && !opts.ExportFirst)
							{
								Console.WriteLine("Did you export your translations?");
							}
							WriteResponseIf(opts.Verbose, downloadResponse);
							return 1;
						}

						WriteResponseIf(opts.Verbose, downloadResponse);
					}
					catch (HttpRequestException e)
					{
						Console.WriteLine("Possibly no network connection.");
						if (opts.Verbose)
						{
							Console.WriteLine(e.Message);
						}
					}

					if (retries > 0)
					{
						Console.WriteLine($"Trying {retries} more times in {RetryWaitTime/MillisPerMinute}-minute intervals.");
						Thread.Sleep(RetryWaitTime);
					}
				}
			}
			catch (CrowdinException e)
			{
				Console.WriteLine("Failed to export translations. Check your project id and project key.");
				if (opts.Verbose)
				{
					Console.WriteLine($"{e.ErrorCode}: {e.Message}");
					Console.WriteLine(e.StackTrace);
				}
			}
			return 1;
		}

		private static async void WriteResponseIf(bool condition, HttpResponseMessage response)
		{
			if (!condition)
				return;
			var message = await response.Content.ReadAsStringAsync();
			Console.WriteLine(message);
		}

		private static bool IsPermanent(HttpStatusCode error)
		{
			return error == HttpStatusCode.NotFound || // translations may not have been exported
				error == HttpStatusCode.InternalServerError; // Crowdin API returned an error
		}
	}
}
