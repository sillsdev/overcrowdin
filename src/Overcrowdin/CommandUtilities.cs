using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Overcrowdin.ContentFiltering;

namespace Overcrowdin
{
	public static class CommandUtilities
	{
		public static async Task<CrowdinProjectSettings> GetProjectSettingsFromConfiguration(IConfiguration config, string optionsBranch, ICrowdinClientFactory apiFactory)
		{
			var apiKeyEnvVar = config["api_key_env"];
			if (string.IsNullOrEmpty(apiKeyEnvVar))
			{
				Console.WriteLine("The Crowdin configuration file is missing or did not contain 'api_key_env' " +
					"(the environment variable containing the API Key for your Crowdin project or account).");
				return null;
			}

			var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
			if (string.IsNullOrEmpty(apiKey))
			{
				Console.WriteLine($"Environment variable {apiKeyEnvVar} did not contain the API Key for your Crowdin project or account.");
				return null;
			}

			var branch = string.IsNullOrEmpty(optionsBranch) ? config["branch"] : optionsBranch;

			var settings = await CrowdinProjectSettings.Init(config["project_identifier"], branch, apiKey, apiFactory);
			return settings;
		}

		public static void GetFileList<T>(IConfiguration config, IFileOptions opts, IFileSystem fs,
			List<T> fileParamsList, SortedSet<string> folders) where T : FileParameters, new()
		{
			// handle files specified on the command line
			if (opts.Files != null && opts.Files.Any())
			{
				var fileParams = new T
				{
					Files = new Dictionary<string, FileInfo>()
				};
				foreach (var file in opts.Files)
				{
					fileParams.Files[file.Replace(Path.DirectorySeparatorChar, '/')] = new FileInfo(file); // TODO (Hasso) 2019.12: normalize keys: no C:\
					var dir = GetNormalizedParentFolder(file);
					if (!string.IsNullOrEmpty(dir))
					{
						folders.Add(dir);
					}
				}
				fileParamsList.Add(fileParams);
			}
			else
			{
				GetFilesFromConfiguration(config, opts, fs, fileParamsList, folders);
			}

			AddParentFolders(folders);
		}

