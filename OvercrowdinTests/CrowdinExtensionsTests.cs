using System.Collections.Generic;
using System.IO;
using Crowdin.Api.Typed;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class CrowdinExtensionsTests : CrowdinApiTestBase
	{
		[Fact]
		public void ShallowCloneAddFileParams()
		{
			var source = new AddFileParameters
			{
				EscapeQuotes = EscapeQuotesOption.Double,
				ImportTranslations = false,
				TranslatableElements = new[] {"/root/elt[@att]"},
				TranslateAttributes = false,
				TranslateContent = false,
			};
			InitCommonProperties(source);

			// Verify everything is initialized
			var uninitialized = new AddFileParameters();
			var properties = typeof(AddFileParameters).GetProperties();
			foreach (var property in properties)
			{
				var sourceValue = property.GetValue(source);
				var defaultValue = property.GetValue(uninitialized);
				Assert.NotEqual(sourceValue, defaultValue);
			}

			// SUT
			var result = source.ShallowClone();
			Assert.IsType<AddFileParameters>(result);
			Assert.NotSame(source, result);
			var dest = (AddFileParameters)result;

			foreach (var property in properties)
			{
				var sourceValue = property.GetValue(source);
				var destValue = property.GetValue(dest);
				Assert.Equal(sourceValue, destValue);
			}
		}

		[Fact]
		public void ShallowCloneUpdateFileParams()
		{
			var source = new UpdateFileParameters
			{
				NewNames = new Dictionary<string, string> {{"directory/oldname.xml", "newname.xml"}},
				UpdateOption = UpdateFileOption.UpdateWithoutChanges
			};
			InitCommonProperties(source);

			// Verify everything is initialized
			var uninitialized = new UpdateFileParameters();
			var properties = typeof(UpdateFileParameters).GetProperties();
			foreach (var property in properties)
			{
				var sourceValue = property.GetValue(source);
				var defaultValue = property.GetValue(uninitialized);
				Assert.NotEqual(sourceValue, defaultValue);
			}

			// SUT
			var result = source.ShallowClone();
			Assert.IsType<UpdateFileParameters>(result);
			Assert.NotSame(source, result);
			var dest = (UpdateFileParameters)result;

			foreach (var property in properties)
			{
				var sourceValue = property.GetValue(source);
				var destValue = property.GetValue(dest);
				Assert.Equal(sourceValue, destValue);
			}
		}

		private void InitCommonProperties(FileParameters fileParams)
		{
			fileParams.Branch = "testBranch";
			fileParams.ExportPatterns = new Dictionary<string, string>
			{
				{"/file/path/one.txt", "/%locale%/file/path/one.txt"},
				{"/file/two.txt", "/%locale%/file/two.txt"}
			};
			fileParams.Files = new Dictionary<string, FileInfo>
			{
				{"/file/path/one.txt", new FileInfo("/file/path/one.txt")},
				{"/file/two.txt", new FileInfo("/file/two.txt")}
			};
			fileParams.FirstLineContainsHeader = true;
			fileParams.Scheme = "identifier,source_phrase,uk,ru,fr";
			fileParams.Titles = new Dictionary<string, string>
			{
				{"/file/path/one.txt", "One"},
				{"/file/two.txt", "Two"}
			};
			fileParams.Type = "gettext";
		}
	}
}
