using System;
using System.IO.Abstractions;
using System.Threading;
using CommandLine;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	internal sealed class Program
	{
		/// <summary>
		/// Because the async lambda functions return immediately we need a semaphore to make sure we wait for the
		/// Crowdin response on any Crowdin api calls before we exit.
		/// </summary>
		private static readonly AutoResetEvent Gate = new AutoResetEvent(false);

		private static int Main(string[] args)
		{
			// Set the static Crowdin client factory for the main application
			CrowdinCommand.ClientFactory = new CrowdinV1ApiFactory();
			IFileSystem fileSystem = new FileSystem();

			// Build the configuration from various sources
			IConfiguration config = new ConfigurationBuilder()
				.SetBasePath(Environment.CurrentDirectory)
				.AddJsonFile("crowdin.json", true, false)
				.AddEnvironmentVariables()
				.AddCommandLine(args)
				.Build();
			int result = 1;
			var parseResult = Parser.Default.ParseArguments<GenerateCommand.Options, UpdateCommand.Options,
					AddCommand.Options, DownloadCommand.Options, ExportCommand.Options>(args)
				.WithParsed<GenerateCommand.Options>(async opts =>
				{
					result = await GenerateCommand.GenerateConfigFromCrowdin(config, opts, fileSystem);
					Gate.Set();
				})
				.WithParsed<UpdateCommand.Options>(async opts =>
				{
					result = await UpdateCommand.UpdateFilesInCrowdin(config, opts, fileSystem);
					Gate.Set();
				})
				.WithParsed<AddCommand.Options>(async opts =>
				{
					result = await AddCommand.AddFilesToCrowdin(config, opts, fileSystem);
					Gate.Set();
				})
				.WithParsed<DownloadCommand.Options>(async opts =>
				{
					result = await DownloadCommand.DownloadFromCrowdin(config, opts, fileSystem);
					Gate.Set();
				})
				.WithParsed<ExportCommand.Options>(async opts =>
				{
					result = await ExportCommand.ExportCrowdinTranslations(config, opts);
					Gate.Set();
				})
				.WithNotParsed(errs =>
				{
					Gate.Set();
				});
			Gate.WaitOne();
			return parseResult.Tag is ParserResultType.NotParsed ? 1 : result;
		}
	}
}