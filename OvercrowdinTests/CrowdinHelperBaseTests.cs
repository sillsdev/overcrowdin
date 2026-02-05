using Overcrowdin;
using RichardSzalay.MockHttp;
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace OvercrowdinTests
{
	public class CrowdinHelperBaseTests : CrowdinApiTestBase
	{
		[Fact]
		public async Task Initialize_ThrowsIfProjectNotFound()
		{
			var mockFileSystem = new MockFileSystem();
			const string apiKeyEnvVar = "EXPORT_KEY_FOR_TEST";
			const string projectName = "expectedCrowdinProject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakeApiKey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectName);
			_mockHttpClient.When("https://api.crowdin.com/api/v2/projects?limit=500&offset=0&hasManagerAccess=0").Respond(
				"application/json", $"{{'data':[{{'data': {{'id': 369681,'identifier': 'UN-expectedProject'}}}}]}}");
			// SUT
			var exception = await Assert.ThrowsAsync<Exception>(async () => await TestCrowdinHelper.Create(_mockConfig.Object, MockApiFactory, new MockHttpClientFactory(_mockHttpClient)));
			Assert.Contains(projectName, exception.Message);
		}

		private class TestCrowdinHelper(CrowdinProjectSettings settings, ICrowdinClientFactory apiFactory, IHttpClientFactory factory)
			: CrowdinHelperBase(settings, null, apiFactory, factory)
		{
			public static async Task<TestCrowdinHelper> Create(IConfiguration config, ICrowdinClientFactory apiFactory, IHttpClientFactory factory)
			{
				var credentials = CommandUtilities.GetProjectSettingsFromConfiguration(config, null, apiFactory);
				return await Initialize(credentials, null, apiFactory, factory, (s, _, a, h) => new TestCrowdinHelper(s, a, h));
			}
		}
	}
}