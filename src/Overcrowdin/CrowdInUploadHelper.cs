using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Crowdin.Api.SourceFiles;

namespace Overcrowdin
{
	internal sealed class CrowdInUploadHelper : CrowdInHelperBase
	{
		#region Member variables
		#endregion

		#region Constructor
		private CrowdInUploadHelper(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null) : base(settings, fs, apiFactory, factory)
		{
		}
		#endregion

		#region Properties
		public int FileUploadCount { get; private set; }

		public int FileErrorCount { get; private set; }
		#endregion

		#region Public methods
		public static async Task<CrowdInUploadHelper> Create(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null)
		{
			return await Initialize(settings, fs, apiFactory, factory, (s, f, a, h) => new CrowdInUploadHelper(s, f, a, h));
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

		[Obsolete]
		public async Task<int> CleanupExtraneousFiles()
		{
			// Only delete files if there were no errors since errors could cause the list-of-files-to-be-deleted
			// to contain files that just failed to upload.
			if (_existingFiles.Count == 0 || FileErrorCount > 0)
				return 0;

			return await CleanUpExtraneousFilesInternal();
		}
		#endregion

		#region Overrides of CrowdInHelper
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