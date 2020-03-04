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

			public Task<ProjectInfo> GetProjectInfo(string projectIdentifier, Credentials credentials)
			{
				return _crowdin.GetProjectInfo(projectIdentifier, credentials);
			}

			public Task<HttpResponseMessage> UpdateFile(string projectId, Credentials credentials, UpdateFileParameters updateFileParameters)
			{
				return _crowdin.UpdateFile(projectId, credentials, updateFileParameters);
			}

			public Task<HttpResponseMessage> AddFile(string projectId, Credentials credentials, AddFileParameters addFileParams)
			{
				return _crowdin.AddFile(projectId, credentials, addFileParams);
			}

			public Task<HttpResponseMessage> CreateFolder(string projectId, Credentials credentials, CreateFolderParameters createFolderParams)
			{
				return _crowdin.CreateFolder(projectId, credentials, createFolderParams);
			}

			public Task<HttpResponseMessage> ExportTranslation(string projectId, Credentials credentials,
				ExportTranslationParameters exportTranslationParameters)
			{
				return _crowdin.ExportTranslation(projectId, credentials, exportTranslationParameters);
			}

			public Task<HttpResponseMessage> GetExportStatus(string projectId, Credentials credentials,
				GetTranslationExportStatusParameters exportTranslationParameters)
			{
				return _crowdin.GetTranslationExportStatus(projectId, credentials, exportTranslationParameters);
			}

			public Task<HttpResponseMessage> DownloadTranslation(string projectId, Credentials credentials,
				DownloadTranslationParameters downloadTranslationParameters)
			{
				return _crowdin.DownloadTranslation(projectId, credentials, downloadTranslationParameters);
			}
		}
	}
}
