using System;
using Overcrowdin;
using RichardSzalay.MockHttp;
using Xunit;

namespace OvercrowdinTests
{
	public class CrowdinProjectSettingsTests : CrowdinApiTestBase
	{

		[Fact]
		public void NoMatchingProjectsThrows()
		{
			_mockHttpClient.When("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': 369681,'name': 'nottest'}}}}]}}");

			var message = Assert.Throws<Exception>(()=> new CrowdinProjectSettings("test", "branch", "faketoken")).Message;
			Assert.Contains("test", message);
		}

		[Fact]
		public void SetsProjectId()
		{
			const int testId = 44444;
			const int branchId = 55555;
			const string projectName = "test";
			const string branchName = "branch";
			_mockHttpClient.When("https://api.crowdin.com/api/v2/projects?limit=25&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {testId},'identifier': '{projectName}'}}}}]}}");
			_mockHttpClient.When($"https://api.crowdin.com/api/v2/projects/{testId}/branches?limit=25&offset=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': {branchId},'name': '{branchName}'}}}}]}}");
			var settings = new CrowdinProjectSettings(projectName, branchName, "faketoken");
			Assert.Equal(testId, settings.ProjectId);
			Assert.Equal(branchId, settings.BranchId);
		}
	}
}
