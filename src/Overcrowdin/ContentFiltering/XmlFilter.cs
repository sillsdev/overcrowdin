using System;
using System.Collections;
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

		public override bool CanVerify(string path, params object[] args)
		{
			var typeArg = args?.OfType<string>().FirstOrDefault();
			return base.CanVerify(path, args) ||
			       string.Equals(typeArg, "xml", StringComparison.OrdinalIgnoreCase);
		}

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

			var translatableElements = (IEnumerable<string>)args[0];
			// ReSharper disable PossibleMultipleEnumeration - most times, there will be only one element. Also, we
			// expect all implementations to be list or array
			return !translatableElements.Any() || translatableElements.Any(te => HasContent(doc, te));
			// ReSharper restore PossibleMultipleEnumeration
		}

		private static bool HasContent(XNode doc, string xpath)
		{
			var evaluated = doc.XPathEvaluate(xpath);
			if (!(evaluated is IEnumerable enumerable))
			{
				return false;
			}

			foreach (var node in enumerable)
			{
				switch (node)
				{
					case XElement element when !string.IsNullOrWhiteSpace(element.Value):
						return true;
					case XAttribute attribute when !string.IsNullOrWhiteSpace(attribute.Value):
						return true;
				}
			}

			return false;
		}
	}
}
