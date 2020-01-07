using System;
using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;

namespace Overcrowdin
{
	class CrowdinV1ApiFactory : ICrowdinClientFactory
	{
		public ICrowdinClient Create()
		{
			return new CrowdinApiV1();
		}

		/// <summary>
		/// Wrapper around the Crowdin.Api.Client class
		/// </summary>
		private class CrowdinApiV1 : ICrowdinClient
		{
			private Client _crowdin;

			internal CrowdinApiV1()
			{
				var httpClient = new HttpClient { BaseAddress = new Uri("https://api.crowdin.com/api/") };
				_crowdin = new Client(httpClient);
			}

			public Task<ProjectInfo> GetProjectInfo(string projectIdentifier, ProjectCredentials projectCredentials)
			{
				return _crowdin.GetProjectInfo(projectIdentifier, projectCredentials);
			}

			public Task<HttpResponseMessage> UpdateFile(string projectId, ProjectCredentials projectCredentials, UpdateFileParameters updateFileParameters)
			{
				return _crowdin.UpdateFile(projectId, projectCredentials, updateFileParameters);
			}

			public Task<HttpResponseMessage> AddFile(string projectId, ProjectCredentials projectCredentials, AddFileParameters addFileParams)
			{
				return _crowdin.AddFile(projectId, projectCredentials, addFileParams);
			}

			public Task<HttpResponseMessage> CreateFolder(string projectId, ProjectCredentials projectCredentials, CreateFolderParameters createFolderParams)
			{
				return _crowdin.CreateFolder(projectId, projectCredentials, createFolderParams);
			}

			public Task<HttpResponseMessage> ExportTranslation(string projectId, ProjectCredentials projectCredentials,
				ExportTranslationParameters exportTranslationParameters)
			{
				return _crowdin.ExportTranslation(projectId, projectCredentials, exportTranslationParameters);
			}

			public Task<HttpResponseMessage> DownloadTranslation(string projectId, ProjectCredentials projectCredentials,
				DownloadTranslationParameters downloadTranslationParameters)
			{
				return _crowdin.DownloadTranslation(projectId, projectCredentials, downloadTranslationParameters);
			}
		}
	}
}
