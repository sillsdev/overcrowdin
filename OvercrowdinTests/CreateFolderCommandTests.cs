using System;
using System.Collections.Generic;
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
		private const string ApiKeyEnvVar = "KEYEXISTS";
		private const string ProjectId = "testcrowdinproject";
		private const string NewFolderName = "testDir";
		private const string Branch = "testBranch";

		[Fact]
		public async void CreateFolders_NoFolders_Succeeds()
		{
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, new CreateFolderOptions(),
				new HashSet<string>());
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolder()
		{
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions();
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreatePreexistingFolderSucceeds()
		{
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			var mockResult = new HttpResponseMessage(HttpStatusCode.InternalServerError)
			{
				Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<error>
  <code>{(int) CrowdinErrorCode.DirectoryAlreadyExists}</code>
  <message>Directory with such name already exists</message>
</error>")
			};
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(mockResult))
				.Verifiable();
			var opts = new CreateFolderOptions();
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderWithBadCharactersFails()
		{
			const string badFolderName = ">bad*Dir?";
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			var mockResult = new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<error>
  <code>{(int) CrowdinErrorCode.DirectoryNameHasInvalidCharacters}</code>
  <message>Directory with such name already exists</message>
</error>")
			};
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => badFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(mockResult))
				.Verifiable();
			var opts = new CreateFolderOptions();
			var folders = new SortedSet<string> {badFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders);
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void CreateFolderForBranch(bool makeBranch)
		{
			var branchName = makeBranch ? Branch : null;
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name) && fp.Branch == branchName && TestUtils.FalseOrUnset(fp.IsBranch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			if (makeBranch)
			{
				_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
						It.Is<CreateFolderParameters>(fp => branchName.Equals(fp.Name) && TestUtils.True(fp.IsBranch) && string.IsNullOrEmpty(fp.Branch))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
					.Verifiable();
			}
			var opts = new CreateFolderOptions {Branch = branchName};
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderForBranchFromConfig()
		{
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			_mockConfig.Setup(config => config["branch"]).Returns(Branch);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name) && Branch.Equals(fp.Branch) && TestUtils.FalseOrUnset(fp.IsBranch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => Branch.Equals(fp.Name) && TestUtils.True(fp.IsBranch) && string.IsNullOrEmpty(fp.Branch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions();
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderForBranchPrefersCLI()
		{
			const string cliBranch = "branchFromCLI";
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			_mockConfig.Setup(config => config["branch"]).Returns(Branch);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name) && cliBranch.Equals(fp.Branch) && TestUtils.FalseOrUnset(fp.IsBranch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => cliBranch.Equals(fp.Name) && TestUtils.True(fp.IsBranch) && string.IsNullOrEmpty(fp.Branch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions {Branch = cliBranch};
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		private class CreateFolderOptions : GlobalOptions, IBranchOptions
		{
			public string Branch { get; set; }
		}
	}
}