		/// <summary>Read from configuration files section that resembles:
		/// files : [
		///  {
		///    "source" : "resources/en/**/*.json",
		///    "translation" : "resources/%two_letters_code%/%original_file_name"
		///  }
		/// ]
		/// </summary>
		public static void GetFilesFromConfiguration<T>(IConfiguration config, IFileOptions opts, IFileSystem fs,
			List<T> fileParamsList, SortedSet<string> folders) where T : FileParameters, new()
		{
			var basePath = config.GetValue<string>("base_path");
			basePath = basePath.Equals(".")
				? fs.Directory.GetCurrentDirectory()
				: Path.Combine(fs.Directory.GetCurrentDirectory(), basePath); // Combine will determine whether to treat basePath as absolute
			var basePathInfo = fs.DirectoryInfo.New(basePath);
			basePath = basePathInfo.FullName; // normalize the path

			var basePathInfoWrapper = new DirectoryInfoWrapper(basePathInfo);

			var filesSection = config.GetSection("files");
			foreach (IConfigurationSection section in filesSection.GetChildren())
			{
				var source = section.GetValue<string>("source");
				var translation = section.GetValue<string>("translation");

				// A leading directory separator char (permissible in source) causes Path.Combine to interpret the path as rooted,
				// but the source path is always relative (to base_path), so trim leading directory separators.
				source = source.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				if (!fs.DirectoryInfo.New(Path.Combine(basePath, source)).FullName.StartsWith(basePath))
				{
					throw new NotSupportedException($"All files must be within the base path. The following may not be: {source}");
				}

				var fileParams = new T
				{
					// REVIEW (Hasso) 2020.09: should we allow a branch from Opts when getting files from the config file?
					Files = new Dictionary<string, FileInfo>(),
					ExportPatterns = new Dictionary<string, string>()
				};
				var translatableElements = section.GetSection("translatable_elements").GetChildren().Select(te => te.Get<string>()).ToList();
				if (fileParams is AddFileParameters addFileParams)
				{
					addFileParams.TranslateContent = GetIntAsBool(section, "translate_content");
					addFileParams.TranslateAttributes = GetIntAsBool(section, "translate_attributes");
					addFileParams.ContentSegmentation = GetIntAsBool(section, "content_segmentation");
					if (section.GetValue<int?>("import_translations") != null)
					{
						Console.WriteLine("Warning: the option import_translations is not yet supported by overcrowdin!");
					}
					addFileParams.TranslatableElements = translatableElements;
				}

				var matcher = new Matcher();
				matcher.AddInclude(source);
				foreach (var ignore in section.GetSection("ignore").GetChildren().Select(i => i.Get<string>()))
				{
					// https://support.crowdin.com/configuration-file/#usage-of-wildcards provides for ignoring translated files
					// by using %placeholders% in the ignore section. Warn users that Overcrowdin has not implemented this.
					var iPercent1 = ignore.IndexOf('%');
					if (iPercent1 >= 0)
					{
						var iPercent2 = ignore.IndexOf('%', iPercent1);
						if (iPercent2 >= 0)
						{
							Console.WriteLine("Warning: the following section of your ignore pattern may be interpreted as plain text:"
								+ Environment.NewLine + ignore.Substring(iPercent1, iPercent2 + 1 - iPercent1));
						}
					}
					matcher.AddExclude(ignore);
				}
				var matches = matcher.Execute(basePathInfoWrapper);
				foreach (var sourceFile in matches.Files.Select(match => match.Path)
					// REVIEW (Hasso) 2025.11: If a file is modified so that it no longer has translatable elements, this filter will leave the old version in Crowdin on Update.
					.Where(f => ContentFilter.IsLocalizable(fs, f, translatableElements)))
				{
					// Key is the relative path with Unix directory separators
					var key = sourceFile.Replace(Path.DirectorySeparatorChar, '/');
					var path = Path.Combine(basePath, sourceFile).Replace(Path.DirectorySeparatorChar, '/');
					fileParams.Files[key] = new FileInfo(path);
					fileParams.ExportPatterns[key] = translation;

					var dir = GetNormalizedParentFolder(key);
					if (!string.IsNullOrEmpty(dir))
					{
						folders.Add(dir);
					}
				}
				if (fileParams.Files.Any())
				{
					fileParamsList.AddRange(BatchFiles(fileParams));
				}
			}
		}

		public static bool GetIntAsBool(IConfiguration config, string key)
		{
			var val = config.GetValue<int?>(key);
			return val != null && val != 0;
		}

		// ENHANCE (Hasso) 2020.01: optimize for mostly-full directory structures?
		private static void AddParentFolders(ISet<string> folders)
		{
			foreach (var folder in folders.ToArray())
			{
				var superFolder = GetNormalizedParentFolder(folder);
				while (!string.IsNullOrEmpty(superFolder))
				{
					folders.Add(superFolder);
					superFolder = GetNormalizedParentFolder(superFolder);
				}
			}
		}

		private static string GetNormalizedParentFolder(string path)
		{
			// On Windows, each Path call normalizes to '\', but we are normalizing to '/' for cross-platform compatibility.
			return Path.GetDirectoryName(path)?.Replace(Path.DirectorySeparatorChar, '/');
		}

		/// <remarks>REVIEW (Hasso) 2025.11: this is no longer needed in API v2</remarks>>
		public static T[] BatchFiles<T>(T allFiles) where T : FileParameters, new()
		{
			return new[] { allFiles };
		}

		public static string GetBranch(IConfiguration config, IBranchOptions opts)
		{
			return opts?.Branch ?? config["branch"];
		}
	}

	public class FileParameters
	{
		public Dictionary<string, FileInfo> Files;
		public Dictionary<string, string> ExportPatterns;
	}

	public class AddFileParameters : FileParameters
	{
		//[Obsolete("Open an Issue or PR if needed")]
		public bool TranslateContent;
		//[Obsolete("Open an Issue or PR if needed")]
		public bool TranslateAttributes;
		public bool ContentSegmentation;
		public List<string> TranslatableElements;
	}

	public interface ICrowdinCredentials
	{
		string AccessToken { get; }
	}
}