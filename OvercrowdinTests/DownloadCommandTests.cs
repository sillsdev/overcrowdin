using System;
using System.IO.Abstractions.TestingHelpers;
using System.Net.Http;
using System.Threading.Tasks;
using Overcrowdin;
using RichardSzalay.MockHttp;
using Xunit;

namespace OvercrowdinTests
{
	public class DownloadCommandTests : CrowdinApiTestBase
	{
		private const int _testProjId = 44444;

		private void MockPrepareToDownload(int projectId, string projectName, bool useBranch, string branch)
		{
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=500&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}','targetLanguages':[{{'id':'fr','name':'French'}}]}}}}]}}");
			if (useBranch)
			{
				_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/branches?limit=500&offset=0").Respond(
					"application/json", $"{{'data':[{{'name':'{branch}', 'id':1}}]}}");
			}
		}

		[Fact]
		public async Task MissingApiKeyReturnsFailure()
		{
			var mockFileSystem = new MockFileSystem();
			const string apiKeyEnvVar = "NOKEYEXISTS";
			const string projectId = "testcrowdinproject";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(".");
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { Filename = "done.zip" }, mockFileSystem.FileSystem, MockApiFactory);
			Assert.Equal(1, result);
		}

		[Fact]
		public async Task MissingBranchReturnsFailure()
		{
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectName = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			const string branch = "branch-name";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			// Set up the calls to check the project and branch
			MockPrepareToDownload(_testProjId, projectName, true, $"unexpected-{branch}");
			var exception = await Assert.ThrowsAsync<Exception>(() => DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { Filename = outputFileName },
				new MockFileSystem().FileSystem, MockApiFactory, new MockHttpClientFactory(_mockHttpClient)));
			Assert.Contains(branch, exception.Message);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task ExportFirstTrueCallsExportAndDownload(bool useBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectName = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			var branch = useBranch ? "some-branch" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			// Set up the calls to Export and Download
			mockFileSystem.Directory.CreateDirectory(baseDir);
			MockPrepareToDownload(_testProjId, projectName, useBranch, branch);
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects/44444/translations/builds?limit=500&offset=0")
				.Respond("application/json", $"{{'data':[]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects/44444/languages/progress?limit=500&offset=0").Respond("application/json",
				"{'data':[{'data': {'languageId': 'fr', 'translationProgress': 81, 'approvalProgress': 18 } }]}");
			_mockHttpClient.Expect(HttpMethod.Post, "https://api.crowdin.com/api/v2/projects/44444/translations/builds").Respond("application/json", "{}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects/44444/translations/builds/0/download").Respond("application/json", "{'url':'https://fakeurl.com'}");

			_mockHttpClient.Expect("https://fakeurl.com").Respond("application/octet-stream", "junk");
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { Filename = outputFileName }, mockFileSystem.FileSystem, MockApiFactory, new MockHttpClientFactory(_mockHttpClient));
			_mockHttpClient.VerifyNoOutstandingExpectation();
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task ExportWithRecentBuildSkipsExportAndCallsDownload(bool useBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectName = "testcrowdinproject";
			const string baseDir = "test";
			var branch = useBranch ? "some-branch" : null;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			// Set up the call to Download
			mockFileSystem.Directory.CreateDirectory(baseDir);
			MockPrepareToDownload(_testProjId, projectName, useBranch, branch);
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects/44444/translations/builds?limit=500&offset=0")
				.Respond("application/json", $"{{'data':[{{'data': {{'projectId': {_testProjId},'attributes':{{'branchId':{(useBranch ? "1" : "null")}, 'targetLanguageIds':['fr']}}, 'status': 'finished', 'finishedAt':'{DateTime.Now}'}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects/44444/languages/progress?limit=500&offset=0").Respond("application/json",
				"{'data':[{'data': {'languageId': 'fr', 'translationProgress': 81, 'approvalProgress': 18 } }]}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects/44444/translations/builds/0/download").Respond("application/json", "{'url':'https://fakeurl.com'}");

			_mockHttpClient.Expect("https://fakeurl.com").Respond("application/octet-stream", "junk");
			var result = await DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, new DownloadCommand.Options { Filename = outputFileName }, mockFileSystem.FileSystem, MockApiFactory, new MockHttpClientFactory(_mockHttpClient));
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Fact]
		public async Task UnexpectedErrorsAreReported()
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "test.zip";
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectName = "testcrowdinproject";
			const int projectId = _testProjId;
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			var options = new DownloadCommand.Options { Filename = outputFileName };
			mockFileSystem.Directory.CreateDirectory(baseDir);
			// Set up the call to get the project but not the builds; this will result in an arbitrary exception 
			MockPrepareToDownload(projectId, projectName, false, null);
			var exception = await Assert.ThrowsAsync<MockHttpMatchException>(() => DownloadCommand.DownloadFromCrowdin(_mockConfig.Object, options, mockFileSystem.FileSystem, MockApiFactory));
			Assert.Contains("Failed to match a mocked request for GET https://api.crowdin.com/api/v2/projects/44444/translations/builds?limit=500&offset=0", exception.Message);
		}
	}

	public class MockHttpClientFactory : IHttpClientFactory
	{
		private readonly MockHttpMessageHandler _mock;
		public MockHttpClientFactory(MockHttpMessageHandler mockHttpMessageHandler)
		{
			_mock = mockHttpMessageHandler;
		}

		public HttpClient GetClient()
		{
			return _mock.ToHttpClient();
		}
	}
}