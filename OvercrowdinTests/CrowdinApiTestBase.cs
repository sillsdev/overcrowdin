using Crowdin.Api;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using RichardSzalay.MockHttp;
using System.IO.Abstractions.TestingHelpers;

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

		public static MockFileSystem SetUpDirectoryStructure()
		{
			var fileSys = new MockFileSystem();
			fileSys.Directory.CreateDirectory("jane/doe");
			fileSys.Directory.CreateDirectory("john/doe");
			fileSys.Directory.CreateDirectory("john/quincy/adams");
			fileSys.Directory.CreateDirectory("john/quincy/doe");
			fileSys.File.WriteAllText("test.txt", "contents");
			fileSys.File.WriteAllText("jane/test.txt", "contents");
			fileSys.File.WriteAllText("jane/doe/test.txt", "contents");
			fileSys.File.WriteAllText("john/test.txt", "contents");
			fileSys.File.WriteAllText("john/doe/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/adams/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/adams/allonym.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/doe/test.txt", "contents");
			return fileSys;
		}

		public static JObject SetUpConfig(string fileSourceGlob, string basePath = ".")
		{
			dynamic configJson = new JObject();
			configJson.project_id = "testcrowdinproject";
			configJson.api_key_env = "KEYEXISTS";
			configJson.base_path = basePath;
			dynamic file = new JObject();
			file.source = fileSourceGlob;
			var files = new JArray { file };
			configJson.files = files;
			return configJson;
		}
	}
}