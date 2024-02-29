using System;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public class DownloadCommand
	{
		[Verb("download", HelpText = "Download the latest translations from Crowdin")]
		public class Options : GlobalOptions, IBranchOptions
		{
			[Option('b', "branch", Required = false, HelpText = "Name of the version branch")]
			public string Branch { get; set; }

			[Option('l', Required = false, Default ="all", HelpText = "The language to download the translations for or 'all' to download for every language.")]
			public string Language { get; set; }

			[Option('f', Required = true, HelpText = "Path and filename relative to the configured basepath for the zip file.")]
			public string Filename { get; set; }
		}

		public static int DownloadFromCrowdin(IConfiguration config, Options opts, IFileSystem fs, IHttpClientFactory factory = null)
		{
			var credentials = CommandUtilities.GetProjectSettingsFromConfiguration(config, opts.Branch);
			if (credentials == null)
			{
				return 1;
			}
			var outputFile = Path.Combine(config["base_path"], opts.Filename);

			try
			{
				var crowdinDownloadHelper = CrowdInDownloadHelper.Create(credentials, fs, factory);
				if (!crowdinDownloadHelper.DownloadTranslations(outputFile))
				{
					return 1;
				}

				return 0;
			}
			catch (Exception e)
			{
				Console.WriteLine("Failed to export translations. Check your project id and project key.");
				if (opts.Verbose)
				{
					Console.WriteLine($"{e.Message}:");
					Console.WriteLine(e.StackTrace);
				}
			}
			return 1;
		}
	}
}
