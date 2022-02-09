using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Moq;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class GenerateCommandTests : CrowdinApiTestBase
	{
		private const string OutputFileName = "overcrowdin.json";
		private const string ProjectIdentifier = "someprojectid";
		private const string APIKeyEnvVar = "KEYEXISTS";
		private const string UserEnvVar = "USEREXISTS";
		private const string BasePath = "testPath";

		[Fact]
		public async void InvalidProjectIdFails()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(APIKeyEnvVar, "fakecrowdinapikey");
			Environment.SetEnvironmentVariable(UserEnvVar, "fakecrowdinuser");
			_mockClient.Setup(x => x.GetProjectInfo(It.IsAny<string>(), It.IsAny<ProjectCredentials>()))
				.Throws(new CrowdinException("No user", 404));
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options {BasePath = ".", Key = APIKeyEnvVar, User = UserEnvVar}, mockFileSystem);
			Assert.Equal(1, result);
		}

		[Fact]
		public async void NoAPIKeyFails()
		{
			var mockFileSystem = new MockFileSystem();
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options {BasePath = ".", Key = "FAKEAPIKEYENVVARFORTESET", User = UserEnvVar}, mockFileSystem);
			Assert.Equal(1, result);
		}

		[Fact]
		public async void NoUsernameFails()
		{
			var mockFileSystem = new MockFileSystem();
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options {BasePath = ".", Key = APIKeyEnvVar, User = "FAKEUSERENVVARFORTESET"}, mockFileSystem);
			Assert.Equal(1, result);
		}

		[Fact]
		public async void ProjectWithFilesWritesValidConfig()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(APIKeyEnvVar, "fakecrowdinapikey");
			Environment.SetEnvironmentVariable(UserEnvVar, "fakecrowdinuser");
			ProjectInfo projectInfo;
			var serializer = new XmlSerializer(typeof(ProjectInfo));
			using (var reader = new StringReader(TestResources.CrowdinProjInfoResponseWithFiles))
			{
				projectInfo = (ProjectInfo) serializer.Deserialize(reader);
			}

			Assert.NotNull(projectInfo); // verify that the test data is good
			_mockClient.Setup(x => x.GetProjectInfo(It.IsAny<string>(), It.IsAny<AccountCredentials>()))
				.Returns(Task.FromResult(projectInfo));
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options
					{BasePath = BasePath, Key = APIKeyEnvVar, OutputFile = OutputFileName, Identifier = ProjectIdentifier, User = UserEnvVar}, mockFileSystem);
			Assert.Equal(0, result);
			var mockOutputFile = mockFileSystem.GetFile(OutputFileName).TextContents;
			var contents = JObject.Parse(mockOutputFile);
			Assert.True(contents.ContainsKey("project_identifier"));
			Assert.True(contents.ContainsKey("api_key_env"));
			Assert.True(contents.ContainsKey("base_path"));
			Assert.Equal(contents["project_identifier"], ProjectIdentifier);
			Assert.Equal(contents["api_key_env"], APIKeyEnvVar);
			Assert.Equal(contents["user_identifier_env"], UserEnvVar);
			Assert.Equal(contents["base_path"], BasePath);
		}

		[Fact]
		public async void ProjectWithNoFilesWritesValidConfig()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(APIKeyEnvVar, "fakecrowdinapikey");
			Environment.SetEnvironmentVariable(UserEnvVar, "fakecrowdinuser");
			ProjectInfo projectInfo;
			var serializer = new XmlSerializer(typeof(ProjectInfo));
			using (var reader = new StringReader(TestResources.CrowdinProjInfoResponseNoFiles))
			{
				projectInfo = (ProjectInfo) serializer.Deserialize(reader);
			}

			Assert.NotNull(projectInfo); // verify that the test data is good
			_mockClient.Setup(x => x.GetProjectInfo(It.IsAny<string>(), It.IsAny<AccountCredentials>()))
				.Returns(Task.FromResult(projectInfo));
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options
					{BasePath = BasePath, Key = APIKeyEnvVar, OutputFile = OutputFileName, Identifier = ProjectIdentifier, User = UserEnvVar}, mockFileSystem);
			Assert.Equal(0, result);
			var mockOutputFile = mockFileSystem.GetFile(OutputFileName).TextContents;
			var contents = JObject.Parse(mockOutputFile);
			Assert.True(contents.ContainsKey("project_identifier"));
			Assert.True(contents.ContainsKey("api_key_env"));
			Assert.True(contents.ContainsKey("base_path"));
			Assert.Equal(contents["project_identifier"], ProjectIdentifier);
			Assert.Equal(contents["api_key_env"], APIKeyEnvVar);
			Assert.Equal(contents["user_identifier_env"], UserEnvVar);
			Assert.Equal(contents["base_path"], BasePath);
		}
	}
}