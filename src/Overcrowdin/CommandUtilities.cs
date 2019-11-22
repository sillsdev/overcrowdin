using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public static class CommandUtilities
	{
		/// <summary>Read from configuration files section that resembles:
		/// files : [
		///  {
		///    "source" : "resources/en/*.json",
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
				var filePattern = section.GetValue<string>("source");
				var matchedFiles = fs.Directory.GetFiles(fs.Directory.GetCurrentDirectory(), filePattern);
				foreach (var sourceFile in matchedFiles)
				{
					files[Path.GetFileName(sourceFile)] = new FileInfo(sourceFile);
				}
			}
		}
	}
}
