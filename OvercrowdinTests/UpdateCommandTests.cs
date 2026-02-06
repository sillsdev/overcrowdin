using Microsoft.Extensions.Configuration;
using Overcrowdin;
using RichardSzalay.MockHttp;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace OvercrowdinTests
{
	public class UpdateCommandTests : CrowdinApiTestBase
	{
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
			MockAddFile(mockFileSystem, TestProjectId, inputFileName);
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
			const string outputFileName = "test.%locale%.txt";
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			dynamic configJson = SetUpConfig(inputFileName);
			configJson.branch = branch;
			configJson.files[0].translation = outputFileName;

			using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString()));
			var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

			// Set up only the expected call to UpdateFile (any calls without the expected file params will return null)
			MockPrepareToAddFilesWithBranch(makeBranch, TestProjectId, TestProjectName, branch);
			MockAddFile(mockFileSystem, TestProjectId, inputFileName).WithPartialContent($"\"exportPattern\":\"{outputFileName}\"");
			var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Fact]
		public async Task UpdateCommandWithConfigFileMatchingNoFiles()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "fakecrowdinapikey");
			dynamic configJson = SetUpConfig("no-existe.txt");
			using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString()));
			var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
			//SUT
			var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Fact]
		public async Task UpdateRemovesPreviouslyUploadedEmptyXml()
		{
			var mockFileSystem = new MockFileSystem();
			const string xmlFileName = "filterFail.xml";
			mockFileSystem.File.WriteAllText(xmlFileName, XmlFilterTests.XmlOpenTag + XmlFilterTests.XmlCloseTag);
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "fakecrowdinapikey");
			dynamic configJson = SetUpConfig(xmlFileName);
			configJson.files[0].translatable_elements = new JArray { XmlFilterTests.XpathToTranslatableElements };

			using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString()));
			var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {TestProjectId},'identifier': '{TestProjectName}'}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=500&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {TestProjectId},'identifier': '{TestProjectName}'}}}}]}}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{TestProjectId}/files?limit=500&offset=0&recursion=1").Respond(
				"application/json", "{'data':[{'data': {'id': 123, 'name': 'filterFail.xml', 'path': 'filterFail.xml', 'branchId': null, 'directoryId': 0}}]}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{TestProjectId}/directories?limit=500&offset=0&recursion=1").Respond(
				"application/json", "{'data':[{'data': {}}]}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/storages?limit=500&offset=0").Respond(
				"application/json", "{'data':[]}");
			_mockHttpClient.Expect(HttpMethod.Delete, $"https://api.crowdin.com/api/v2/projects/{TestProjectId}/files/123")
				.Respond(HttpStatusCode.NoContent, "application/json", "{}");

			var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem, MockApiFactory);

			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}
	}
}
