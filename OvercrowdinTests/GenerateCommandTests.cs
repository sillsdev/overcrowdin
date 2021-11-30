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
		[Fact]
		public async void InvalidProjectIdFails()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable("KEYEXISTS", "fakecrowdinapikey");
			_mockClient.Setup(x => x.GetProjectInfo(It.IsAny<string>(), It.IsAny<ProjectCredentials>()))
				.Throws(new CrowdinException("No user", 404));
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options {BasePath = ".", Key = "KEYEXISTS"}, mockFileSystem);
			Assert.Equal(1, result);
		}

		[Fact]
		public async void NoAPIKeyFails()
		{
			var mockFileSystem = new MockFileSystem();
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options {BasePath = ".", Key = "FAKEAPIKEYENVVARFORTESET"}, mockFileSystem);
			Assert.Equal(1, result);
		}

		[Fact]
		public async void ProjectWithFilesWritesValidConfig()
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "overcrowdin.json";
			const string projectIdentifier = "someprojectid";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string basePath = "testPath";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			ProjectInfo projectInfo;
			var serializer = new XmlSerializer(typeof(ProjectInfo));
			using (var reader = new StringReader(TestResources.CrowdinProjInfoResponseWithFiles))
			{
				projectInfo = (ProjectInfo) serializer.Deserialize(reader);
			}

			Assert.NotNull(projectInfo); // verify that the test data is good
			_mockClient.Setup(x => x.GetProjectInfo(It.IsAny<string>(), It.IsAny<ProjectCredentials>()))
				.Returns(Task.FromResult(projectInfo));
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options
					{BasePath = basePath, Key = apiKeyEnvVar, OutputFile = outputFileName, Identifier = projectIdentifier}, mockFileSystem);
			Assert.Equal(0, result);
			var mockOutputFile = mockFileSystem.GetFile(outputFileName).TextContents;
			var contents = JObject.Parse(mockOutputFile);
			Assert.True(contents.ContainsKey("project_identifier"));
			Assert.True(contents.ContainsKey("api_key_env"));
			Assert.True(contents.ContainsKey("base_path"));
			Assert.Equal(contents["project_identifier"], projectIdentifier);
			Assert.Equal(contents["api_key_env"], apiKeyEnvVar);
			Assert.Equal(contents["base_path"], basePath);
		}

		[Fact]
		public async void ProjectWithNoFilesWritesValidConfig()
		{
			var mockFileSystem = new MockFileSystem();
			const string outputFileName = "overcrowdin.json";
			const string projectIdentifier = "someprojectid";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string basePath = "testPath";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			ProjectInfo projectInfo;
			var serializer = new XmlSerializer(typeof(ProjectInfo));
			using (var reader = new StringReader(TestResources.CrowdinProjInfoResponseNoFiles))
			{
				projectInfo = (ProjectInfo) serializer.Deserialize(reader);
			}

			Assert.NotNull(projectInfo); // verify that the test data is good
			_mockClient.Setup(x => x.GetProjectInfo(It.IsAny<string>(), It.IsAny<ProjectCredentials>()))
				.Returns(Task.FromResult(projectInfo));
			var result = await GenerateCommand.GenerateConfigFromCrowdin(_mockConfig.Object,
				new GenerateCommand.Options
					{BasePath = basePath, Key = apiKeyEnvVar, OutputFile = outputFileName, Identifier = projectIdentifier}, mockFileSystem);
			Assert.Equal(0, result);
			var mockOutputFile = mockFileSystem.GetFile(outputFileName).TextContents;
			var contents = JObject.Parse(mockOutputFile);
			Assert.True(contents.ContainsKey("project_identifier"));
			Assert.True(contents.ContainsKey("api_key_env"));
			Assert.True(contents.ContainsKey("base_path"));
			Assert.Equal(contents["project_identifier"], projectIdentifier);
			Assert.Equal(contents["api_key_env"], apiKeyEnvVar);
			Assert.Equal(contents["base_path"], basePath);
		}
	}
}