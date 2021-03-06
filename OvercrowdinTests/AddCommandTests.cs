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
	public class AddCommandTests : CrowdinApiTestBase
	{
		[Fact]
		public async void AddCommandWithCommandLine()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Set up only the expected call to AddFile (any calls without the expected file params will return null)
			_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object, new AddCommand.Options { Files = new[] { inputFileName } }, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void AddCommandWithConfigFile()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			mockFileSystem.File.WriteAllText(inputFileName, "mock contents");
			dynamic configJson = new JObject();

			configJson.project_id = projectId;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray {file};
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				// Set up only the expected call to AddFile (any calls without the expected file params will return null)
				_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable();
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem);
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}

		[Fact]
		public async void AddCommandWithConfigFileMatchingNoFiles()
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
			var files = new JArray {file};
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem);
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}

		[Fact]
		public async void AddCommandBatchesManyFiles()
		{
			const int fileCount = CommandUtilities.BatchSize + 2;
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
			var files = new JArray {file};
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				// Set up only the expected calls to AddFile (any calls without the expected file params will return null)
				_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<AddFileParameters>(fp => fp.Files.Count == CommandUtilities.BatchSize && fp.ExportPatterns.Count == CommandUtilities.BatchSize)))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable("first (full) batch");
				_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<AddFileParameters>(fp => fp.Files.Count == 2 && fp.ExportPatterns.Count == 2)))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable("second batch");
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem);
				_mockClient.Verify();
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
			var pathParts = new[] {"relative", "path"};
			const string inputFileName = "relative/path/test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			// Set up only the expected calls (any unexpected calls will return null)
			_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName) && fp.Branch == branch)))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable("should have added file");
			_mockClient.Setup(x => x.CreateFolder(projectId, It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => fp.Name.StartsWith(pathParts[0]) && fp.Branch == branch && TestUtils.FalseOrUnset(fp.IsBranch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable("should have created folder");
			if (makeBranch)
			{
				_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<CreateFolderParameters>(fp => fp.Name == branch && fp.Branch == null && TestUtils.True(fp.IsBranch))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable("should have created branch");
			}
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object, new AddCommand.Options { Files = new[] { inputFileName } }, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void AddCommandWithCommandLineToBranch(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Set up only the expected call to AddFile (any calls without the expected file params will return null)
			_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName) && fp.Branch == branch)))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			if (makeBranch)
			{
				_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<CreateFolderParameters>(fp => fp.Name == branch && fp.Branch == null && TestUtils.True(fp.IsBranch))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable();
			}
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options {Branch = branch, Files = new[] {inputFileName}}, mockFileSystem);
			_mockClient.Verify();
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
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			var branch = makeBranch ? "branchName" : null;
			mockFileSystem.File.WriteAllText(inputFileName, "mock contents");
			dynamic configJson = new JObject();

			configJson.project_id = projectId;
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

			// Set up only the expected call to AddFile (any calls without the expected file params will return null)
			_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName) && fp.Branch == branch)))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			if (makeBranch)
			{
				_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<CreateFolderParameters>(fp => fp.Name == branch && fp.Branch == null && TestUtils.True(fp.IsBranch))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable();
			}

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem);
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}
	}
}
