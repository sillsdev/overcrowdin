using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	internal sealed class Program
	{
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
			var parseResult = Parser.Default.ParseArguments<UpdateCommand.Options,
					AddCommand.Options, DownloadCommand.Options>(args);
			if (parseResult.Tag is ParserResultType.NotParsed)
			{
				return 1;
			}

			var errorLevel = 2;
			await parseResult.WithParsedAsync<UpdateCommand.Options>(async opts =>
			{
				errorLevel = await UpdateCommand.UpdateFilesInCrowdin(config, opts, fileSystem, clientFactory);
			});
			await parseResult.WithParsedAsync<AddCommand.Options>(async opts =>
			{
				errorLevel = await AddCommand.AddFilesToCrowdin(config, opts, fileSystem, clientFactory);
			});
			await parseResult.WithParsedAsync<DownloadCommand.Options>(async opts =>
			{
				errorLevel = await DownloadCommand.DownloadFromCrowdin(config, opts, fileSystem, clientFactory);
			});
			return errorLevel;
		}
	}
}