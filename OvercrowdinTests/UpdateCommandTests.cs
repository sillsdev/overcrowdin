using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class UpdateCommandTests : CrowdinApiTestBase
	{
		[Fact]
		public async void MissingApiKeyReturnsFailure()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.json";
			const string apiKeyEnvVar = "NOKEYEXISTS";
			const string projectId = "testcrowdinproject";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Only setup the expected call to AddFile (any calls without the expected file params will return null)
			var gate = new AutoResetEvent(false);
			var result = await UpdateCommand.UpdateFilesInCrowdin(_mockConfig.Object, new UpdateCommand.Options { Files = new[] { inputFileName } },
				gate, mockFileSystem.FileSystem);
			gate.WaitOne();
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Fact]
		public async void UpdateCommandWithCommandLine()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.json";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Only setup the expected call to AddFile (any calls without the expected file params will return null)
			_mockClient.Setup(x => x.UpdateFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<UpdateFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
			var gate = new AutoResetEvent(false);
			var result = await UpdateCommand.UpdateFilesInCrowdin(_mockConfig.Object, new UpdateCommand.Options { Files = new[] { inputFileName } },
				gate, mockFileSystem);
			gate.WaitOne();
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void UpdateCommandWithConfigFile()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.json";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			mockFileSystem.File.WriteAllText("test.json", "{ mock: config }");
			dynamic configJson = new JObject();

			configJson.project_id = projectId;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = "test.json";
			var files = new JArray();
			files.Add(file);
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				// Only setup the expected call to AddFile (any calls without the expected file params will return null)
				_mockClient.Setup(x => x.UpdateFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<UpdateFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
				var gate = new AutoResetEvent(false);
				var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), gate, mockFileSystem);
				gate.WaitOne();
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}
	}
}
