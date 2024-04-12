using System;
using System.Linq;
using System.Threading.Tasks;
using Crowdin.Api.ProjectsGroups;
using Crowdin.Api.SourceFiles;

namespace Overcrowdin
{
	public sealed class CrowdinProjectSettings
	{
		public readonly string AccessToken;
		public readonly string Project;
		public readonly string Branch;
		public int ProjectId;
		public int BranchId;

		private CrowdinProjectSettings(string project, string branchName, string accessToken)
		{
			Project = project;
			Branch = branchName;
			AccessToken = accessToken;
		}

		public static async Task<CrowdinProjectSettings> Init(string project, string branchName, string accessToken, ICrowdinClientFactory apiFactory)
		{
			var settings = new CrowdinProjectSettings(project, branchName, accessToken);
			var apiInstance = apiFactory.Create(accessToken);
			try
			{
				var projects = await apiInstance.ProjectsGroups.ListProjects<Project>();
				settings.ProjectId = projects.Data.First(p => p.Identifier == project).Id;
				if (!string.IsNullOrEmpty(branchName))
				{
					var sourceExecutor = new SourceFilesApiExecutor(apiInstance);
					var branches = await sourceExecutor.ListBranches(settings.ProjectId);
					var branch = branches.Data.FirstOrDefault(b => b.Name == branchName);
					settings.BranchId = branch?.Id ?? 0;
				}
			}
			catch(Exception e)
			{
				throw new Exception($"Could not find project with name {project} and branch {branchName}", e);
			}

			return settings;
		}
	}
}