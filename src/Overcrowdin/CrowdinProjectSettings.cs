namespace Overcrowdin
{
	public sealed class CrowdinProjectSettings
	{
		public readonly string AccessToken;
		public readonly string Project;
		public readonly string Branch;

		private CrowdinProjectSettings(string project, string branchName, string accessToken)
		{
			Project = project;
			Branch = branchName;
			AccessToken = accessToken;
		}

		public static CrowdinProjectSettings Init(string project, string branchName, string accessToken, ICrowdinClientFactory apiFactory)
		{
			return new CrowdinProjectSettings(project, branchName, accessToken);
		}
	}
}