using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;

namespace Overcrowdin
{
	public interface ICrowdinClient
	{
		Task<ProjectInfo> GetProjectInfo(string projectIdentifier, Credentials credentials);
		Task<HttpResponseMessage> UpdateFile(string projectId, Credentials credentials, UpdateFileParameters updateFileParameters);
		Task<HttpResponseMessage> AddFile(string projectId, Credentials credentials, AddFileParameters addFileParams);
		Task<HttpResponseMessage> CreateFolder(string projectId, Credentials credentials, CreateFolderParameters createFolderParams);
		Task<HttpResponseMessage> ExportTranslation(string projectId, Credentials credentials, ExportTranslationParameters exportTranslationParameters);
		Task<HttpResponseMessage> GetExportStatus(string projectId, Credentials credentials, GetTranslationExportStatusParameters exportTranslationParameters);
		Task<HttpResponseMessage> DownloadTranslation(string projectId, Credentials credentials, DownloadTranslationParameters downloadTranslationParameters);
	}
}