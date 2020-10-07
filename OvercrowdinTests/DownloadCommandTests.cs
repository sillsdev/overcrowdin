using System;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Moq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class DownloadCommandTests : CrowdinApiTestBase
	{
		[Fact]
		public async void MissingApiKeyReturnsFailure()
		{
			var mockFileSystem = new MockFileSystem();
			const string apiKeyEnvVar = "NOKEYEXISTS";
			const string projectId = "testcrowdinproject";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(".");
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { ExportFirst = true, Filename = "done.zip" }, mockFileSystem.FileSystem);
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void ExportFirstTrueCallsExportAndDownload(bool useBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectId = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			var branch = useBranch ? "some-branch" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			// Set up the calls to Export and Download
			_mockClient.Setup(client => client.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(),
					It.IsAny<ExportTranslationParameters>()))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted))).Verifiable();
			var mockExportStatus = new HttpResponseMessage(HttpStatusCode.Accepted)
			{
				Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<success>
  <status>{ExportStatus.Finished}</status>
  <progress>100</progress>
  <last_build>2018-10-22T13:49:00+0000</last_build>
</success>")
			};
			_mockClient.Setup(client => client.GetExportStatus(projectId, It.IsAny<ProjectCredentials>(),
				It.Is<GetTranslationExportStatusParameters>(p => p.Branch == branch)))
				.Returns(Task.FromResult(mockExportStatus));
			_mockClient.Setup(client => client.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(),
					It.Is<DownloadTranslationParameters>(p => p.Branch == branch)))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) {Content = new ByteArrayContent(new byte[] {0x50, 0x4b, 0x03, 0x04})}));
			mockFileSystem.Directory.CreateDirectory(baseDir);
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { ExportFirst = true, Filename = outputFileName }, mockFileSystem.FileSystem);
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()), Times.Exactly(1));
			_mockClient.Verify(x => x.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<DownloadTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void ExportFirstFalseSkipsExportAndCallsDownload(bool useBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectId = "testcrowdinproject";
			const string baseDir = "test";
			var branch = useBranch ? "some-branch" : null;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			// Set up the call to Download
			_mockClient.Setup(client => client.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(),
					It.Is<DownloadTranslationParameters>(p => p.Branch == branch)))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent(new byte[] { 0x50, 0x4b, 0x03, 0x04 }) }));
			mockFileSystem.Directory.CreateDirectory(baseDir);
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { ExportFirst = false, Filename = outputFileName }, mockFileSystem.FileSystem);
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()), Times.Never);
			_mockClient.Verify(x => x.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<DownloadTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(0, result);
		}

		[Fact]
		public async void ErrorsAreReported()
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectId = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			var options = new DownloadCommand.Options {ExportFirst = false, Filename = outputFileName};
			// Set up the call to Download
			_mockClient.Setup(client => client.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<DownloadTranslationParameters>()))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
			mockFileSystem.Directory.CreateDirectory(baseDir);
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, options, mockFileSystem.FileSystem);
			_mockClient.Verify(x => x.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<DownloadTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(1, result);
		}
	}
}
