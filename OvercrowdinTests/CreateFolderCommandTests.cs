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
		private const string ApiKeyEnvVar = "KEYEXISTS";
		private const string ProjectId = "testcrowdinproject";
		private const string NewFolderName = "testDir";
		private const string Branch = "testBranch";

		[Fact]
		public async void CreateFolders_NoFolders_Succeeds()
		{
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, new CreateFolderOptions(),
				new HashSet<string>(), new MockFileSystem());
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolder()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions();
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreatePreexistingFolderSucceeds()
		{
			var mockFileSystem = new MockFileSystem();
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
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderWithBadCharactersFails()
		{
			var mockFileSystem = new MockFileSystem();
			const string badFolderName = ">bad*Dir?";
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			var mockResult = new HttpResponseMessage(HttpStatusCode.InternalServerError)
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
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void CreateFolderForBranch(bool isBranch)
		{
			var mockFileSystem = new MockFileSystem();
			var branchName = isBranch ? Branch : null;
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name) && VerifyBranchParams(fp, branchName))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions {Branch = branchName};
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderForBranchFromConfig()
		{
			var mockFileSystem = new MockFileSystem();
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			_mockConfig.Setup(config => config["branch"]).Returns(Branch);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name) && VerifyBranchParams(fp, Branch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions();
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void CreateFolderForBranchPrefersCLI()
		{
			var mockFileSystem = new MockFileSystem();
			const string cliBranch = "branchFromCLI";
			Environment.SetEnvironmentVariable(ApiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(ApiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(ProjectId);
			_mockConfig.Setup(config => config["branch"]).Returns(Branch);
			// Set up only the expected call to CreateFolder (any calls without the expected folder params will return null)
			_mockClient.Setup(x => x.CreateFolder(It.IsAny<string>(), It.IsAny<ProjectCredentials>(),
					It.Is<CreateFolderParameters>(fp => NewFolderName.Equals(fp.Name) && VerifyBranchParams(fp, cliBranch))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))
				.Verifiable();
			var opts = new CreateFolderOptions {Branch = cliBranch};
			var folders = new SortedSet<string> {NewFolderName};
			var result = await CreateFolderCommand.CreateFoldersInCrowdin(_mockConfig.Object, opts, folders, mockFileSystem);
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		private static bool VerifyBranchParams(CreateFolderParameters cfParams, string branch)
		{
			return string.IsNullOrEmpty(branch)
				? string.IsNullOrEmpty(cfParams.Branch) && (!cfParams.IsBranch.HasValue || !cfParams.IsBranch.Value)
				: branch.Equals(cfParams.Branch) && cfParams.IsBranch.HasValue && cfParams.IsBranch.Value;
		}

		private class CreateFolderOptions : GlobalOptions, IBranchOptions
		{
			public string Branch { get; set; }
		}
	}
}
