using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public static class CommandUtilities
	{
		private const string UnsupportedSyntaxX =
			"The specified source syntax is not supported. Please submit a pull request to help us support ";

		public static void GetFileList(IConfiguration config, IFileOptions opts, IFileSystem fs, FileParameters fileParams, SortedSet<string> folders)
		{
			if (fileParams.Files == null)
			{
				fileParams.Files = new Dictionary<string, FileInfo>();
			}

			// handle files specified on the command line
			if (opts.Files != null && opts.Files.Any())
			{
				foreach (var file in opts.Files)
				{
					fileParams.Files[file.Replace(Path.DirectorySeparatorChar, '/')] = new FileInfo(file); // TODO (Hasso) 2019.12: normalize keys: no C:\
					var dir = GetNormalizedParentFolder(file);
					if (!string.IsNullOrEmpty(dir))
					{
						folders.Add(dir);
					}
				}
			}
			else
			{
				GetFilesFromConfiguration(config, fs, fileParams, folders);
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
		public static void GetFilesFromConfiguration(IConfiguration config, IFileSystem fs, FileParameters fileParams, SortedSet<string> folders)
		{
			if (fileParams.Files == null)
			{
				fileParams.Files = new Dictionary<string, FileInfo>();
			}
			if (fileParams.ExportPatterns == null)
			{
				fileParams.ExportPatterns = new Dictionary<string, string>();
			}

			var basePath = config.GetValue<string>("base_path");
			basePath = basePath.Equals(".")
				? fs.Directory.GetCurrentDirectory()
				// DirectoryInfo will normalize the path; Combine will determine whether to treat basePath as absolute.
				: fs.DirectoryInfo.FromDirectoryName(Path.Combine(fs.Directory.GetCurrentDirectory(), basePath)).FullName;
			var basePathLength = basePath.Length;
			// The root directory (C:\ or /) contains a trailing path separator character; other paths do not; always include it in the length
			if (!basePath.EndsWith(Path.DirectorySeparatorChar))
			{
				basePathLength++;
			}

			var filesSection = config.GetSection("files");
			foreach (IConfigurationSection section in filesSection.GetChildren())
			{
				var source = section.GetValue<string>("source");
				var translation = section.GetValue<string>("translation");
				// A leading directory separator char (permissible in source) causes Path.Combine to interpret the path as rooted,
				// but the source path is always relative (to base_path), so use join here.
				var directory = fs.DirectoryInfo.FromDirectoryName(Path.Join(basePath, Path.GetDirectoryName(source))).FullName;
				var searchOption = SearchOption.TopDirectoryOnly;
				if (!directory.StartsWith(basePath))
				{
					throw new NotSupportedException($"All files must be within the base path. The following may not be: {source}");
				}
				if (directory.Contains("***"))
				{
					throw new NotImplementedException(UnsupportedSyntaxX + source);
				}
				if (directory.EndsWith("**"))
				{
					// For now, we support recursion only at the end of the directory path. Other options are not worth our effort at this time.
					directory = Path.GetDirectoryName(directory);
					searchOption = SearchOption.AllDirectories;
				}
				if (directory.Contains('*'))
				{
					throw new NotImplementedException(UnsupportedSyntaxX + source);
				}
				var filePattern = Path.GetFileName(source);
				var matchedFiles = fs.Directory.GetFiles(directory, filePattern, searchOption);
				foreach (var sourceFile in matchedFiles.Where(f => IsLocalizable(f, fs)))
				{
					// Key is the relative path with Unix directory separators
					var key = sourceFile.Substring(basePathLength).Replace(Path.DirectorySeparatorChar, '/');
					fileParams.Files[key] = new FileInfo(sourceFile);
					fileParams.ExportPatterns[key] = translation;

					var dir = GetNormalizedParentFolder(key);
					if (!string.IsNullOrEmpty(dir))
					{
						folders.Add(dir);
					}
				}
			}
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

		public const int BatchSize = 20;

		public static FileParameters[] BatchFiles(FileParameters allFiles)
		{
			if (allFiles.Files.Count <= BatchSize)
			{
				return new[] {allFiles};
			}

			var keys = allFiles.Files.Keys.ToArray();
			var batchCount = (keys.Length - 1) / BatchSize + 1; // if there is any remainder, round up
			var batchedFiles = new FileParameters[batchCount];
			for (var i = 0; i < batchCount; i++)
			{
				// choosing UpdateFileParameters because FileParameters is abstract and creating a new subclass seems like too much extra code.
				// UpdateFileParameters contains the NewNames list that will need to be batched
				batchedFiles[i] = new UpdateFileParameters
				{
					Files = new Dictionary<string, FileInfo>(),
					ExportPatterns = new Dictionary<string, string>()
				};
			}
			for (var i = 0; i < keys.Length; i++)
			{
				var key = keys[i];
				var currentBatch = batchedFiles[i / BatchSize];
				currentBatch.Files[key] = allFiles.Files[key];
				if (allFiles.ExportPatterns.TryGetValue(key, out var exportPattern))
				{
					currentBatch.ExportPatterns[key] = exportPattern;
				}
			}

			return batchedFiles;
		}

		/// <summary>
		/// Determines whether a file should be uploaded to Crowdin.
		/// .resx files with no localizable data are not uploaded.
		/// </summary>
		public static bool IsLocalizable(string path, IFileSystem fs)
		{
			return !".resx".Equals(Path.GetExtension(path)) || HasLocalizableData(XDocument.Load(fs.File.OpenRead(path)));
		}

		/// <returns>true if the given resx document contains at least one localizable string</returns>
		public static bool HasLocalizableData(XDocument resxDoc)
		{
			return resxDoc.Element("root")?.Elements("data").Any(HasLocalizableData) ?? false;
		}

		/// <returns>true if the given resx <c>data</c> element has a localizable string</returns>
		public static bool HasLocalizableData(XElement elt)
		{
			var name = elt.Attribute("name")?.Value;
			if (string.IsNullOrEmpty(name))
				return false;
			// Project resource strings do not have a '.' in their name, but WinForms dialog .resx files have a '.' in every name.
			// The only localizable properties of WinForms components are Text, AccessibleName, and AccessibleDescription.
			if (name.Contains('.') &&
				!(name.EndsWith(".Text") || name.EndsWith(".AccessibleName") || name.EndsWith(".AccessibleDescription")))
				return false;
			if (string.IsNullOrWhiteSpace(elt.Element("value")?.Value))
				return false;
			return true;
		}
	}
}
