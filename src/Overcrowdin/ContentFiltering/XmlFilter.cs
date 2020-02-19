using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Overcrowdin.ContentFiltering
{
	/// <summary>
	/// .xml files with no elements or attributes matching the translatable_elements xpaths
	/// (or with none specified) are not uploaded to Crowdin.
	/// </summary>
	public class XmlFilter : ContentFilterBase
	{
		public override string FileExtension => ".xml";

		public override bool IsLocalizable(IFileSystem fs, string path, params object[] args)
		{
			return HasTranslatableItems(XDocument.Parse(fs.File.ReadAllText(path)), args);
		}

		public static bool HasTranslatableItems(XDocument doc, params object[] args)
		{
			if (args.Length == 0 || args[0] == null)
			{
				return true;
			}

			var translatableElements = (IEnumerable<string>) args[0];
			// ReSharper disable PossibleMultipleEnumeration - most times, there will be only one element. Also, we
			// expect all implementations to be list or array
			return !translatableElements.Any() || translatableElements.Any(te => doc.XPathSelectElements(te).Any());
			// ReSharper restore PossibleMultipleEnumeration
		}
	}
}
