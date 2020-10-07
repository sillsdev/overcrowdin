using System;
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
	public class ExportCommandTests : CrowdinApiTestBase
	{
		[Fact]
		public async void MissingApiKeyReturnsFailure()
		{
			const string apiKeyEnvVar = "NOKEYEXISTS";
			const string projectId = "testcrowdinproject";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(".");
			var result = await ExportCommand.ExportCrowdinTranslations(_mockConfig.Object, new ExportCommand.Options());
			_mockClient.Verify();
			Assert.Equal(1, result);
		}

		[Fact(Skip = "not implemented")]
		public void TestStuff()
		{
			// TODO (Hasso) 2020.10: sync, async, branches
			throw new NotImplementedException("sync, async, etc.");
		}

		[Fact]
		public async void ErrorsAreReported()
		{
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectId = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			var options = new ExportCommand.Options();
			// Set up the call to Export
			_mockClient.Setup(client => client.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
			var result = await ExportCommand.ExportCrowdinTranslations(_mockConfig.Object, options);
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(1, result);
		}

		[Theory]
		[InlineData(true, false)]
		[InlineData(false, true)]
		public void CloneOptions(bool async, bool verbose)
		{
			const string branch = "my-branch";
			var orig = new ExportCommand.Options
			{
				Asynchronous = async,
				Branch = branch,
				Verbose = verbose
			};
			var clone = orig.Clone();
			Assert.Equal(orig.Asynchronous, clone.Asynchronous);
			Assert.Equal(orig.Branch, clone.Branch);
			Assert.Equal(orig.Verbose, clone.Verbose);
		}
	}
}
