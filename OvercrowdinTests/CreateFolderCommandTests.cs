using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Moq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class CreateFolderCommandTests : CrowdinApiTestBase
	{
		[Fact]
		public async void CreateFolders_NoFolders_Succeeds()
		{
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, new GlobalOptions(),
				new HashSet<string>(), new MockFileSystem());
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolder()
		{
			var mockFileSystem = new MockFileSystem();
			const string newFolderName = "testDir";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => newFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new GlobalOptions();
			var result = await CreateFolderCommand.CreateFolderInCrowdin(_mockConfig.Object, opts, newFolderName, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreatePreexistingFolderSucceeds()
		{
			var mockFileSystem = new MockFileSystem();
			const string newFolderName = "testDir";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			var mockResult = new HttpResponseMessage(HttpStatusCode.InternalServerError)
			{
				Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<error>
  <code>{(int) CrowdinErrorCodes.DirectoryAlreadyExists}</code>
  <message>Directory with such name already exists</message>
</error>")
			};
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => newFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(mockResult))
				.Verifiable();
			var opts = new GlobalOptions();
			var result = await CreateFolderCommand.CreateFolderInCrowdin(_mockConfig.Object, opts, newFolderName, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderWithBadCharactersFails()
		{
			var mockFileSystem = new MockFileSystem();
			const string newFolderName = "testDir";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			var mockResult = new HttpResponseMessage(HttpStatusCode.InternalServerError)
			{
				Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<error>
  <code>{(int) CrowdinErrorCodes.DirectoryNameHasInvalidCharacters}</code>
  <message>Directory with such name already exists</message>
</error>")
			};
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => newFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(mockResult))
				.Verifiable();
			var opts = new GlobalOptions();
			var result = await CreateFolderCommand.CreateFolderInCrowdin(_mockConfig.Object, opts, newFolderName, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(1, result);
		}
	}
}
