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
		/// <summary>
		/// Finished (100%) Export Status. Must be constructed for each test, since the StringContent can be read only once.
		/// </summary>
		public static HttpResponseMessage ExportStatusFinished => new HttpResponseMessage(HttpStatusCode.Accepted)
		{
			Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<success>
  <status>{ExportStatus.Finished}</status>
  <progress>100</progress>
  <last_build>2018-10-22T13:49:00+0000</last_build>
</success>")
		};

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
		[InlineData(true)]
		[InlineData(false)]
		public async void WaitsForResults(bool wait)
		{
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectId = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			var options = new ExportCommand.Options{Asynchronous = !wait};
			// Set up the call to Export. The Export call is always async because it would time out for larger projects.
			_mockClient.Setup(client => client.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(),
					It.Is<ExportTranslationParameters>(p => TestUtils.True(p.Async))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted))).Verifiable();
			if (wait)
			{
				_mockClient.Setup(client => client.GetExportStatus(projectId, It.IsAny<ProjectCredentials>(),
						It.IsAny<GetTranslationExportStatusParameters>()))
					.Returns(Task.FromResult(ExportStatusFinished))
					.Verifiable();
			}
			var result = await ExportCommand.ExportCrowdinTranslations(_mockConfig.Object, options);
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(0, result);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async void ExportsBranches(bool useBranch)
		{
			const string apiKeyEnvVar = "EXPORTKEYFORTEST";
			const string projectId = "testcrowdinproject";
			const string baseDir = "test";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			_mockConfig.Setup(config => config["base_path"]).Returns(baseDir);
			var branch = useBranch ? "some-branch" : null;
			var options = new ExportCommand.Options{Asynchronous = false, Branch = branch};
			// Set up the call to Export. The Export call is always async because it would time out for larger projects.
			_mockClient.Setup(client => client.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(),
					It.Is<ExportTranslationParameters>(p => TestUtils.True(p.Async) && p.Branch == branch)))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted))).Verifiable();
			_mockClient.Setup(client => client.GetExportStatus(projectId, It.IsAny<ProjectCredentials>(),
					It.Is<GetTranslationExportStatusParameters>(p => p.Branch == branch)))
				.Returns(Task.FromResult(ExportStatusFinished))
				.Verifiable();
			var result = await ExportCommand.ExportCrowdinTranslations(_mockConfig.Object, options);
			_mockClient.Verify(x => x.ExportTranslation(projectId, It.IsAny<ProjectCredentials>(), It.IsAny<ExportTranslationParameters>()), Times.Exactly(1));
			Assert.Equal(0, result);
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
