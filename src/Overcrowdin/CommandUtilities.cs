using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public static class CommandUtilities
	{
		private const string UnsupportedSyntaxX =
			"The specified source syntax is not supported. Please submit a pull request to help us support ";

		public static Dictionary<string, FileInfo> GetFileList(IConfiguration config, IFileOptions opts, IFileSystem fs)
		{
			var files = new Dictionary<string, FileInfo>();
			// handle files specified on the command line
			if (opts.Files != null && opts.Files.Any())
			{
				foreach (var file in opts.Files)
				{
					files[file] = new FileInfo(file); // TODO (Hasso) 2019.12: use the full relative path here and elsewhere. Centralize.
				}
			}
			else
			{
				GetFilesFromConfiguration(config, fs, files);
			}

			return files;
		}

		/// <summary>Read from configuration files section that resembles:
		/// files : [
		///  {
		///    "source" : "resources/en/**/*.json",
		///    "translation" : "resources/%two_letters_code%/%original_file_name"
		///  }
		/// ]
		/// ENHANCE: put the translation destination into Crowdin
		/// </summary>
		public static void GetFilesFromConfiguration(IConfiguration config, IFileSystem fs, Dictionary<string, FileInfo> files)
		{
			var filesSection = config.GetSection("files");
			foreach (IConfigurationSection section in filesSection.GetChildren())
			{
				var source = section.GetValue<string>("source");
				var basePath = fs.Directory.GetCurrentDirectory();
				var basePathLength = basePath.Length;
				var directory = Path.Combine(basePath, Path.GetDirectoryName(source));
				var searchOption = SearchOption.TopDirectoryOnly;
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
				foreach (var sourceFile in matchedFiles)
				{
					// two off-by-one errors cancel each other out to cleanly trim the path separator from the key
					files[sourceFile.Substring(basePathLength)] = new FileInfo(sourceFile);
				}
			}
		}
	}
}
