using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;

namespace Overcrowdin
{
	public interface ICrowdinClient
	{
		Task<ProjectInfo> GetProjectInfo(string projectIdentifier, Credentials projectCredentials);
		Task<HttpResponseMessage> UpdateFile(string projectId, Credentials projectCredentials, UpdateFileParameters updateFileParameters);
		Task<HttpResponseMessage> AddFile(string projectId, Credentials projectCredentials, AddFileParameters addFileParams);
		Task<HttpResponseMessage> CreateFolder(string projectId, Credentials projectCredentials, CreateFolderParameters createFolderParams);
		Task<HttpResponseMessage> ExportTranslation(string projectId, Credentials projectCredentials, ExportTranslationParameters exportTranslationParameters);
		Task<HttpResponseMessage> DownloadTranslation(string projectId, Credentials projectCredentials, DownloadTranslationParameters downloadTranslationParameters);
	}
}