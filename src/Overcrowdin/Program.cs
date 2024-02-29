using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
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

		private static async Task<int> Main(string[] args)
		{
			// Set the Crowdin client factory for the main application
			var clientFactory = new CrowdinApiFactory();
			IFileSystem fileSystem = new FileSystem();

			// Build the configuration from various sources
			IConfiguration config = new ConfigurationBuilder()
				.SetBasePath(Environment.CurrentDirectory)
				.AddJsonFile("crowdin.json", true, false)
				.AddEnvironmentVariables()
				.AddCommandLine(args)
				.Build();
			int result = 1;
			var parseResult = Parser.Default.ParseArguments<UpdateCommand.Options,
					AddCommand.Options, DownloadCommand.Options>(args);
			await parseResult.WithParsedAsync<UpdateCommand.Options>(async opts =>
			{
				await UpdateCommand.UpdateFilesInCrowdin(config, opts, fileSystem, clientFactory);
			});
			await parseResult.WithParsedAsync<AddCommand.Options>(async opts =>
			{
				await AddCommand.AddFilesToCrowdin(config, opts, fileSystem, clientFactory);
			});
			await parseResult.WithParsedAsync<DownloadCommand.Options>(async opts =>
			{
				await DownloadCommand.DownloadFromCrowdin(config, opts, fileSystem, clientFactory);
			});
			return parseResult.Tag is ParserResultType.NotParsed ? 1 : result;
		}
	}
}