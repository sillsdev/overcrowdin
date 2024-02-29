using System.Net.Http;
using Crowdin.Api;
using Microsoft.Extensions.Configuration;
using Moq;
using Overcrowdin;
using RichardSzalay.MockHttp;

namespace OvercrowdinTests
{
	/// <summary>
	/// Base class for tests that need to mock the CrowdinApi
	/// </summary>
	public class CrowdinApiTestBase
	{
		protected readonly Mock<IConfiguration> _mockConfig;
		protected readonly CrowdinApiClient _mockClient;
		protected readonly MockHttpMessageHandler _mockHttpClient;
		/*
		 *	HttpClient? httpClient = null,
			IJsonParser? jsonParser = null,
			IRateLimiter? rateLimiter = null,
			IRetryService? retryService = null
		 */
		public CrowdinApiTestBase()

		{
			_mockHttpClient = new MockHttpMessageHandler(BackendDefinitionBehavior.Always);
			_mockClient = new CrowdinApiClient(
				new CrowdinCredentials { AccessToken = "fakeyfakey" },
				_mockHttpClient.ToHttpClient());
			_mockConfig = new Mock<IConfiguration>();

		}

		public ICrowdinClientFactory MockApiFactory
		{
			get
			{
				var mockFact = new Mock<ICrowdinClientFactory>();
				mockFact.Setup(x => x.Create(
					It.IsAny<string>())).Returns(_mockClient);
				return mockFact.Object;
			}
		}
	}
}