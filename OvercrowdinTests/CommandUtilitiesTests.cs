using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class CommandUtilitiesTests : CrowdinApiTestBase
	{
		private static MockFileSystem SetUpDirectoryStructure()
		{
			var fileSys = new MockFileSystem();
			fileSys.Directory.CreateDirectory("jane/doe");
			fileSys.Directory.CreateDirectory("john/doe");
			fileSys.Directory.CreateDirectory("john/quincy/adams");
			fileSys.Directory.CreateDirectory("john/quincy/doe");
			fileSys.File.WriteAllText("test.txt", "contents");
			fileSys.File.WriteAllText("jane/test.txt", "contents");
			fileSys.File.WriteAllText("jane/doe/test.txt", "contents");
			fileSys.File.WriteAllText("john/test.txt", "contents");
			fileSys.File.WriteAllText("john/doe/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/adams/test.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/adams/allonym.txt", "contents");
			fileSys.File.WriteAllText("john/quincy/doe/test.txt", "contents");
			return fileSys;
		}

		private static JObject SetUpConfig(string fileSourceGlob, string basePath = ".")
		{
			dynamic configJson = new JObject();

			configJson.project_id = "testcrowdinproject";
			configJson.api_key_env = "KEYEXISTS";
			configJson.base_path = basePath;
			dynamic file = new JObject();
			file.source = fileSourceGlob;
			var files = new JArray {file};
			configJson.files = files;
			return configJson;
		}

		[Fact]
		public void PathsFindFiles()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/adams/test.txt");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Single(fileParams.Files);
			Assert.Equal(@"C:\john\quincy\adams\test.txt", fileParams.Files.Values.First().FullName);
		}

		[Fact]
		public void GlobsFindFiles()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/adams/*.txt");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Equal(2, fileParams.Files.Count);
			var foundFilesArray = fileParams.Files.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\quincy\adams\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
		}

		[Fact]
		public void GlobsFindFilesRecursively()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/**/*.txt");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Equal(6, fileParams.Files.Count);
			var foundFilesArray = fileParams.Files.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\doe\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
			Assert.DoesNotContain(foundFilesArray, file => file.Contains("jane"));
		}

		[Fact(Skip = "not implemented")]
		public void GlobsFindFilesRecursivelyInSpecifiedSubdirs()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/**/doe/*.txt");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Equal(3, fileParams.Files.Count);
		}

		[Fact(Skip = "not implemented")]
		public void GlobsFindFilesInSiblingDirs()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/*/*.txt");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Equal(3, fileParams.Files.Count);
			var foundFilesArray = fileParams.Files.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\quincy\adams\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\doe\test.txt", foundFilesArray);
		}

		[Theory]
		[InlineData("john/*/*.txt")]
		[InlineData("**/quincy/**/*.txt")]
		[InlineData("john/**/doe/*.txt")]
		public void HelpfulErrorsForUnimplementedSyntax(string sourceExpression)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig(sourceExpression);
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var e = Assert.Throws<NotImplementedException>(() => CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>()));
				Assert.Contains("Please submit a pull request", e.Message);
				Assert.Contains(sourceExpression, e.Message);
			}
		}

		[Theory]
		[InlineData(@"C:\john", "../jane/*.txt")]
		public void HelpfulErrorsForUnsupportedPaths(string basePath, string subPath)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig(subPath, basePath);
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var e = Assert.Throws<NotSupportedException>(() => CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>()));
				Assert.Contains(subPath, e.Message);
			}
		}

		[Fact]
		public void HelpfulErrorsForAbsoluteSourcePaths()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("C:/jane/*.txt", @"C:\john");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				// DNF is helpful enough, and checking earlier is too complicated for such an unexpected error
				var e = Assert.Throws<DirectoryNotFoundException>(() => CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>()));
				Assert.Contains(@"C:\john\C:\jane", e.Message);
			}
		}

		[Fact]
		public void TranslationExportPatterns()
		{
			const string janePattern = "/l10n/%two_letters_code%/jane_doe/%original_file_name%";
			const string johnPattern = "/l10n/%two_letters_code%/john/**/%file_name%.%two_letters_code%.%file_extension%";
			var mockFileSystem = SetUpDirectoryStructure();
			dynamic configJson = SetUpConfig("/jane/doe/*.txt");
			var files = configJson.files;
			files[0].translation = janePattern;
			dynamic file = new JObject();
			file.source = "/john/quincy/**/*.txt";
			file.translation = johnPattern;
			files.Add(file);
			var fileParams = new UpdateFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Equal(5, fileParams.Files.Count);
			Assert.Equal(5, fileParams.ExportPatterns.Count);
			foreach (var key in fileParams.Files.Keys)
			{
				Assert.Contains(key, fileParams.ExportPatterns.Keys);
			}
			Assert.Equal(janePattern, fileParams.ExportPatterns["jane/doe/test.txt"]);
			Assert.Equal(johnPattern, fileParams.ExportPatterns["john/quincy/adams/allonym.txt"]);
			Assert.Equal(johnPattern, fileParams.ExportPatterns["john/quincy/doe/test.txt"]);
		}

		[Fact]
		public void GetIntAsBool()
		{
			dynamic json = new JObject();
			json.zero = 0;
			json.one = 1;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				Assert.False(CommandUtilities.GetIntAsBool(config, "zero"));
				Assert.True(CommandUtilities.GetIntAsBool(config, "one"));
				Assert.Null(CommandUtilities.GetIntAsBool(config, "not_specified"));
			}
		}

		[Fact]
		public void ExtraOptionsForXmlFiles()
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName = "test.xml";
			mockFileSystem.File.WriteAllText(fileName, "<br/>");
			dynamic configJson = SetUpConfig(fileName);
			var files = configJson.files;
			files[0].translate_content = 0;
			files[0].translate_attributes = 0;
			files[0].content_segmentation = 0;
			files[0].translatable_elements = new JArray {"//string[@txt]", "/cheese/wheel"};
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Single(fileParams.Files);
			Assert.False(fileParams.TranslateContent);
			Assert.False(fileParams.TranslateAttributes);
			//Assert.False(fileParams.ContentSegmentation); // TODO (Hasso) 2020.01: support this whenever Crowdin does
			var te = fileParams.TranslatableElements.ToArray();
			Assert.Equal(2, te.Length);
			Assert.Contains("//string[@txt]", te);
			Assert.Contains("/cheese/wheel", te);
		}

		[Fact]
		public void ExtraOptionsForSpecificXmlFiles() // TODO
		{
		}

		[Theory]
		[InlineData(".", "jane/doe/test.txt")]
		[InlineData(@"jane\doe", "test.txt")]
		[InlineData(@"jane\doe\", "test.txt")]
		[InlineData(@"jane/doe/", "test.txt")]
		[InlineData(@"C:\jane\doe", "test.txt")]
		[InlineData("john/../jane/doe/", "test.txt")]
		public void BasePathExcludedFromKeys(string basePath, string subPath)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig(subPath, basePath);
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Single(fileParams.Files);
			Assert.Equal(@"C:\jane\doe\test.txt", fileParams.Files.Values.First().FullName);
			Assert.Equal(subPath, fileParams.Files.Keys.First());
		}

		[Fact]
		public void AbsoluteBasePathWorks()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("/adams/test.txt", "C:/john/quincy");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var originalDir = mockFileSystem.Directory.GetCurrentDirectory();
				try
				{
					mockFileSystem.Directory.SetCurrentDirectory("C:/jane/doe");
					CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
				}
				finally
				{
					mockFileSystem.Directory.SetCurrentDirectory(originalDir);
				}
			}

			Assert.Single(fileParams.Files);
			Assert.Equal(@"C:\john\quincy\adams\test.txt", fileParams.Files.Values.First().FullName);
			Assert.Equal("adams/test.txt", fileParams.Files.Keys.First());
		}

		[Theory]
		[InlineData(@"C:\john", "quincy")]
		[InlineData(@"C:\john\quincy\doe", "..")]
		public void RelativeBasePathWorks(string currentDir, string relativeBasePath)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("/adams/test.txt", relativeBasePath);
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var originalDir = mockFileSystem.Directory.GetCurrentDirectory();
				try
				{
					mockFileSystem.Directory.SetCurrentDirectory(currentDir);
					CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
				}
				finally
				{
					mockFileSystem.Directory.SetCurrentDirectory(originalDir);
				}
			}

			Assert.Single(fileParams.Files);
			Assert.Equal(@"C:\john\quincy\adams\test.txt", fileParams.Files.Values.First().FullName);
			Assert.Equal("adams/test.txt", fileParams.Files.Keys.First());
		}

		[Fact]
		public void FindsContainingFolders()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/**/*.txt");
			var foldersToCreate = new SortedSet<string>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFileList(config, new AddCommand.Options(), mockFileSystem, new AddFileParameters(), foldersToCreate);
			}

			Assert.Equal(4, foldersToCreate.Count);
			Assert.Contains("john", foldersToCreate);
			Assert.Contains("john/quincy", foldersToCreate);
			Assert.Contains("john/quincy/adams", foldersToCreate);
			Assert.Contains("john/quincy/doe", foldersToCreate);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void BatchesFilesInTwenties(bool testWithExportPatterns)
		{
			const int fileCount = CommandUtilities.BatchSize + 1;
			var files = new Dictionary<string, FileInfo>();
			var exportPatterns = new Dictionary<string, string>();
			var fileParams = new AddFileParameters
			{
				Files = files,
				ExportPatterns = exportPatterns
			};
			for (var i = 0; i < fileCount; i++)
			{
				var key = i.ToString();
				files[key] = new FileInfo($"{i}.txt");
				if (testWithExportPatterns)
				{
					exportPatterns[key] = $"%locale%/{i}.txt";
				}
			}

			var result = CommandUtilities.BatchFiles(fileParams);

			Assert.Equal(2, result.Length);
			Assert.Equal(CommandUtilities.BatchSize, result[0].Files.Count);
			Assert.Single(result[1].Files);
			// Getting an aggregate set by adding the file from result[1] to result[0] would be easier, but changing the data seems unprincipled.
			var allBatchedFiles = result[0].Files.Concat(result[1].Files).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			for (var i = 0; i < fileCount; i++)
			{
				var key = i.ToString();
				Assert.Equal(files[key], allBatchedFiles[key]);
			}

			if (!testWithExportPatterns)
			{
				Assert.Empty(result[0].ExportPatterns);
				Assert.Empty(result[1].ExportPatterns);
				return;
			}
			Assert.Equal(CommandUtilities.BatchSize, result[0].ExportPatterns.Count);
			Assert.Single(result[1].ExportPatterns);
			var allBatchedExportPatterns = result[0].ExportPatterns.Concat(result[1].ExportPatterns).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			for (var i = 0; i < fileCount; i++)
			{
				var key = i.ToString();
				Assert.Equal(exportPatterns[key], allBatchedExportPatterns[key]);
			}
		}

		[Fact]
		public void BatchEmptyFilesListDoesntCrash()
		{
			var fileParams = new AddFileParameters { Files = new Dictionary<string, FileInfo>() };

			var result = CommandUtilities.BatchFiles(fileParams);

			Assert.Single(result);
			Assert.Empty(result.First().Files);
		}

		// Tests for filtering unnecessary .resx files:

		[Fact]
		public void ResxFilterExcludesEmptyValues()
		{
			var elt = new XElement("data", new XAttribute("name", "someString"), new XElement("value"));
			Assert.False(CommandUtilities.HasLocalizableData(elt));
		}

		[Theory]
		[InlineData("something.Icon")]
		[InlineData("something.Name")]
		public void ResxFilterExcludesNonLocalizableStrings(string dataName)
		{
			var elt = new XElement("data", new XAttribute("name", dataName), new XElement("value", "content"));
			Assert.False(CommandUtilities.HasLocalizableData(elt));
		}

		[Theory]
		[InlineData("someString")]
		[InlineData("something.Text")]
		[InlineData("something.AccessibleName")]
		[InlineData("something.AccessibleDescription")]
		public void ResxFilterIncludesLocalizableStrings(string dataName)
		{
			var elt = new XElement("data", new XAttribute("name", dataName), new XElement("value", "content"));
			Assert.True(CommandUtilities.HasLocalizableData(elt));
		}

		[Fact]
		public void ResxFilterIncludesLocalizableDocuments()
		{
			var doc = XDocument.Load(new StringReader(ResxOpenTag + ResxLocalizableData + ResxNonLocalizableData + ResxCloseTag));
			Assert.True(CommandUtilities.HasLocalizableData(doc));
		}

		/// <remarks>
		/// Crowdin imports whitespaces strings, but they are hidden by default and localizers are instructed not to localize spaces.
		/// </remarks>
		[Fact]
		public void ResxFilterExcludesLocalizableWhitespace()
		{
			var doc = XDocument.Load(new StringReader(ResxOpenTag + ResxLocalizableWhitespace + ResxCloseTag));
			Assert.False(CommandUtilities.HasLocalizableData(doc));
		}

		[Fact]
		public void ResxFilterExcludesNonLocalizableDocuments()
		{
			var doc = XDocument.Load(new StringReader(ResxOpenTag + ResxEmptyLocalizableData + ResxNonLocalizableData + ResxCloseTag));
			Assert.False(CommandUtilities.HasLocalizableData(doc));
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void FilterFiltersResx(bool hasLocalizableData)
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName = "test.resx";
			mockFileSystem.File.WriteAllText(fileName, ResxOpenTag + (hasLocalizableData ? ResxLocalizableData : string.Empty) + ResxCloseTag);
			Assert.Equal(hasLocalizableData, CommandUtilities.IsLocalizable(fileName, mockFileSystem));
		}

		[Fact]
		public void FilterFiltersOnlyResx()
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName = "test.xml";
			mockFileSystem.File.WriteAllText(fileName, ResxOpenTag + ResxCloseTag);
			Assert.True(CommandUtilities.IsLocalizable(fileName, mockFileSystem));
		}

		[Fact]
		public void MatchedFilesAreFiltered()
		{
			var mockFileSystem = new MockFileSystem();
			const string localizableFileName = "full.resx";
			mockFileSystem.File.WriteAllText(localizableFileName, ResxOpenTag + ResxLocalizableData + ResxCloseTag);
			mockFileSystem.File.WriteAllText("empty.resx", ResxOpenTag + ResxCloseTag);
			var configJson = SetUpConfig("*.resx");
			var fileParams = new AddFileParameters();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, fileParams, new SortedSet<string>());
			}

			Assert.Single(fileParams.Files);
			Assert.Equal(localizableFileName, fileParams.Files.Keys.First());
		}

		private const string ResxOpenTag = @"<?xml version=""1.0"" encoding=""utf-8""?><root>";
		private const string ResxLocalizableData = @"
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
		private const string ResxCloseTag = "</root>";
	}
}
