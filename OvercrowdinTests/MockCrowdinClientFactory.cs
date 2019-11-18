using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Overcrowdin;

namespace OvercrowdinTests
{
	internal class MockCrowdinClientFactory : ICrowdinClientFactory
	{
		public ICrowdinClient Create()
		{
			return new MockCrowdinClient();
		}

		internal class MockCrowdinClient : ICrowdinClient
		{
			public Task<ProjectInfo> GetProjectInfo(string projectIdentifier, ProjectCredentials projectCredentials)
			{
				throw new System.NotImplementedException();
			}

			public Task<HttpResponseMessage> UpdateFile(string projectId, ProjectCredentials projectCredentials, UpdateFileParameters updateFileParameters)
			{
				throw new System.NotImplementedException();
			}

			public Task<HttpResponseMessage> AddFile(string projectId, ProjectCredentials projectCredentials, AddFileParameters addFileParams)
			{
				throw new System.NotImplementedException();
			}

			public Task ExportTranslation(string projectId, ProjectCredentials projectCredentials,
				ExportTranslationParameters exportTranslationParameters)
			{
				throw new System.NotImplementedException();
			}

			public Task<HttpResponseMessage> DownloadTranslation(string projectId, ProjectCredentials projectCredentials,
				DownloadTranslationParameters downloadTranslationParameters)
			{
				throw new System.NotImplementedException();
			}
		}
	}
}