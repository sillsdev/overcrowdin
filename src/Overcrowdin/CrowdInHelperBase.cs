using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Branches;
using Crowdin.Api.ProjectsGroups;
using Crowdin.Api.SourceFiles;
using Crowdin.Api.Storage;
using Crowdin.Api.Translations;
using AddBranchRequest = Crowdin.Api.Branches.AddBranchRequest;
using Branch = Crowdin.Api.Branches.Branch;
using Directory = Crowdin.Api.SourceFiles.Directory;
using File = Crowdin.Api.SourceFiles.File;

namespace Overcrowdin
{
	public abstract class CrowdInHelperBase
	{
		#region Member variables
		protected readonly ICrowdinApiClient _client;
		protected readonly IBranchesApiExecutor _branchExecutor;
		protected readonly SourceFilesApiExecutor _fileExecutor;

		private readonly string _projectStr;
		private readonly string _branch;

		protected Project _project;
		protected long? _branchId;

		protected List<TranslationProjectBuild> _existingTranslationBuilds;
		protected List<FileInfoCollectionResource> _existingFiles;
		protected List<Directory> _existingDirectories;

		private readonly IFileSystem _fileSystem;
		private readonly IHttpClientFactory _httpClientFactory = new DefaultHttpClientFactory();

		private class DefaultHttpClientFactory : IHttpClientFactory
		{
			public HttpClient GetClient()
			{
				return new HttpClient();
			}
		}
		#endregion

		#region Constructor
		protected CrowdInHelperBase(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory)
		{
			_projectStr = settings.Project;
			_branch = string.IsNullOrEmpty(settings.Branch) ? "None" : settings.Branch;
			_client = apiFactory.Create(settings.AccessToken);
			_branchExecutor = new BranchesApiExecutor(_client);
			_fileExecutor = new SourceFilesApiExecutor(_client);
			_fileSystem = fs;
			if (factory != null)
				_httpClientFactory = factory;
		}
		#endregion

		#region Protected helper methods
		protected static async Task<T> Initialize<T>(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory,
			Func<CrowdinProjectSettings, IFileSystem, ICrowdinClientFactory, IHttpClientFactory, T> createHelper) where T : CrowdInHelperBase
		{
			if (settings == null)
				return null;

			T crowdIn = createHelper(settings, fs, apiFactory, factory);

			Console.WriteLine("Initializing CrowdIn...");

			var initialized = await crowdIn.InitializeInternal();

			if (!initialized)
				return null;

			Console.WriteLine("CrowdIn initialization complete");
			return crowdIn;
		}

		protected virtual async Task<bool> InitializeInternal()
		{
			Console.WriteLine("    Checking project...");
			List<Project> projects = await GetFullList((offset, count) => _client.ProjectsGroups.ListProjects<Project>(limit: count, offset: offset));

			Console.WriteLine("Recognized projects...");
			foreach (var proj in projects)
			{
				Console.WriteLine(" - Name:  " + proj.Name + "\t- Identifier:  " + proj.Identifier + "\t- Description:  " + proj.Description);
			}
			_project = projects.Find(p => p.Identifier.Equals(_projectStr, StringComparison.OrdinalIgnoreCase));

			if (_project == null)
			{
				Console.Error.WriteLine($"Project matching '{_projectStr}' could not be found. Check to make sure the user that generated the access token has access to the project.");
				return false;
			}

			// check to see if branch is needed
			if (_branch.Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				_branchId = null;
				return true;
			}

			Console.WriteLine("    Checking branch...");
			List<Branch> branches = await GetFullList((offset, count) => _branchExecutor.ListBranches(_project.Id, null, count, offset));
			if (int.TryParse(_branch, out var branchNbr))
				_branchId = branchNbr;
			else
				_branchId = branches.Find(p => p.Name.Equals(_branch, StringComparison.OrdinalIgnoreCase))?.Id ?? -1;

			if (!branches.Exists(p => p.Id == _branchId))
			{
				Console.WriteLine($"Branch matching '{_branch}' could not be found in the project {_projectStr}, adding it.");
				var addedBranch = await _branchExecutor.AddBranch(_project.Id, new AddBranchRequest { Name = _branch });
				_branchId = addedBranch.Id;
				return true;
			}

			return true;
		}

