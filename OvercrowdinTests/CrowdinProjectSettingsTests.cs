using System;
using System.Threading.Tasks;
using Overcrowdin;
using RichardSzalay.MockHttp;
using Xunit;

namespace OvercrowdinTests
{
	public class CrowdinProjectSettingsTests : CrowdinApiTestBase
	{

		[Fact]
		public async Task NoMatchingProjectsThrows()
		{
			_mockHttpClient.When("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': 369681,'name': 'nottest'}}}}]}}");

			var exception = await Assert.ThrowsAsync<Exception>(async () => await CrowdinProjectSettings.Init("test", "branch", "faketoken", MockApiFactory));
			Assert.Contains("test", exception.Message);
		}

		[Fact]
		public async Task SetsProjectId()
		{
			const int testId = 44444;
			const int branchId = 55555;
			const string projectName = "test";
			const string branchName = "branch";
			_mockHttpClient.When("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {testId},'identifier': '{projectName}'}}}}]}}");
			_mockHttpClient.When($"https://api.crowdin.com/api/v2/projects/{testId}/branches?limit=25&offset=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {branchId},'name': '{branchName}'}}}}]}}");
			var settings = await CrowdinProjectSettings.Init(projectName, branchName, "faketoken", MockApiFactory);
			Assert.Equal(testId, settings.ProjectId);
			Assert.Equal(branchId, settings.BranchId);
		}
	}
}