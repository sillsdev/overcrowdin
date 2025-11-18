using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
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
		public async Task AddCommandWithConfigFileWithXmlOptions()
		{
			var mockFileSystem = new MockFileSystem();
			const int projectId = 75309;
			const string fileName0 = "testA.xml";
			const string fileName1 = "testB.xml";
			const string fileName2 = "testC.xml";
			const string fileContents0 = "<string txt='something'/>";
			const string fileContents1 = "<cheese><wheel>swiss</wheel></cheese>";
			const string fileContents2 = "<what>ever</what>";
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
			files.Add(new JObject());
			files[2].source = fileName2;
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
			MockAddFile(mockFileSystem, projectId, fileName2, fileContents2);
			// SUT
			using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString()));
			var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
			var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), mockFileSystem, MockApiFactory);
			_mockHttpClient.VerifyNoOutstandingExpectation();
			Assert.Equal(0, result);
		}

		/// <summary>
		/// Integration test that two files in the same folder can have distinct translation export locations
		/// </summary>
		[Fact]
		public async Task AddCommandWithConfigFileWithExportOptions()
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName0 = "dir/strings-en.ext";
			const string fileName1 = "dir/otherStrings.ext";
			const string translationLoc0 = "/%locale%/strings-%locale%.xml";
			const string translationLoc1 = "/%locale%/%original_path%/%file_name%.%locale%.%file_extension%";
			dynamic configJson = SetUpConfig(fileName0);
			var files = configJson.files;
			files[0].translation = translationLoc0;
			files.Add(new JObject());
			files[1].source = fileName1;
			files[1].translation = translationLoc1;
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "key-exists");
			MockPrepareToAddFile(TestProjectId, TestProjectName);
			// Only the first translation location gets associated with the directory, since the files are both in the same directory. Each gets associated with its file.
			ExpectDirectory(TestProjectId, "dir", 2).WithPartialContent($"\"exportPattern\":\"{translationLoc0}\"");
			MockAddFile(mockFileSystem, TestProjectId, fileName0).WithPartialContent($"\"exportOptions\":{{\"exportPattern\":\"{translationLoc0}\"}}");
			MockAddFile(mockFileSystem, TestProjectId, fileName1).WithPartialContent($"\"exportOptions\":{{\"exportPattern\":\"{translationLoc1}\"}}");
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

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task AddCommandCreatesFolderStructure(bool makeBranch)
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName1 = "Src/Common/Program/Properties/Resources.resx";
			const string inputFileName2 = "Src/Common/Library/Properties/Resources.resx";
			var branch = makeBranch ? "branchName" : null;
			Environment.SetEnvironmentVariable(TestApiKeyEnv, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(TestApiKeyEnv);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(TestProjectName);
			_mockConfig.Setup(config => config["branch"]).Returns(branch);
			MockPrepareToAddFilesWithBranch(makeBranch, TestProjectId, TestProjectName, branch);
			ExpectDirectory(TestProjectId, "Src", 1);
			ExpectDirectory(TestProjectId, "Common", 2, 1);
			ExpectDirectory(TestProjectId, "Program", 3, 2);
			ExpectDirectory(TestProjectId, "Properties", 4, 3);
			MockAddFile(mockFileSystem, TestProjectId, inputFileName1);
			ExpectDirectory(TestProjectId, "Library", 5, 2);
			ExpectDirectory(TestProjectId, "Properties", 6, 5);
			MockAddFile(mockFileSystem, TestProjectId, inputFileName2);
			//// SUT
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object,
				new AddCommand.Options { Files = new[] { inputFileName1, inputFileName2 } },
				mockFileSystem, MockApiFactory);
			_mockHttpClient.VerifyNoOutstandingExpectation();
			Assert.Equal(0, result);
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