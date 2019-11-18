namespace Overcrowdin
{
	public class CrowdinCommand
	{
		public static ICrowdinClientFactory ClientFactory;

		public static ICrowdinClient GetClient()
		{
			return ClientFactory.Create();
		}
	}
}