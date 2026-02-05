using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Crowdin.Api.Languages;
using Crowdin.Api.TranslationStatus;

namespace Overcrowdin
{
	internal sealed class CrowdinDownloadHelper : CrowdInHelperBase
	{
		private const int languageReadyPercentage = 90;

		#region Constructor
		private CrowdinDownloadHelper(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null) : base(settings, fs, apiFactory, factory)
		{
		}
		#endregion

		#region Public methods
		public static async Task<CrowdinDownloadHelper> Create(CrowdinProjectSettings settings, IFileSystem fs, ICrowdinClientFactory apiFactory, IHttpClientFactory factory = null)
		{
			return await Initialize(settings, fs, apiFactory, factory, (s, f, a, h) => new CrowdinDownloadHelper(s, f, a, h));
		}

		public async Task<bool> DownloadTranslations(string outputPath)
		{
			try
			{
				return await DownloadInternal(outputPath);
			}
			catch (Exception e)
			{
				//Console.Error.WriteLine($"Error uploading file {parentDirectory} / {fileName}:");
				Console.Error.WriteLine("    " + (e.InnerException != null ? e.InnerException.Message : e.Message));
				return false;
			}
		}
		#endregion

		#region Overrides of CrowdInHelper
		protected override async Task InitializeInternal()
		{
			await base.InitializeInternal();

			Console.WriteLine("    Loading existing translation builds...");
			_existingTranslationBuilds = await GetFullList((offset, count) => _client.Translations.ListProjectBuilds(_project.Id, _branchId, count, offset));
		}
		#endregion

		#region Private helper methods
		private async Task<bool> DownloadInternal(string outputPath)
		{
			List<ProgressResource> languageProgress = await GetFullList((offset, count) => _client.TranslationStatus.GetProjectProgress(_project.Id, count, offset));
			var readyLanguages = new List<string>();
			foreach (Language lang in _project.TargetLanguages)
			{
				ProgressResource progress = languageProgress.Find(lp => lp.LanguageId == lang.Id);
				if (progress != null)
					readyLanguages.Add(lang.Id);
			}

			if (readyLanguages.Count == 0)
			{
				Console.WriteLine($"WARNING: No languages found that are {languageReadyPercentage}% approved");
				return false;
			}

			return await BuildAndDownload(outputPath, readyLanguages, false, DateTimeOffset.Now.Subtract(new TimeSpan(0, 10, 0)));
		}
		#endregion
	}
}