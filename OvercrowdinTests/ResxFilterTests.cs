using System.IO;
using System.Xml.Linq;
using Overcrowdin.ContentFiltering;
using Xunit;

namespace OvercrowdinTests
{
	public class ResxFilterTests
	{
		[Fact]
		public void ResxFilterExcludesEmptyValues()
		{
			var elt = new XElement("data", new XAttribute("name", "someString"), new XElement("value"));
			Assert.False(ResxFilter.HasLocalizableData(elt));
		}

		[Theory]
		[InlineData("something.Icon")]
		[InlineData("something.Name")]
		public void ResxFilterExcludesNonLocalizableStrings(string dataName)
		{
			var elt = new XElement("data", new XAttribute("name", dataName), new XElement("value", "content"));
			Assert.False(ResxFilter.HasLocalizableData(elt));
		}

		[Theory]
		[InlineData("someString")]
		[InlineData("something.Text")]
		[InlineData("something.AccessibleName")]
		[InlineData("something.AccessibleDescription")]
		public void ResxFilterIncludesLocalizableStrings(string dataName)
		{
			var elt = new XElement("data", new XAttribute("name", dataName), new XElement("value", "content"));
			Assert.True(ResxFilter.HasLocalizableData(elt));
		}

		[Fact]
		public void ResxFilterIncludesLocalizableDocuments()
		{
			var doc = XDocument.Load(new StringReader(ResxOpenTag + ResxLocalizableData + ResxNonLocalizableData + ResxCloseTag));
			Assert.True(ResxFilter.HasLocalizableData(doc));
		}

		/// <remarks>
		/// Crowdin imports whitespaces strings, but they are hidden by default and localizers are instructed not to localize spaces.
		/// </remarks>
		[Fact]
		public void ResxFilterExcludesLocalizableWhitespace()
		{
			var doc = XDocument.Load(new StringReader(ResxOpenTag + ResxLocalizableWhitespace + ResxCloseTag));
			Assert.False(ResxFilter.HasLocalizableData(doc));
		}

		[Fact]
		public void ResxFilterExcludesNonLocalizableDocuments()
		{
			var doc = XDocument.Load(new StringReader(ResxOpenTag + ResxEmptyLocalizableData + ResxNonLocalizableData + ResxCloseTag));
			Assert.False(ResxFilter.HasLocalizableData(doc));
		}

		internal const string ResxOpenTag = @"<?xml version=""1.0"" encoding=""utf-8""?><root>";
		internal const string ResxLocalizableData = @"
  <data name=""$this.AccessibleName"" xml:space=""preserve"">
	<value>Date matcher</value>
  </data>";
		private const string ResxLocalizableWhitespace = @"
  <data name=""ksSingleSpace"" xml:space=""preserve"">
	<value> </value>
  </data>";
		private const string ResxNonLocalizableData = @"
  <data name=""&gt;&gt;$this.Name"" xml:space=""preserve"">
	<value>SimpleDateMatchDlg</value>
  </data>";
		private const string ResxEmptyLocalizableData = @"
  <data name=""$this.Text"" xml:space=""preserve"">
	<value></value>
  </data>";
		internal const string ResxCloseTag = "</root>";
	}
}