namespace Overcrowdin
{
	public sealed class CrowdinProjectSettings
	{
		public readonly string AccessToken;
		public readonly string Project;
		public readonly string Branch;
		public readonly bool Verbose;

		private CrowdinProjectSettings(string project, string branchName, string accessToken, bool verbose)
		{
			Project = project;
			Branch = branchName;
			AccessToken = accessToken;
			Verbose = verbose;
		}

		public static CrowdinProjectSettings Init(string project, string branchName, string accessToken, ICrowdinClientFactory apiFactory, bool verbose = false)
		{
			return new CrowdinProjectSettings(project, branchName, accessToken, verbose);
		}
	}
}