using System.IO.Abstractions;
using System.Linq;

namespace Overcrowdin.ContentFiltering
{
	public static class ContentFilter
	{
		public static ContentFilterBase[] Filters { get; }

		static ContentFilter()
		{
			Filters = new ContentFilterBase[]
			{
				new ResxFilter(),
				new XmlFilter()
			};
		}

		/// <summary>
		/// Determines whether a file should be uploaded to Crowdin.
		/// Certain files with no localizable data are not uploaded.
		/// </summary>
		public static bool IsLocalizable(IFileSystem fs, string path, params object[] args)
		{
			var filter = Filters.FirstOrDefault(f => f.CanVerify(path));
			if (filter == null)
			{
				return true; // this file type is not filtered; all files of this type are localizable
			}
			return filter.IsLocalizable(fs, path, args);
		}
	}
}