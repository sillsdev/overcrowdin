using Crowdin.Api;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using RichardSzalay.MockHttp;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;

namespace OvercrowdinTests
{
	/// <summary>
	/// Base class for tests that need to mock the CrowdinApi
	/// </summary>
	public class CrowdinApiTestBase
	{
		public const int TestProjectId = 44444;
		public const string TestProjectName = "testcrowdinproject";
		public const string TestApiKeyEnv = "KEYEXISTS";

		protected readonly Mock<IConfiguration> _mockConfig;
		protected readonly CrowdinApiClient _mockClient;
		protected readonly MockHttpMessageHandler _mockHttpClient;
		/*
		 *	HttpClient? httpClient = null,
			IJsonParser? jsonParser = null,
			IRateLimiter? rateLimiter = null,
			IRetryService? retryService = null
		 */
		public CrowdinApiTestBase()

		{
			_mockHttpClient = new MockHttpMessageHandler(BackendDefinitionBehavior.Always);
			_mockClient = new CrowdinApiClient(
				new CrowdinCredentials { AccessToken = "fakeyfakey" },
				_mockHttpClient.ToHttpClient());
			_mockConfig = new Mock<IConfiguration>();

		}

		public ICrowdinClientFactory MockApiFactory
		{
			get
			{
				var mockFact = new Mock<ICrowdinClientFactory>();
				mockFact.Setup(x => x.Create(
					It.IsAny<string>())).Returns(_mockClient);
				return mockFact.Object;
			}
		}

		public static MockFileSystem SetUpDirectoryStructure()
		{
			var fileSys = new MockFileSystem();
			fileSys.Directory.CreateDirectory("jane/doe");
			fileSys.Directory.CreateDirectory("john/doe");
			fileSys.Directory.CreateDirectory("john/quincy/adams");
			fileSys.Directory.CreateDirectory("john/quincy/doe");
			fileSys.File.WriteAllText("test.txt", "contents");
			fileSys.File.WriteAllText("jane/test.txt", "contents");
			fileSys.File.WriteAllText("jane/doe/test.txt", "contents");
			fileSys.File.WriteAllText("john/test.txt", "contents");
			fileSys.File.WriteAllText("john/doe/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/adams/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/adams/allonym.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/doe/test.txt", "contents");
			return fileSys;
		}

		public static JObject SetUpConfig(string fileSourceGlob, string basePath = ".")
		{
			dynamic configJson = new JObject();
			configJson.project_identifier = TestProjectName;
			configJson.api_key_env = TestApiKeyEnv;
			configJson.base_path = basePath;
			dynamic file = new JObject();
			file.source = fileSourceGlob;
			var files = new JArray { file };
			configJson.files = files;
			return configJson;
		}

		protected MockedRequest ExpectDirectory(int projectId, string name, int? id = null, int? parent = null)
		{
			var request = _mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/directories")
				.WithPartialContent($"\"name\":\"{name}\"");
			if (parent != null)
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

		protected void MockPrepareToAddFile(int projectId, string projectName)
		{
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}'}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/projects?limit=500&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {projectId},'identifier': '{projectName}'}}}}]}}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/files?limit=500&offset=0&recursion=1").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/directories?limit=500&offset=0&recursion=1").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/storages?limit=500&offset=0").Respond(
				"application/json", $"{{'data':[]}}");
		}

		/// <returns>The MockedRequest expectation to add the file to the project</returns>
		protected MockedRequest MockAddFile(MockFileSystem mockFileSystem, int projectId, string inputFileName, string fileContent = "irrelevant")
		{
			mockFileSystem.AddFile(inputFileName, new MockFileData(fileContent));
			_mockHttpClient.Expect(HttpMethod.Post, "https://api.crowdin.com/api/v2/storages")
				.WithHeaders("Crowdin-API-FileName", $"{Path.GetFileName(inputFileName)}").Respond("application/json", "{}");
			var addFileRequest = _mockHttpClient.Expect(HttpMethod.Post, $"https://api.crowdin.com/api/v2/projects/{projectId}/files").Respond("application/json", "{}");
			_mockHttpClient.Expect(HttpMethod.Delete, "https://api.crowdin.com/api/v2/storages/0").Respond(HttpStatusCode.NoContent, "application/json", "{}");
			return addFileRequest;
		}

		protected void MockPrepareToAddFilesWithBranch(bool makeBranch, int projectId, string projectName, string branch)
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
			_mockHttpClient.Expect($"https://api.crowdin.com/api/v2/projects/{projectId}/directories?limit=500&offset=0&recursion=1").Respond(
				"application/json", $"{{'data':[{{'data': {{}}}}]}}");
			_mockHttpClient.Expect("https://api.crowdin.com/api/v2/storages?limit=500&offset=0").Respond(
				"application/json", $"{{'data':[]}}");
		}
	}
}