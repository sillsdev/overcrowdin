using System;
using System.Linq;
using Crowdin.Api.ProjectsGroups;
using Crowdin.Api.SourceFiles;

namespace Overcrowdin
{
    public sealed class CrowdinProjectSettings
    {
        public readonly string AccessToken;
        public readonly string Project;
        public readonly string Branch;
		public readonly int ProjectId;
		public readonly int BranchId;

		public CrowdinProjectSettings(string project, string branchName, string accessToken)
        {
            Project = project;
            Branch = branchName;
            AccessToken = accessToken;
			var apiInstance = CrowdinCommand.GetClient(accessToken);
			try
			{
				var projects = apiInstance.ProjectsGroups.ListProjects<Project>().Result.Data;
				ProjectId = projects.First(p => p.Identifier == project).Id;
				if (!string.IsNullOrEmpty(branchName))
				{
					var sourceExecutor = new SourceFilesApiExecutor(apiInstance);
					var branches = sourceExecutor.ListBranches(ProjectId).Result.Data;
					var branch = branches.FirstOrDefault(b => b.Name == branchName);
					BranchId = branch?.Id ?? 0;
				}
			}
			catch
			{
				throw new Exception($"Could not find project with name {Project} and branch {Branch}");
			}
		}
    }
}
