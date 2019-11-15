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
			IConfiguration config = new ConfigurationBuilder()
				.AddJsonFile("crowdin.json", true, false)
				.AddYamlFile("crowdin.yaml", true, false)
				.AddEnvironmentVariables()
				.AddCommandLine(args)
				.Build();
			int result = 1;
			var parseResult = Parser.Default.ParseArguments<GenerateCommand.Options, UpdateCommand.Options, AddCommand.Options>(args)
				.WithParsed<GenerateCommand.Options>(async opts =>
					{
						result = await GenerateCommand.GenerateConfigFromCrowdin(config, opts, Gate);
					})
				.WithParsed<UpdateCommand.Options>(async opts =>
				{
					result = await UpdateCommand.UpdateFilesInCrowdin(config, opts, Gate);
				})
				.WithParsed<AddCommand.Options>(async opts => await AddCommand.AddFilesToCrowdin(config, opts, Gate))
				.WithNotParsed(errs =>
				{
					Gate.Set();
				});
			Gate.WaitOne();
			return parseResult.Tag is ParserResultType.NotParsed ? 1 : result;
		}
	}
}