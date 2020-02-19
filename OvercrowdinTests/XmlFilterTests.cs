using System.IO;
using System.Xml.Linq;
using Overcrowdin.ContentFiltering;
using Xunit;

namespace OvercrowdinTests
{
	public class XmlFilterTests
	{
		[Fact]
		public static void TranslatableElementsUnspecified_Passes()
		{
			var doc = XDocument.Load(new StringReader(XmlOpenTag + XmlCloseTag));
			Assert.True(XmlFilter.HasTranslatableItems(doc));
		}

		[Fact]
		public static void TranslatableElementsMatch_Passes() {
			var doc = XDocument.Load(new StringReader(XmlOpenTag + XmlGroupCorrect + XmlCloseTag));
			// ReSharper disable once CoVariantArrayConversion - precreating a 2D array is necessary to replicate production behavior
			Assert.True(XmlFilter.HasTranslatableItems(doc, XpathArrToTranslatableElements));
		}

		[Theory]
		[InlineData("Wrong Attribute", XmlGroupWithWrongAttribute)]
		[InlineData("Wrong Element", XmlGroupWithWrongElement)]
		[InlineData("No Attribute", XmlGroupWithoutAttribute)]
		[InlineData("Empty File (almost)", "")]
		public static void ExcludesWrongItems(string message, string xmlGroup)
		{
			var doc = XDocument.Load(new StringReader(XmlOpenTag + xmlGroup + XmlCloseTag));
			// ReSharper disable once CoVariantArrayConversion - precreating a 2D array is necessary to replicate production behavior
			Assert.False(XmlFilter.HasTranslatableItems(doc, XpathArrToTranslatableElements), message);
		}

		[Fact]
		public static void MultipleXpaths_MatchesOnlySecond_Passes()
		{
			var doc = XDocument.Load(new StringReader(XmlOpenTag + XmlGroupWithWrongAttribute + XmlCloseTag));
			var xPaths = new[] { new[] { XpathToTranslatableElements, XpathToWrongAttribute } };
			// ReSharper disable once CoVariantArrayConversion - precreating a 2D array is necessary to replicate production behavior
			Assert.True(XmlFilter.HasTranslatableItems(doc, xPaths));
		}

		internal static readonly string[][] XpathArrToTranslatableElements = {new[] {XpathToTranslatableElements}};
		internal const string XpathToTranslatableElements = "/strings//group/string[@txt]";
		internal const string XpathToWrongAttribute = "/strings//group/string[@wrong]";
		internal const string XmlOpenTag = @"<?xml version='1.0' encoding='UTF-8'?><strings>";
		internal const string XmlGroupCorrect = @"
  <group id='TopGroup1'>
    <string txt='No Records'/>
  </group>";
		internal const string XmlGroupWithWrongElement = @"
  <group id='TopGroupWrongElt'>
    <wrong txt='No Records'/>
  </group>";
		internal const string XmlGroupWithWrongAttribute = @"
  <group id='TopGroupWrongAtt'>
    <string wrong='No Records'/>
  </group>";
		internal const string XmlGroupWithoutAttribute = @"
  <group id='TopGroupWithoutAtt'>
    <string>just for giggles</string>
  </group>";
		internal const string XmlCloseTag = @"</strings>";
	}
}
