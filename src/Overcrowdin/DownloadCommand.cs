using System;
using System.IO;
using System.IO.Abstractions;
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
			[Option('b', "branch", Required = false, HelpText = "Name of the Crowdin branch (overrides any branch in crowdin.json)")]
			public string Branch { get; set; }

			[Option('f', Required = true, HelpText = "Path and filename relative to the configured base path for the zip file.")]
			public string Filename { get; set; }
		}

		public static async Task<int> DownloadFromCrowdin(IConfiguration config, Options opts, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null)
		{
			var credentials = CommandUtilities.GetProjectSettingsFromConfiguration(config, opts.Branch, apiFactory);
			if (credentials == null)
			{
				return 1;
			}
			var outputFile = Path.Combine(config["base_path"], opts.Filename);

			try
			{
				var crowdinDownloadHelper = await CrowdinDownloadHelper.Create(credentials, fs, apiFactory, factory);
				var result = await crowdinDownloadHelper.DownloadTranslations(outputFile);
				return result ? 0 : 1;
			}
			catch (Exception)
			{
				Console.WriteLine("Failed to export translations. Check your project id and project key.");
				throw;
			}
		}
	}
}