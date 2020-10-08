using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
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
		// REVIEW (Hasso) 2020.01: should we be checking the same for AddFiles and other methods? Should this check be centralized?
		public async void MissingApiKeyReturnsFailure()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "NOKEYEXISTS";
			const string projectId = "testcrowdinproject";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// No need to set up calls. If the API key is missing, the API call should not be attempted.
			var result = await UpdateCommand.UpdateFilesInCrowdin(_mockConfig.Object, new UpdateCommand.Options { Files = new[] { inputFileName } }, mockFileSystem.FileSystem);
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void UpdateCommandWithCommandLine(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Set up only the expected call to UpdateFile (any calls without the expected file params will return null)
			_mockClient.Setup(x => x.UpdateFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<UpdateFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var result = await UpdateCommand.UpdateFilesInCrowdin(_mockConfig.Object,
				new UpdateCommand.Options {Branch = branch, Files = new[] {inputFileName}}, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void UpdateCommandWithConfigFile(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : string.Empty;
			mockFileSystem.File.WriteAllText(inputFileName, "mock contents");
			dynamic configJson = new JObject();

			configJson.project_id = projectId;
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
				_mockClient.Setup(x => x.UpdateFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<UpdateFileParameters>(fp => fp.Branch == branch && fp.Files.ContainsKey(inputFileName))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable();
				var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem);
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}

		[Fact]
		public async void UpdateCommandWithConfigFileMatchingNoFiles()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "no-existe.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");

			dynamic configJson = new JObject();
			configJson.project_id = projectId;
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
				var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem);
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}

		[Fact]
		public async void UpdateCommandBatchesManyFiles()
		{
			const int secondBatchSize = 2;
			const int fileCount = CommandUtilities.BatchSize + secondBatchSize;
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			for (var i = 0; i < fileCount; i++)
			{
				mockFileSystem.File.WriteAllText($"file{i}.txt", "mock contents");
			}

			dynamic configJson = new JObject();
			configJson.project_id = projectId;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = "*.txt";
			file.translation = "/l10n/%two_letters_code%/%original_file_name%";
			var files = new JArray { file };
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				// Set up only the expected calls (any calls without the expected file params will return null)
				_mockClient.Setup(x => x.UpdateFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<UpdateFileParameters>(fp => fp.Files.Count == CommandUtilities.BatchSize && fp.ExportPatterns.Count == CommandUtilities.BatchSize)))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable("first (full) batch");
				_mockClient.Setup(x => x.UpdateFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<UpdateFileParameters>(fp => fp.Files.Count == secondBatchSize && fp.ExportPatterns.Count == secondBatchSize)))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable("second batch");
				var result = await UpdateCommand.UpdateFilesInCrowdin(configurationBuilder, new UpdateCommand.Options(), mockFileSystem);
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}
	}
}
