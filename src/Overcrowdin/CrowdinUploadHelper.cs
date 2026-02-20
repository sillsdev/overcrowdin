using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Crowdin.Api.SourceFiles;

namespace Overcrowdin
{
	internal sealed class CrowdinUploadHelper : CrowdinHelperBase
	{
		#region Member variables
		#endregion

		#region Constructor
		private CrowdinUploadHelper(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null) : base(settings, fs, apiFactory, factory)
		{
		}
		#endregion

		#region Properties
		public int FileUploadCount { get; private set; }

		public int FileErrorCount { get; private set; }
		#endregion

		#region Public methods
		public static async Task<CrowdinUploadHelper> Create(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null)
		{
			return await Initialize(settings, fs, apiFactory, factory, (s, f, a, h) => new CrowdinUploadHelper(s, f, a, h));
		}

		public async Task UploadFile(string fileData, string filePath, FileParameters parameters)
		{
			try
			{
				await UploadFileInternal(fileData, filePath, ProjectFileType.Auto, parameters);
				FileUploadCount++;
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"Error uploading file {filePath}:");
				Console.Error.WriteLine("    " + (e.InnerException != null ? e.InnerException.Message : e.Message));
				FileErrorCount++;
			}
		}

		public async Task<int> DeleteFiles(IEnumerable<string> filePaths)
		{
			return await DeleteFilesInternal(filePaths);
		}
		#endregion

		#region Overrides of CrowdinHelperBase
		protected override bool CreateBranchIfNeeded => true;

		protected override async Task InitializeInternal()
		{
			await base.InitializeInternal();

			await PrepareForUploads();
		}
		#endregion

		#region Private helper methods

		private async Task<int> CleanUpExtraneousFilesInternal()
		{
			int filesDeleted = 0;
			foreach (FileInfoCollectionResource file in _existingFiles)
			{
				await _fileExecutor.DeleteFile(_project.Id, file.Id);
				filesDeleted++;
			}

			return filesDeleted;
		}
		#endregion
	}
}
