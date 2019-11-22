using System;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Threading;
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
			var gate = new AutoResetEvent(false);
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { ExportFirst = true, Filename = "done.zip" },
				gate, mockFileSystem.FileSystem);
			gate.WaitOne();
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Fact]
		public async void ExportFirstTrueCallsExportAndDownload()
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
			// Setup the calls to Export and Download
			_mockClient.Setup(client => client.ExportTranslation(projectId,It.IsAny<ProjectCredentials>(),
				It.IsAny<ExportTranslationParameters>())).Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
			_mockClient.Setup(client => client.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(),
				It.IsAny<DownloadTranslationParameters>()))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent(new byte [] { 0x50, 0x4b, 0x03, 0x04 })}));
			mockFileSystem.Directory.CreateDirectory(baseDir);
			var gate = new AutoResetEvent(false);
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { ExportFirst = true, Filename = outputFileName },
				gate, mockFileSystem.FileSystem);
			gate.WaitOne();
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()));
			_mockClient.Verify(x => x.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<DownloadTranslationParameters>()));
			Assert.Equal(0, result);
		}

		[Fact]
		public async void ExportFirstFalseSkipsExportAndCallsDownload()
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
			// Setup the call to Download
			_mockClient.Setup(client => client.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(),
					It.IsAny<DownloadTranslationParameters>()))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent(new byte[] { 0x50, 0x4b, 0x03, 0x04 }) }));
			mockFileSystem.Directory.CreateDirectory(baseDir);
			var gate = new AutoResetEvent(false);
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { ExportFirst = false, Filename = outputFileName },
				gate, mockFileSystem.FileSystem);
			gate.WaitOne();
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()), Times.Never);
			_mockClient.Verify(x => x.DownloadTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<DownloadTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(0, result);
		}
	}
}
