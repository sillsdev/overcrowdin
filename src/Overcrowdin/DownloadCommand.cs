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

			[Option('r', Required = false, Default = 0, HelpText = "Number of times to retry failed downloads (in four-minute intervals)")]
			public int Retries { get; set; }
		}

		/// <summary>time to wait before retrying failed downloads (four minutes = 240,000ms)</summary>
		private const int RetryWaitTime = 240000;

		/// <summary>Number of milliseconds in a minute (60,000ms)</summary>
		private const int MillisPerMinute = 60000;

		public static async Task<int> DownloadFromCrowdin(IConfiguration config, Options opts, AutoResetEvent gate, IFileSystem fs)
		{
			try
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
					if (opts.ExportFirst)
					{
						var exportResponse = await crowdin.ExportTranslation(projectId, projectCredentials,
							new ExportTranslationParameters());
						if (!exportResponse.IsSuccessStatusCode)
						{
							Console.WriteLine("Failed to export translations.");
							if (opts.Verbose)
							{
								var error = await exportResponse.Content.ReadAsStringAsync();
								Console.WriteLine(error);
							}
							return 1;
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
						if (downloadResponse.StatusCode == HttpStatusCode.NotFound && !opts.ExportFirst)
						{
							Console.WriteLine("Did you export your translations?");
							if (opts.Verbose)
							{
								var error = await downloadResponse.Content.ReadAsStringAsync();
								Console.WriteLine(error);
							}
							return 1;
						}

						if (opts.Verbose)
						{
							var error = await downloadResponse.Content.ReadAsStringAsync();
							Console.WriteLine(error);
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

					if (retries > 0)
					{
						Console.WriteLine($"Trying {retries} more times in {RetryWaitTime/MillisPerMinute}-minute intervals.");
						Thread.Sleep(RetryWaitTime);
					}
				}

				return 1;
			}
			finally
			{
				gate.Set();
			}
		}
	}
}
