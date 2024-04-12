using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using RichardSzalay.MockHttp;
using Xunit;

namespace OvercrowdinTests
{
	public class UpdateCommandTests : CrowdinApiTestBase
	{
		private const int TestProjectId = 44444;

		private void MockPrepareToAddFile(int projectId, string projectName)
		{
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}'}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=500&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}'}}}}]}}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/files?limit=500&offset=0&recursion=1").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/directories?limit=500&offset=0").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/storages?limit=500&offset=0").Respond(
				"application/json", $"{{'data':[]}}");
		}
		private void MockAddFile(int projectId, string inputFileName, MockFileSystem mockFileSystem)
		{
			mockFileSystem.AddFile(inputFileName, new MockFileData("irrelevant"));
			_mockHttpClient.Expect(HttpMethod.Post, "https://api.crowdin.com/api/v2/storages")
				.WithHeaders("Crowdin-API-FileName", $"{Path.GetFileName(inputFileName)}").Respond("application/json", "{}");
			_mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/files").Respond("application/json", "{}");
			_mockHttpClient.Expect(HttpMethod.Delete, "https://api.crowdin.com/api/v2/storages/0").Respond(HttpStatusCode.NoContent, "application/json", "{}");
		}

		private void MockPrepareToAddFilesWithBranch(bool makeBranch, int projectId, string projectName, string branch)
		{
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}'}}}}]}}");
			if (makeBranch)
			{
				_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/branches?limit=25&offset=0").Respond(
					"application/json", $"{{'data':[]}}");
			}

			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=500&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}'}}}}]}}");
			if (makeBranch)
			{
				_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/branches?limit=500&offset=0").Respond(
					"application/json", $"{{'data':[]}}");
				_mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/branches")
					.WithPartialContent($"\"name\":\"{branch}\"").Respond("application/json", "{}");
			}

			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/files?limit=500&offset=0&recursion=1").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/directories?limit=500&offset=0").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/storages?limit=500&offset=0").Respond(
				"application/json", $"{{'data':[]}}");
		}


		[Fact]
		public async Task MissingApiKeyReturnsFailure()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "NOKEYEXISTS";
			const string projectId = "testcrowdinproject";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// No need to set up calls. If the API key is missing, the API call should not be attempted.
			var result = await UpdateCommand.UpdateFilesInCrowdin(_mockConfig.Object, new UpdateCommand.Options { Files = new[] { inputFileName } }, mockFileSystem.FileSystem, MockApiFactory);
			Assert.Equal(1, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task UpdateCommandWithCommandLine(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			MockPrepareToAddFilesWithBranch(makeBranch, TestProjectId, projectName, branch);
			MockAddFile(TestProjectId, inputFileName, mockFileSystem);
			var result = await UpdateCommand.UpdateFilesInCrowdin(_mockConfig.Object,
				new UpdateCommand.Options { Branch = branch, Files = new[] { inputFileName } }, mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task UpdateCommandWithConfigFile(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			mockFileSystem.File.WriteAllText(inputFileName, "mock contents");
			dynamic configJson = new JObject();

			configJson.project_identifier = projectName;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.branch = branch;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray();
			files.Add(file);
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				// Set up only the expected call to UpdateFile (any calls without the expected file params will return null)
				MockPrepareToAddFilesWithBranch(makeBranch, TestProjectId, projectName, branch);
				MockAddFile(TestProjectId, inputFileName, mockFileSystem);
				var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
				_mockHttpClient.VerifyNoOutstandingExpectation();
			}
		}

		[Fact]
		public async Task UpdateCommandWithConfigFileMatchingNoFiles()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "no-existe.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");

			dynamic configJson = new JObject();
			configJson.project_identifier = projectName;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray();
			files.Add(file);
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
				_mockHttpClient.VerifyNoOutstandingExpectation();
			}
		}
	}
}