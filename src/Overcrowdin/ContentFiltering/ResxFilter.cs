using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;

namespace Overcrowdin.ContentFiltering
{
	/// <summary>
	/// .resx files with no localizable data are not uploaded to Crowdin.
	/// </summary>
	public class ResxFilter : ContentFilterBase
	{
		public override string FileExtension => ".resx";

		public override bool IsLocalizable(IFileSystem fs, string path, params object[] args)
		{
			return HasLocalizableData(XDocument.Load(fs.File.OpenRead(path)));
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
