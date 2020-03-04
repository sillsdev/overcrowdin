using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using CommandLine;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public class ExportCommand
	{
		[Verb("exporttranslations", HelpText = "Export the latest translations from Crowdin (does not download them)")]
		public class Options : GlobalOptions
		{
			[Option ('a', Required = false, Default = false, HelpText = "Asynchronous export (return without waiting for the export to complete).")]
			public bool Asynchronous { get; set; }
		}

		/// <summary>Number of milliseconds in a minute (60,000ms)</summary>
		private const int MillisPerMinute = 60000;

		private static readonly XmlSerializer _statusSerializer;

		static ExportCommand()
		{
			_statusSerializer = new XmlSerializer(typeof(ExportStatus));
		}

		public static async Task<int> ExportCrowdinTranslations(IConfiguration config, Options opts)
		{
			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			if (credentials == null)
			{
				return 1;
			}

			return await ExportCrowdinTranslations(projectId, credentials, opts.Verbose, opts.Asynchronous);
		}

		public static async Task<int> ExportCrowdinTranslations(string projectId, Credentials credentials, bool verbose, bool asynchronous = false)
		{
			var crowdin = CrowdinCommand.GetClient();
			var exportResponse = await crowdin.ExportTranslation(projectId, credentials, new ExportTranslationParameters {Async = true});
			if (exportResponse.IsSuccessStatusCode)
				return asynchronous ? 0 : await AwaitCrowdinBuild(crowdin, projectId, credentials, verbose);

			Console.WriteLine("Failed to export translations.");
			WriteResponseIf(verbose, exportResponse);
			return 1;
		}

		/// <summary>
		/// Wait until the Crowdin build is complete, checking status every minute. Tolerate a few errors (in case of inconsistent internet)
		/// </summary>
		private static async Task<int> AwaitCrowdinBuild(ICrowdinClient crowdin, string projectId, Credentials credentials, bool verbose)
		{
			HttpResponseMessage statusResponse = null;
			for (int consecutiveFailures = 0, percentComplete = -1; consecutiveFailures < 12;)
			{
				statusResponse = await crowdin.GetExportStatus(projectId, credentials, new GetTranslationExportStatusParameters());
				if (statusResponse.IsSuccessStatusCode)
				{
					consecutiveFailures = 0;
					using (var xmlReader = XmlReader.Create(await statusResponse.Content.ReadAsStreamAsync()))
					{
						if (_statusSerializer.CanDeserialize(xmlReader))
						{
							var status = (ExportStatus)_statusSerializer.Deserialize(xmlReader);
							if (status.Status == ExportStatus.Finished)
							{
								Console.WriteLine("Finished exporting translations.");
								return 0;
							}
							if (percentComplete == status.Progress)
							{
								Console.WriteLine($"Export seems to have stalled at {percentComplete}%. Giving up.");
								return 1;
							}
							percentComplete = status.Progress;
							var verboseMessage = verbose ? $"\tpresently exporting {status.CurrentFile} in {status.CurrentLanguage}" : string.Empty;
							Console.WriteLine($"{percentComplete}% complete exporting translations...{verboseMessage}");
						}
						else
						{
							consecutiveFailures++;
							WriteResponseIf(verbose, statusResponse);
						}
					}
				}
				else
				{
					consecutiveFailures++;
					WriteResponseIf(verbose, statusResponse);
				}
				Thread.Sleep(MillisPerMinute);
			}

			// Too many errors; give up.
			Console.WriteLine("Failed to export translations.");
			WriteResponseIf(verbose, statusResponse);
			return 1;
		}

		private static async void WriteResponseIf(bool condition, HttpResponseMessage response)
		{
			if (!condition)
				return;
			var message = await response.Content.ReadAsStringAsync();
			Console.WriteLine(message);
		}
	}
}
