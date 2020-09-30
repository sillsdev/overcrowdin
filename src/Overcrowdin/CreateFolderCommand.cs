using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
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
			var crowdin = CrowdinCommand.GetClient();
			using (var foldersE = folders.GetEnumerator())
			{
				var status = 0;
				while (status == 0 && foldersE.MoveNext())
				{
					status = await CreateFolderInCrowdin(crowdin, config, opts, foldersE.Current, fs);
				}
				return status;
			}
		}

		internal static async Task<int> CreateFolderInCrowdin(ICrowdinClient crowdin, IConfiguration config, GlobalOptions opts, string folder, IFileSystem fs)
		{
			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			var branch = (opts as IBranchOptions)?.Branch ?? config["branch"];
			var createFolderParams = new CreateFolderParameters {Name = folder, Branch = branch, IsBranch = !string.IsNullOrEmpty(branch)};
			if (opts.Verbose)
			{
				Console.WriteLine("Creating folder {0}...", folder);
			}
			var result = await crowdin.CreateFolder(projectId, credentials, createFolderParams);
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
					// ENHANCE (Hasso) 2020.01: Extracting error codes could be refactored into a method for other clients
					using (var xmlReader = XmlReader.Create(await result.Content.ReadAsStreamAsync()))
					{
						if (_errorSerializer.CanDeserialize(xmlReader))
						{
							var error = (Error)_errorSerializer.Deserialize(xmlReader);
							if (error.Code == (int) CrowdinErrorCode.DirectoryAlreadyExists)
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