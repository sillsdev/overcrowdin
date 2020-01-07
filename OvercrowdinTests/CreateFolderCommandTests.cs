using System;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Threading;
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
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
			var gate = new AutoResetEvent(false);
			var opts = new GlobalOptions();
			var result = await CreateFolderCommand.CreateFolderInCrowdin(_mockConfig.Object, opts, newFolderName, gate, mockFileSystem);
			gate.WaitOne();
			_mockClient.Verify();
			Assert.Equal(0, result);
		}
	}
}
