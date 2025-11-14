using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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

		/// <returns>The MockedRequest expectation to add the file to the project</returns>
		private MockedRequest MockAddFile(MockFileSystem mockFileSystem, int projectId, string inputFileName, string fileContent = "irrelevant")
		{
			mockFileSystem.AddFile(inputFileName, new MockFileData(fileContent));
			_mockHttpClient.Expect(HttpMethod.Post, "https://api.crowdin.com/api/v2/storages")
				.WithHeaders("Crowdin-API-FileName", $"{Path.GetFileName(inputFileName)}").Respond("application/json", "{}");
			var addFileRequest = _mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/files").Respond("application/json", "{}");
			_mockHttpClient.Expect(HttpMethod.Delete, "https://api.crowdin.com/api/v2/storages/0").Respond(HttpStatusCode.NoContent, "application/json", "{}");
			return addFileRequest;
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
		public async Task AddCommandWithCommandLine()
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
			MockAddFile(mockFileSystem, projectId, inputFileName);
			// Set up only the expected call to AddFile (any calls without the expected file params will return null)
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object, new AddCommand.Options { Files = new[] { inputFileName } }, mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
			_mockHttpClient.VerifyNoOutstandingExpectation();
		}

		[Fact]
		public async Task AddCommandWithConfigFile()
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
				MockAddFile(mockFileSystem, projectId, inputFileName);
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
				_mockHttpClient.VerifyNoOutstandingExpectation();
			}
		}

		[Fact]
		public async Task AddCommandWithConfigFileMatchingNoFiles()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "no-existe.txt";
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "fakecrowdinapikey");

			dynamic configJson = new JObject();
			configJson.project_identifier = TestProjectName;
			configJson.api_key_env = TestApiKeyEnv;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray { file };
			configJson.files = files;

			using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString()));
			var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
			MockPrepareToAddFile(TestProjectId, TestProjectName);
			var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
		}


		/// <summary>
		/// Integration test that two XML files in the same folder can have distinct XML options.
		/// </summary>
		[Fact]
		public async Task AddCommandWithConfigFileWithXMLOptions()
		{
			var mockFileSystem = new MockFileSystem();
			const int projectId = 75309;
			const string fileName0 = "testA.xml";
			const string fileName1 = "testB.xml";
			const string fileContents0 = "<string txt='something'/>";
			const string fileContents1 = "<cheese><wheel>swiss</wheel></cheese>";
			const string trElt0 = "//string[@txt]";
			const string trElt1A = "/cheese/wheel";
			const string trElt1B = "/round[@round]";
			dynamic configJson = SetUpConfig(fileName0);
			var files = configJson.files;
			files[0].translate_content = 0;
			files[0].translate_attributes = 0;
			files[0].content_segmentation = 0;
			files[0].translatable_elements = new JArray { trElt0 };
			files.Add(new JObject());
			files[1].source = fileName1;
			files[1].translate_content = 1;
			files[1].translate_attributes = 1;
			files[1].content_segmentation = 1;
			files[1].translatable_elements = new JArray { trElt1A, trElt1B };
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "key-exists");
			MockPrepareToAddFile(projectId, TestProjectName);
			MockAddFile(mockFileSystem, projectId, fileName0, fileContents0).WithPartialContent($"\"translatableElements\":[\"{trElt0}\"]")
				.WithPartialContent($"\"translateContent\":false")
				.WithPartialContent($"\"translateAttributes\":false")
				.WithPartialContent($"\"contentSegmentation\":false");
			MockAddFile(mockFileSystem, projectId, fileName1, fileContents1).WithPartialContent($"\"translatableElements\":[\"{trElt1A}\",\"{trElt1B}\"]")
				.WithPartialContent($"\"translateContent\":true")
				.WithPartialContent($"\"translateAttributes\":true")
				.WithPartialContent($"\"contentSegmentation\":true");
			// SUT
			using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString()));
			var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
			var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
			_mockHttpClient.VerifyNoOutstandingExpectation();
			Assert.Equal(0, result);
		}


		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task AddCommandCreatesFolders(bool makeBranch)
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
			ExpectDirectory(projectId, "relative");
			ExpectDirectory(projectId, "path");
			MockAddFile(mockFileSystem, projectId, inputFileName);
			//// SUT
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options { Files = new[] { inputFileName } },
				mockFileSystem, MockApiFactory);
			_mockHttpClient.VerifyNoOutstandingExpectation();
			Assert.Equal(0, result);
		}

		[Fact]
		public async Task AddCommandCreatesFolderStructure()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName1 = "Src/Common/Program/Properties/Resources.resx";
			const string inputFileName2 = "Src/Common/Library/Properties/Resources.resx";
			const string projectName = "testcrowdinproject";
			const int projectId = 369681;
			const string branch = "branchName";
			const string apiKeyEnvVar = "KEYEXISTS";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			MockPrepareToAddFilesWithBranch(true, projectId, projectName, branch);
			ExpectDirectory(projectId, "Src", 1);
			ExpectDirectory(projectId, "Common", 2, 1);
			ExpectDirectory(projectId, "Program", 3, 2);
			ExpectDirectory(projectId, "Properties", 4, 3);
			MockAddFile(mockFileSystem, projectId, inputFileName1);
			ExpectDirectory(projectId, "Library", 5, 2);
			ExpectDirectory(projectId, "Properties", 6, 5);
			MockAddFile(mockFileSystem, projectId, inputFileName2);
			//// SUT
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options { Files = new[] { inputFileName1, inputFileName2 } },
				mockFileSystem, MockApiFactory);
			_mockHttpClient.VerifyNoOutstandingExpectation();
			Assert.Equal(0, result);
		}

		private MockedRequest ExpectDirectory(int projectId, string name, int? id = null, int? parent = null)
		{
			var request = _mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/directories")
				.WithPartialContent($"\"name\":\"{name}\"");
			if(parent != null)
			{
				request.WithPartialContent($"\"directoryId\":{parent}");
			}
			return request.Respond("application/json", $$$"""
				{"data": {
					"name": "{{{name}}}",
					"id": {{{id?.ToString() ?? "null"}}},
					"directoryId": {{{parent?.ToString() ?? "null"}}},
				}}
				""");
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task AddCommandWithCommandLineToBranch(bool makeBranch)
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
			MockAddFile(mockFileSystem, projectId, inputFileName);
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options { Branch = branch, Files = new[] { inputFileName } }, mockFileSystem, MockApiFactory);
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task AddCommandWithConfigFileToBranch(bool makeBranch)
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
				MockAddFile(mockFileSystem, projectId, inputFileName);
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
				Assert.Equal(0, result);
				_mockHttpClient.VerifyNoOutstandingExpectation();
			}
		}
	}
}