		protected async Task<bool> PrepareForUploads()
		{
			Console.WriteLine("    Loading existing file list...");
			_existingFiles = await GetFullList((offset, count) => _fileExecutor.ListFiles<FileInfoCollectionResource>(_project.Id, count, offset, _branchId, recursion: 1));

			Console.WriteLine("    Loading existing directory list...");
			_existingDirectories = await GetFullList((offset, count) => _fileExecutor.ListDirectories(_project.Id, count, offset, _branchId, recursion: 1));

			// Clean up any storage that was being used previously.
			// This should be rare since we delete the storage after successfully uploading a file.
			Console.WriteLine("    Cleaning existing storage list...");
			List<StorageResource> existingStorages = await GetFullList((offset, count) => _client.Storage.ListStorages(count, offset));
			foreach (StorageResource s in existingStorages)
				await _client.Storage.DeleteStorage(s.Id);

			return true;
		}

		/// <summary>
		/// Helper method to get the full count of items back from a call to the CrowdIn API (which has a limit of 500 per call)
		/// </summary>
		protected static async Task<List<T>> GetFullList<T>(Func<int, int, Task<ResponseList<T>>> getTruncatedList)
		{
			const int countPerCall = 500;
			List<T> fullList = new List<T>(countPerCall);
			int prevSize;
			do
			{
				prevSize = fullList.Count;
				ResponseList<T> response = await getTruncatedList(fullList.Count, countPerCall);
				fullList.AddRange(response.Data);
			}
			while (fullList.Count - prevSize == countPerCall);

			return fullList;
		}

		protected async Task<bool> BuildAndDownload(string outputPath, List<string> readyLanguages, bool skipUntranslated, DateTimeOffset buildCutoffTime)
		{
			// Check to see if an existing build has already been created for the languages that are ready
			long? buildId = _existingTranslationBuilds.Find(tb =>
				tb.FinishedAt > buildCutoffTime && (_branchId == null || tb.Attributes.BranchId == _branchId) &&
				tb.Attributes.TargetLanguageIds.All(id => readyLanguages.Contains(id)))?.Id;

			if (buildId == null)
				buildId = await BuildTranslation(readyLanguages, skipUntranslated);

			if (buildId == null)
				return false; // build failed

			Console.WriteLine("Dowloading translation zip...");
			// Get the URL for the built translation
			var response = await _client.Translations.DownloadProjectTranslations(_project.Id, buildId.Value);
			var url = response.Link?.Url;
			if (url == null)
			{
				Console.Error.WriteLine("Failed to get download URL");
				return false;
			}

			// Download the build translation and save it to disk
			var httpClient = _httpClientFactory.GetClient();
			using (var zipFileStream = _fileSystem.FileStream.New(outputPath, FileMode.Create)) // Can't use a "using" statement since it gets disposed before the enumeration is read
			using (var stream = await httpClient.GetStreamAsync(url))
				await stream.CopyToAsync(zipFileStream);

			Console.WriteLine("Translation zip downloaded");
			return true;
		}

		protected async Task UploadFileInternal(string fileData, string filePath, ProjectFileType fileType, FileParameters parameters)
		{
			// Find the wanted directory or create it if it doesn't already exist
			Directory directory = null;
			// split on the path separator so we can create all the necessary parent folders
			// ReSharper disable once PossibleNullReferenceException
			var dirs = Path.GetDirectoryName(filePath).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
			var parentDir = 0L;
			var branchSearchId = _branchId ?? 0; // Files can't have a null directory or branch ID for some reason
			foreach (var dir in dirs)
			{
				directory = _existingDirectories.Find(d =>
					d.BranchId == branchSearchId && d.DirectoryId == parentDir && string.Equals(d.Name, dir, StringComparison.OrdinalIgnoreCase));
				if (directory == null)
				{
					var request = new AddDirectoryRequest
					{
						Name = dir,
						// Redundant to the File ExportPattern, but this may make it easier to upload a single new file through the web interface.
						ExportPattern = parameters.FilesToExportPatterns[filePath]
					};
					if (parentDir != 0)
					{
						request.DirectoryId = parentDir;
					}
					else
					{
						request.BranchId = _branchId;
					}
					directory = await _fileExecutor.AddDirectory(_project.Id, request);
					_existingDirectories.Add(directory);
				}
				parentDir = directory.Id;
			}

			// Create new storage for the file and upload it.
			var fileName = Path.GetFileName(filePath);
			StorageResource storage;
			using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(fileData), false))
				storage = await _client.Storage.AddStorage(stream, fileName);

