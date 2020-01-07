using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public sealed class CreateFolderCommand
	{
		public static async Task<int> CreateFolderInCrowdin(IConfiguration config, GlobalOptions opts, string folder, AutoResetEvent gate, IFileSystem fs)
		{
			var crowdin = CrowdinCommand.GetClient();
			var projectId = config["project_identifier"];
			var projectKey = Environment.GetEnvironmentVariable(config["api_key_env"]);
			var projectCredentials = new ProjectCredentials { ProjectKey = projectKey };
			var createFolderParams = new CreateFolderParameters {Name = folder};
			if (opts.Verbose)
			{
				Console.WriteLine("Creating folder {0}...", folder);
			}
			var result = await crowdin.CreateFolder(projectId, projectCredentials, createFolderParams);
			if (result.IsSuccessStatusCode)
			{
				if (opts.Verbose)
				{
					Console.WriteLine("Finished creating folder.");
					var info = await result.Content.ReadAsStringAsync();
					Console.WriteLine(info);
				}
			}
			else
			{
				Console.WriteLine("Failure creating folder.");
				if (opts.Verbose)
				{
					var error = await result.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}
			gate.Set();
			return result.IsSuccessStatusCode ? 0 : 1;
		}
	}
}