using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;

namespace Overcrowdin
{
	public interface ICrowdinClient
	{
		Task<ProjectInfo> GetProjectInfo(string projectIdentifier, ProjectCredentials projectCredentials);
		Task<HttpResponseMessage> UpdateFile(string projectId, ProjectCredentials projectCredentials, UpdateFileParameters updateFileParameters);
		Task<HttpResponseMessage> AddFile(string projectId, ProjectCredentials projectCredentials, AddFileParameters addFileParams);
		Task<HttpResponseMessage> ExportTranslation(string projectId, ProjectCredentials projectCredentials, ExportTranslationParameters exportTranslationParameters);
		Task<HttpResponseMessage> DownloadTranslation(string projectId, ProjectCredentials projectCredentials, DownloadTranslationParameters downloadTranslationParameters);
	}
}