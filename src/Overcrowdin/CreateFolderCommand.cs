using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public sealed class CreateFolderCommand
	{
		private static readonly XmlSerializer _errorSerializer;

		static CreateFolderCommand()
		{
			_errorSerializer = new XmlSerializer(typeof(Error));
		}

		public static async Task<int> CreateFoldersInCrowdin(IConfiguration config, GlobalOptions opts, ISet<string> folders, IFileSystem fs)
		{
			if (folders.Count == 0)
			{
				return 0;
			}

			Console.WriteLine("Creating {0} folders...", folders.Count);
			using (var foldersE = folders.GetEnumerator())
			{
				var status = 0;
				while (status == 0 && foldersE.MoveNext())
				{
					status = await CreateFolderInCrowdin(config, opts, foldersE.Current, fs);
				}
				return status;
			}
		}

		public static async Task<int> CreateFolderInCrowdin(IConfiguration config, GlobalOptions opts, string folder, IFileSystem fs)
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
				if (result.StatusCode == HttpStatusCode.InternalServerError)
				{
					using (var xmlReader = XmlReader.Create(await result.Content.ReadAsStreamAsync()))
					{
						if (_errorSerializer.CanDeserialize(xmlReader))
						{
							var error = (Error)_errorSerializer.Deserialize(xmlReader);
							if (error.Code == (int) CrowdinErrorCodes.DirectoryAlreadyExists)
							{
								if (opts.Verbose)
								{
									Console.WriteLine("Folder already exists.");
								}
								// An existing folder is not a problem. The client wanted the folder created; it now exists; report success.
								return 0;
							}
						}
					}

				}
				Console.WriteLine("Failure creating folder.");
				if (opts.Verbose)
				{
					var error = await result.Content.ReadAsStringAsync();
					Console.WriteLine(error);
				}
			}
			return result.IsSuccessStatusCode ? 0 : 1;
		}
	}
}