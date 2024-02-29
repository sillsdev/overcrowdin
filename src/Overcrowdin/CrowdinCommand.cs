using System.Threading;
using Crowdin.Api;

namespace Overcrowdin
{
	public class CrowdinCommand
	{
		public static ThreadLocal<ICrowdinClientFactory> _safeClientFactory = new ThreadLocal<ICrowdinClientFactory>();
		public static ICrowdinClientFactory ClientFactory
		{
			get => _safeClientFactory.IsValueCreated ? _safeClientFactory.Value : null;
			set => _safeClientFactory.Value = value;
		}

		public static ICrowdinApiClient GetClient(string apiKey)
		{
			return ClientFactory.Create(apiKey);
		}
	}
}