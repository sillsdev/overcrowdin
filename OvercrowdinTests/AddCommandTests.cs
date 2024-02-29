using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using RichardSzalay.MockHttp;
using Xunit;

namespace OvercrowdinTests
{
	public class AddCommandTests : CrowdinApiTestBase
	{

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
		public async void AddCommandWithCommandLine()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			const int projectId = 44444;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			MockPrepareToAddFile(projectId, projectName);
			MockAddFile(projectId, inputFileName, mockFileSystem);
			// Set up only the expected call to AddFile (any calls without the expected file params will return null)
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object, new AddCommand.Options { Files = new[] { inputFileName } }, mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Fact]
		public async void AddCommandWithConfigFile()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			const int projectId = 44444;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			mockFileSystem.File.WriteAllText(inputFileName, "mock contents");
			dynamic configJson = new JObject();

			configJson.project_identifier = projectName;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray { file };
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				MockPrepareToAddFile(projectId, projectName);
				MockAddFile(projectId, inputFileName, mockFileSystem);
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
				_mockHttpClient.VerifyNoOutstandingExpectation();
			}
		}

		[Fact]
		public async void AddCommandWithConfigFileMatchingNoFiles()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "no-existe.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			const int projectId = 444444;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");

			dynamic configJson = new JObject();
			configJson.project_identifier = projectName;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray { file };
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				MockPrepareToAddFile(projectId, projectName);
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
			}
		}


		// ENHANCE (Hasso) 2020.01: verify that the folder is created *before* the file is added, and the branch before the folder
		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void AddCommandCreatesFolders(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "relative/path/test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			const int projectId = 369681;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			MockPrepareToAddFilesWithBranch(makeBranch, projectId, projectName, branch);
			//// SUT
			_mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/directories").WithPartialContent("\"name\":\"relative\"").Respond("application/json", "{}");
			_mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/directories").WithPartialContent("\"name\":\"path\"").Respond("application/json", "{}");
			MockAddFile(projectId, inputFileName, mockFileSystem);
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options { Files = new[] { inputFileName } },
				mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void AddCommandWithCommandLineToBranch(bool makeBranch)
		{
			const string inputFileName = "test.txt";
			var mockFileSystem = new MockFileSystem();
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			const int projectId = 55555;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			MockPrepareToAddFilesWithBranch(makeBranch, projectId, projectName, branch);
			MockAddFile(projectId, inputFileName, mockFileSystem);
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options { Branch = branch, Files = new[] { inputFileName } }, mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void AddCommandWithConfigFileToBranch(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectName = "testcrowdinproject";
			const int projectId = 234234;
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			dynamic configJson = new JObject();

			configJson.project_identifier = projectName;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			if (makeBranch)
			{
				configJson.branch = branch;
			}
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray { file };
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				MockPrepareToAddFilesWithBranch(makeBranch, projectId, projectName, branch);
				MockAddFile(projectId, inputFileName, mockFileSystem);
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
				_mockHttpClient.VerifyNoOutstandingExpectation();
			}
		}
	}
}