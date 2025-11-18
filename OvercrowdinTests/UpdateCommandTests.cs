using Microsoft.Extensions.Configuration;
using Overcrowdin;
using RichardSzalay.MockHttp;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
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
	}
}