			var directorySearchId = directory?.Id ?? 0; // Files and directories can't have a null branch ID for some reason
			var existingFile = _existingFiles.Find(f =>
				f.BranchId == branchSearchId && f.DirectoryId == directorySearchId && string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase));
			if (existingFile != null)
			{
				// File already exists so move it from storage onto the existing file, updating it
				var request = new ReplaceFileRequest
				{
					StorageId = storage.Id,
					UpdateOption = FileUpdateOption.KeepTranslationsAndApprovals
				};

				ValueTuple<File, bool?> response = await _fileExecutor.UpdateOrRestoreFile(_project.Id, existingFile.Id, request);
				_existingFiles.Remove(existingFile);
				Console.WriteLine($"Updated file {response.Item1.Path} ({(response.Item2 == true ? "modified" : "no change")})");
			}
			else
			{
				// File does not exist so move it from storage as a new file
				var request = new AddFileRequest
				{
					Name = fileName,
					StorageId = storage.Id,
					BranchId = _branchId,
					DirectoryId = directory?.Id,
					Type = fileType,
					ExportOptions = new GeneralFileExportOptions { ExportPattern = parameters.FilesToExportPatterns[filePath] }
				};
				if (parameters is AddFileParameters addParams)
				{
					request.ImportOptions = new XmlFileImportOptions
					{
						TranslateContent = addParams.TranslateContent,
						TranslateAttributes = addParams.TranslateAttributes,
						ContentSegmentation = addParams.ContentSegmentation,
						TranslatableElements = addParams.TranslatableElements
					};
				}

				// appears that both values can't be null, so try using 0 for directory id
				if (request.BranchId == null && request.DirectoryId == null)
					request.DirectoryId = 0;

				var newFile = await _fileExecutor.AddFile(_project.Id, request);
				Console.WriteLine($"Added file {newFile.Path}");
			}

			// Delete storage that was created for the file (no need to keep it around)
			await _client.Storage.DeleteStorage(storage.Id);
		}

		#endregion

		#region Private helper methods
		private async Task<long?> BuildTranslation(List<string> languages, bool skipUntranslated)
		{
			Console.WriteLine($"Starting translation build for {string.Join(", ", languages)}...");

			BuildProjectTranslationRequest request = new TranslationCreateProjectBuildForm
			{
				BranchId = _branchId,
				TargetLanguageIds = languages,
				SkipUntranslatedFiles = skipUntranslated
			};

			ProjectBuild build = await _client.Translations.BuildProjectTranslation(_project.Id, request);
			if (build.Status == BuildStatus.InProgress)
				build = await WaitForBuildToComplete(build);

			if (build.Status == BuildStatus.Failed || build.Status == BuildStatus.Canceled)
			{
				Console.Error.WriteLine("Translation build stopped unexpectedly: " + build.Status);
				return null;
			}

			Console.WriteLine("Translation build finished");
			return build.Id;
		}

		private async Task<ProjectBuild> WaitForBuildToComplete(ProjectBuild build)
		{
			int prevProgressDiv10 = 0;
			var waitPeriod = 2000;
			var maxWaitTime = 60 * 60 * 1000; // 1 hour
			var taskTime = 0;
			do
			{
				await Task.Delay(waitPeriod);

				build = await _client.Translations.CheckProjectBuildStatus(_project.Id, build.Id);

				int progressDiv10 = build.Progress / 10;
				if (progressDiv10 != prevProgressDiv10)
				{
					Console.WriteLine($"    {build.Progress}% complete");
					prevProgressDiv10 = progressDiv10;
				}

				maxWaitTime += waitPeriod;
			} while (build.Status == BuildStatus.InProgress && taskTime < maxWaitTime);
			if(taskTime >= maxWaitTime)
				build.Status = BuildStatus.Failed;

			return build;
		}
		#endregion
	}

	public interface IHttpClientFactory
	{
		HttpClient GetClient();
	}
}