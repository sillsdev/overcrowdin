using Crowdin.Api;

namespace Overcrowdin
{
	public interface ICrowdinClientFactory
	{
		ICrowdinApiClient Create(string apiKey);
	}
}