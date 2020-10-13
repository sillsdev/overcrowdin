using System;
using System.Collections.Generic;
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
		private static readonly XmlSerializer ErrorSerializer;

		static CreateFolderCommand()
		{
			ErrorSerializer = new XmlSerializer(typeof(Error));
		}

		public static async Task<int> CreateFoldersInCrowdin(IConfiguration config, GlobalOptions opts, ISet<string> folders)
		{
			var branch = CommandUtilities.GetBranch(config, opts as IBranchOptions);
			if (folders.Count == 0 && string.IsNullOrEmpty(branch))
			{
				return 0;
			}

			var crowdin = CrowdinCommand.GetClient();
			var status = 0;
			if (!string.IsNullOrEmpty(branch))
			{
				status = await CreateBranchInCrowdin(crowdin, config, opts, branch);
				if (status != 0)
				{
					return status;
				}
			}

			Console.WriteLine("Creating {0} folders...", folders.Count);
			using (var foldersE = folders.GetEnumerator())
			{
				while (status == 0 && foldersE.MoveNext())
				{
					status = await CreateFolderInCrowdin(crowdin, config, opts, foldersE.Current, branch);
				}
				return status;
			}
		}

		private static async Task<int> CreateFolderInCrowdin(ICrowdinClient crowdin, IConfiguration config, GlobalOptions opts,
			string folder, string branch)
		{
			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			var createFolderParams = new CreateFolderParameters {Name = folder, Branch = branch};
			return await CreateFolderOrBranchInternal(crowdin, opts, "folder", folder, projectId, credentials, createFolderParams);
		}

		private static async Task<int> CreateBranchInCrowdin(ICrowdinClient crowdin, IConfiguration config, GlobalOptions opts, string branch)
		{
			var projectId = config["project_identifier"];
			var credentials = CommandUtilities.GetCredentialsFromConfiguration(config);
			var createBranchParams = new CreateFolderParameters {Name = branch, IsBranch = true};
			return await CreateFolderOrBranchInternal(crowdin, opts, "branch", branch, projectId, credentials, createBranchParams);
		}

		private static async Task<int> CreateFolderOrBranchInternal(ICrowdinClient crowdin, GlobalOptions opts, string folderOrBranch, string name,
			string projectId, Credentials credentials, CreateFolderParameters createFolderParams)
		{
			if (opts.Verbose)
			{
				Console.WriteLine($"Creating {folderOrBranch} {name}...");
			}
			var result = await crowdin.CreateFolder(projectId, credentials, createFolderParams);
			if (result.IsSuccessStatusCode)
			{
				if (opts.Verbose)
				{
					Console.WriteLine($"Finished creating {folderOrBranch}.");
					var info = await result.Content.ReadAsStringAsync();
					Console.WriteLine(info);
				}
			}
			else
			{
				// When this was written in 2020.01, the status code for existing folders was 500. In 2020.10, we noticed that it had changed to 400.
				// Ideally, Crowdin would not change their API like this. Since they have, we may wish to safely parse the XML regardless of the status code.
				if (result.StatusCode == HttpStatusCode.BadRequest || result.StatusCode == HttpStatusCode.InternalServerError)
				{
					// ENHANCE (Hasso) 2020.01: Extracting error codes could be refactored into a method for other clients
					using (var xmlReader = XmlReader.Create(await result.Content.ReadAsStreamAsync()))
					{
						if (ErrorSerializer.CanDeserialize(xmlReader))
						{
							var error = (Error)ErrorSerializer.Deserialize(xmlReader);
							if (error.Code == (int) CrowdinErrorCode.DirectoryAlreadyExists)
							{
								if (opts.Verbose)
								{
									Console.WriteLine($"The {folderOrBranch} already exists.");
								}
								// An existing folder is not a problem. The client wanted the folder created; it now exists; report success.
								return 0;
							}
						}
					}

				}
				Console.WriteLine($"Failure creating {folderOrBranch}.");
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