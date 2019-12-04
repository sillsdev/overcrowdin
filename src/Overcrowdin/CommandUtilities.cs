using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public static class CommandUtilities
	{
		private const string SourceUnsupportedX =
			"The specified source syntax is not supported. Please submit a pull request to help us support ";

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
					throw new NotImplementedException(SourceUnsupportedX + source);
				}
				if (directory.EndsWith("**"))
				{
					// For now, we support recursion only at the end of the directory path. Other options are not worth our effort at this time.
					directory = Path.GetDirectoryName(directory);
					searchOption = SearchOption.AllDirectories;
				}
				if (directory.Contains('*'))
				{
					throw new NotImplementedException(SourceUnsupportedX + source);
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
