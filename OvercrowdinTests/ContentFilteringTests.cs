using System.IO.Abstractions.TestingHelpers;
using Overcrowdin.ContentFiltering;
using Xunit;

namespace OvercrowdinTests
{
	public class ContentFilteringTests
	{
		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void FilterFiltersResx(bool hasLocalizableData)
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName = "test.resx";
			mockFileSystem.File.WriteAllText(fileName,
				ResxFilterTests.ResxOpenTag +
				(hasLocalizableData ? ResxFilterTests.ResxLocalizableData : string.Empty) +
				ResxFilterTests.ResxCloseTag);
			Assert.Equal(hasLocalizableData, ContentFilter.IsLocalizable(mockFileSystem, fileName));
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void FilterFiltersXml(bool hasLocalizableData)
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName = "test.xml";
			mockFileSystem.File.WriteAllText(fileName,
				XmlFilterTests.XmlOpenTag +
				(hasLocalizableData ? XmlFilterTests.XmlGroupCorrect : string.Empty) +
				XmlFilterTests.XmlCloseTag);
			// ReSharper disable once CoVariantArrayConversion - precreating a 2D array is necessary to replicate production behavior
			Assert.Equal(hasLocalizableData,
				ContentFilter.IsLocalizable(mockFileSystem, fileName, XmlFilterTests.XpathArrToTranslatableElements));
		}

		[Theory]
		[InlineData("test.xml")]
		[InlineData("test.txt")]
		public void FilterPassesUnfilterableFiles(string fileName)
		{
			var mockFileSystem = new MockFileSystem();
			mockFileSystem.File.WriteAllText(fileName, ResxFilterTests.ResxOpenTag + ResxFilterTests.ResxCloseTag);
			Assert.True(ContentFilter.IsLocalizable(mockFileSystem, fileName));
		}
	}
}
