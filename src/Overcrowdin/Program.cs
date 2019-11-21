using System;
using System.IO.Abstractions;
using System.Threading;
using CommandLine;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	internal sealed class Program
	{
		private Program(IConfiguration config)
		{
			Configuration = config;
		}

		private IConfiguration Configuration { get; }
		/// <summary>
		/// Because the async lambda functions return immediately we need a semaphore to make sure we wait for the
		/// Crowdin response on any Crowdin api calls before we exit.
		/// </summary>
		static readonly AutoResetEvent Gate = new AutoResetEvent(false);

		private static int Main(string[] args)
		{
			// Set the static Crowdin client factory for the main application
			CrowdinCommand.ClientFactory = new CrowdinV1ApiFactory();
			IFileSystem fileSystem = new FileSystem();

			// Build the configuration from various sources
			IConfiguration config = new ConfigurationBuilder()
				.SetBasePath(Environment.CurrentDirectory)
				.AddJsonFile("crowdin.json", true, false)
				.AddYamlFile("crowdin.yaml", true, false)
				.AddEnvironmentVariables()
				.AddCommandLine(args)
				.Build();
			int result = 1;
			var parseResult = Parser.Default.ParseArguments<GenerateCommand.Options, UpdateCommand.Options, AddCommand.Options, DownloadCommand.Options>(args)
				.WithParsed<GenerateCommand.Options>(async opts =>
					{
						result = await GenerateCommand.GenerateConfigFromCrowdin(config, opts, Gate, fileSystem);
					})
				.WithParsed<UpdateCommand.Options>(async opts =>
				{
					result = await UpdateCommand.UpdateFilesInCrowdin(config, opts, Gate, fileSystem);
				})
				.WithParsed<AddCommand.Options>(async opts => await AddCommand.AddFilesToCrowdin(config, opts, Gate, fileSystem))
				.WithParsed<DownloadCommand.Options>(async opts => await DownloadCommand.DownloadFromCrowdin(config, opts, Gate))
				.WithNotParsed(errs =>
				{
					Gate.Set();
				});
			Gate.WaitOne();
			return parseResult.Tag is ParserResultType.NotParsed ? 1 : result;
		}
	}
}