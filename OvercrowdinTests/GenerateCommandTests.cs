using System;
using Microsoft.Extensions.Configuration;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class GenerateCommandTests
	{
		private IConfiguration _mockConfig;


		public GenerateCommandTests()
		{
			CrowdinCommand.ClientFactory = new MockCrowdinClientFactory();
		}

		public void Setup()
		{

		}

		[Fact]
		public void Test1()
		{

		}
	}
}
