using Microsoft.Extensions.Configuration;
using Moq;
using Overcrowdin;

namespace OvercrowdinTests
{
	/// <summary>
	/// Base class for tests that need to mock the CrowdinApi
	/// </summary>
	public class CrowdinApiTestBase
	{
		protected readonly Mock<IConfiguration> _mockConfig;
		protected readonly Mock<ICrowdinClient> _mockClient;
		public CrowdinApiTestBase()

		{
			_mockClient = new Mock<ICrowdinClient>();
			var mockFact = new Mock<ICrowdinClientFactory>();
			mockFact.Setup(x => x.Create()).Returns(_mockClient.Object);

			CrowdinCommand.ClientFactory = mockFact.Object;
			_mockConfig = new Mock<IConfiguration>();

		}
	}
}