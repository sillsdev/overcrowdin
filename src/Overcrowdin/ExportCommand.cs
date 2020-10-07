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
		public class Options : GlobalOptions, IBranchOptions
		{
			[Option('b', "branch", Required = false, HelpText = "Name of the version branch")]
			public string Branch { get; set; }

			[Option ('a', Required = false, Default = false, HelpText = "Asynchronous export (return without waiting for the export to complete).")]
			public bool Asynchronous { get; set; }

			public Options Clone()
			{
				return new Options
				{
					Asynchronous = Asynchronous,
					Branch = Branch,
					Verbose = Verbose
				};
			}
		}

		/// <summary>Number of milliseconds in a minute (60,000ms)</summary>
		private const int MillisPerMinute = 60000;

		private static readonly XmlSerializer StatusSerializer;

		static ExportCommand()
		{
			StatusSerializer = new XmlSerializer(typeof(ExportStatus));
		}

		public static async Task<int> ExportCrowdinTranslations(IConfiguration config, Options opts)
		{
			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			if (credentials == null)
			{
				return 1;
			}

			var computedOpts = opts.Clone();
			computedOpts.Branch = CommandUtilities.Branch(config, opts);
			return await ExportCrowdinTranslations(projectId, credentials, computedOpts);
		}

		public static async Task<int> ExportCrowdinTranslations(string projectId, Credentials credentials, Options opts)
		{
			var crowdin = CrowdinCommand.GetClient();
			var exportResponse = await crowdin.ExportTranslation(projectId, credentials, new ExportTranslationParameters {Async = true, Branch = opts.Branch});
			if (exportResponse.IsSuccessStatusCode)
				return opts.Asynchronous ? 0 : await AwaitCrowdinBuild(crowdin, projectId, credentials, opts);

			Console.WriteLine("Failed to export translations.");
			WriteResponseIf(opts.Verbose, exportResponse);
			return 1;
		}

		/// <summary>
		/// Wait until the Crowdin build is complete, checking status every minute. Tolerate a few errors (in case of inconsistent internet)
		/// </summary>
		private static async Task<int> AwaitCrowdinBuild(ICrowdinClient crowdin, string projectId, Credentials credentials, Options opts)
		{
			HttpResponseMessage statusResponse = null;
			for (int consecutiveFailures = 0, percentComplete = -1; consecutiveFailures < 12;)
			{
				statusResponse = await crowdin.GetExportStatus(projectId, credentials, new GetTranslationExportStatusParameters{Branch = opts.Branch});
				if (statusResponse.IsSuccessStatusCode)
				{
					consecutiveFailures = 0;
					using (var xmlReader = XmlReader.Create(await statusResponse.Content.ReadAsStreamAsync()))
					{
						if (StatusSerializer.CanDeserialize(xmlReader))
						{
							var status = (ExportStatus)StatusSerializer.Deserialize(xmlReader);
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
							var verboseMessage = opts.Verbose ? $"\tpresently exporting {status.CurrentFile} in {status.CurrentLanguage}" : string.Empty;
							Console.WriteLine($"{percentComplete}% complete exporting translations...{verboseMessage}");
						}
						else
						{
							consecutiveFailures++;
							WriteResponseIf(opts.Verbose, statusResponse);
						}
					}
				}
				else
				{
					consecutiveFailures++;
					WriteResponseIf(opts.Verbose, statusResponse);
				}
				Thread.Sleep(MillisPerMinute);
			}

			// Too many errors; give up.
			Console.WriteLine("Failed to export translations.");
			WriteResponseIf(opts.Verbose, statusResponse);
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
