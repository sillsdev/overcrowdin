using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class CommandUtilitiesTests : CrowdinApiTestBase
	{
		[Fact]
		public void IncompleteConfigReturnsNull()
		{
			var result = CommandUtilities.GetProjectSettingsFromConfiguration(_mockConfig.Object, null);
			Assert.Null(result);
		}

		[Fact]
		public void MissingApiKeyReturnsNull()
		{
			const string apiKeyEnvVar = "NOKEYEXISTS";
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			var result = CommandUtilities.GetProjectSettingsFromConfiguration(_mockConfig.Object, null);
			Assert.Null(result);
		}
		
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
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
			Assert.Single(fileParams.Files);
			Assert.Equal(@"C:\john\quincy\adams\test.txt", fileParams.Files.Values.First().FullName);
		}

		[Fact]
		public void GlobsFindFiles()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/adams/*.txt");
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
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
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
			Assert.Equal(6, fileParams.Files.Count);
			var foundFilesArray = fileParams.Files.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\doe\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
			Assert.DoesNotContain(foundFilesArray, file => file.Contains("jane"));
		}

		[Fact]
		public void GlobsFindFilesRecursivelyInSpecifiedSubdirs()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/**/doe/*.txt");
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
			var foundFilesArray = fileParams.Files.Values.Select(val => val.FullName).ToArray();
			Assert.Equal(2, fileParams.Files.Count);
			Assert.Contains(@"C:\john\doe\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\doe\test.txt", foundFilesArray);
		}

		[Fact]
		public void GlobsFindFilesInSiblingDirs()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/*/*.txt");
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
			Assert.Equal(3, fileParams.Files.Count);
			var foundFilesArray = fileParams.Files.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\quincy\adams\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\doe\test.txt", foundFilesArray);
		}

		[Theory]
		[InlineData(@"C:\john", "../jane/*.txt")]
		[InlineData(@"C:\john", "C:/jane/*.txt")]
		public void HelpfulErrorsForUnsupportedPaths(string basePath, string subPath)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig(subPath, basePath);
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var e = Assert.Throws<NotSupportedException>(() => CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>()));
				Assert.Contains(subPath, e.Message);
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
			var fileParamsList = new List<FileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Equal(2, fileParamsList.Count);
			var janeFileParams = fileParamsList[0];
			Assert.Single(janeFileParams.Files);
			Assert.Single(janeFileParams.ExportPatterns);
			Assert.Equal(janePattern, janeFileParams.ExportPatterns["jane/doe/test.txt"]);

			var johnFileParams = fileParamsList[1];
			Assert.Equal(4, johnFileParams.Files.Count);
			Assert.Equal(4, johnFileParams.ExportPatterns.Count);
			foreach (var key in johnFileParams.Files.Keys)
			{
				Assert.Equal(johnPattern, johnFileParams.ExportPatterns[key]);
			}
		}

		[Fact]
		public void IgnoreFolders()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			dynamic configJson = SetUpConfig("/**/*.txt");
			var file = configJson.files[0];
			file.ignore = new JArray("**/jane/**/*", "**/quincy/**/*.*");
			var fileParamsList = new List<FileParameters>();
			var folders = new SortedSet<string>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, folders);
			}

			Assert.Single(fileParamsList);
			var files = fileParamsList[0].Files.Keys;
			Assert.Equal(3, files.Count);
			Assert.Contains("test.txt", files);
			Assert.Contains("john/test.txt", files);
			Assert.Contains("john/doe/test.txt", files);

			Assert.Equal(2, folders.Count);
			Assert.Contains("john", folders);
			Assert.Contains("john/doe", folders);
		}

		[Fact]
		public void IgnoreFoldersMatching()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			dynamic configJson = SetUpConfig("/**/*.txt");
			var file = configJson.files[0];
			file.ignore = new JArray("**/j*/**/*");
			var fileParamsList = new List<FileParameters>();
			var folders = new SortedSet<string>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, folders);
			}

			Assert.Single(fileParamsList);
			var files = fileParamsList[0].Files.Keys;
			Assert.Single(files);
			Assert.Contains("test.txt", files);

			Assert.Empty(folders);
		}

		[Fact]
		public void IgnoreFilesMatching()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			dynamic configJson = SetUpConfig("/**/*.txt");
			var file = configJson.files[0];
			file.ignore = new JArray("**/te*");
			var fileParamsList = new List<FileParameters>();
			var folders = new SortedSet<string>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, folders);
			}

			Assert.Single(fileParamsList);
			var files = fileParamsList[0].Files.Keys;
			Assert.Single(files);
			Assert.Contains("john/quincy/adams/allonym.txt", files);
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
				Assert.False(CommandUtilities.GetIntAsBool(config, "not_specified"));
			}
		}

		[Fact]
		public void AdditionalOptionsForXmlFiles()
		{
			var mockFileSystem = new MockFileSystem();
			const string fileName0 = "test.xml";
			const string fileName1 = "tstB.xml";
			const string trElt0 = "//string[@txt]";
			const string trElt1a = "/cheese/wheel";
			const string trElt1b = "/round[@round]";
			mockFileSystem.File.WriteAllText(fileName0, "<string txt='something'/>");
			mockFileSystem.File.WriteAllText(fileName1, "<cheese><wheel>swiss</wheel></cheese>");
			dynamic configJson = SetUpConfig(fileName0);
			var files = configJson.files;
			files[0].translate_content = 0;
			files[0].translate_attributes = 0;
			files[0].content_segmentation = 0;
			files[0].translatable_elements = new JArray {trElt0};
			files.Add(new JObject());
			files[1].source = fileName1;
			files[1].translate_content = 1;
			files[1].translate_attributes = 1;
			files[1].content_segmentation = 1;
			files[1].translatable_elements = new JArray {trElt1a, trElt1b};
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Equal(2, fileParamsList.Count);
			var fileParams = fileParamsList[0];
			Assert.Single(fileParams.Files);
			Assert.False(fileParams.TranslateContent);
			Assert.False(fileParams.TranslateAttributes);
			//Assert.False(fileParams.ContentSegmentation); // TODO (Hasso) 2020.01: support this whenever Crowdin does
			var te = fileParams.TranslatableElements.ToArray();
			Assert.Single(te);
			Assert.Contains(trElt0, te);
			fileParams = fileParamsList[1];
			Assert.Single(fileParams.Files);
			Assert.True(fileParams.TranslateContent);
			Assert.True(fileParams.TranslateAttributes);
			//Assert.True(fileParams.ContentSegmentation); // TODO (Hasso) 2020.01: support this whenever Crowdin does
			te = fileParams.TranslatableElements.ToArray();
			Assert.Equal(2, te.Length);
			Assert.Contains(trElt1a, te);
			Assert.Contains(trElt1b, te);
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
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
			Assert.Single(fileParams.Files);
			Assert.Equal(@"C:\jane\doe\test.txt", fileParams.Files.Values.First().FullName);
			Assert.Equal(subPath, fileParams.Files.Keys.First());
		}

		[Fact]
		public void AbsoluteBasePathWorks()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("/adams/test.txt", "C:/john/quincy");
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				mockFileSystem.Directory.SetCurrentDirectory("C:/jane/doe");
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
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
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				mockFileSystem.Directory.SetCurrentDirectory(currentDir);
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
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
				CommandUtilities.GetFileList(config, new AddCommand.Options(), mockFileSystem, new List<AddFileParameters>(), foldersToCreate);
			}

			Assert.Equal(4, foldersToCreate.Count);
			Assert.Contains("john", foldersToCreate);
			Assert.Contains("john/quincy", foldersToCreate);
			Assert.Contains("john/quincy/adams", foldersToCreate);
			Assert.Contains("john/quincy/doe", foldersToCreate);
		}

		[Fact]
		public void BatchEmptyFilesListDoesntCrash()
		{
			var fileParams = new AddFileParameters { Files = new Dictionary<string, FileInfo>() };

			var result = CommandUtilities.BatchFiles(fileParams);

			Assert.Single(result);
			Assert.Empty(result.First().Files);
		}

		[Fact]
		public void ResxFilesAreFiltered()
		{
			var mockFileSystem = new MockFileSystem();
			const string localizableFileName = "full.resx";
			mockFileSystem.File.WriteAllText(localizableFileName,
				ResxFilterTests.ResxOpenTag + ResxFilterTests.ResxLocalizableData + ResxFilterTests.ResxCloseTag);
			mockFileSystem.File.WriteAllText("empty.resx", ResxFilterTests.ResxOpenTag + ResxFilterTests.ResxCloseTag);
			var configJson = SetUpConfig("*.resx");
			var fileParamsList = new List<AddFileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, fileParamsList, new SortedSet<string>());
			}

			Assert.Single(fileParamsList);
			var fileParams = fileParamsList[0];
			Assert.Single(fileParams.Files);
			Assert.Equal(localizableFileName, fileParams.Files.Keys.First());
		}

		[Theory]
		[InlineData("Add")]
		[InlineData("Update")]
		public void XmlFilesAreFiltered(string operation)
		{
			var mockFileSystem = new MockFileSystem();
			const string fileNameNotFiltered = "notFiltered.xml";
			mockFileSystem.File.WriteAllText(fileNameNotFiltered, XmlFilterTests.XmlOpenTag + XmlFilterTests.XmlCloseTag);
			const string fileNamePassesFilter = "filterPass.xml";
			mockFileSystem.File.WriteAllText(fileNamePassesFilter,
				XmlFilterTests.XmlOpenTag + XmlFilterTests.XmlGroupCorrect + XmlFilterTests.XmlCloseTag);
			mockFileSystem.File.WriteAllText("filterFail.xml", XmlFilterTests.XmlOpenTag + XmlFilterTests.XmlCloseTag);
			dynamic configJson = SetUpConfig(fileNameNotFiltered);
			dynamic file = new JObject();
			file.source = "filter*.xml";
			file.translatable_elements = new JArray { XmlFilterTests.XpathToWrongAttribute, XmlFilterTests.XpathToTranslatableElements };
			configJson.files.Add(file);
			var fileParamsList = new List<FileParameters>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				switch (operation)
				{
					case "Add":
						var addFileParamsList = new List<AddFileParameters>();
						CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, addFileParamsList, new SortedSet<string>());
						fileParamsList.AddRange(addFileParamsList);
						break;
					case "Update":
						var updateFileParamsList = new List<FileParameters>();
						CommandUtilities.GetFilesFromConfiguration(config, null, mockFileSystem, updateFileParamsList, new SortedSet<string>());
						fileParamsList.AddRange(updateFileParamsList);
						break;
				}
			}

			Assert.Equal(2, fileParamsList.Count);
			var fileParams = fileParamsList[0];
			Assert.Single(fileParams.Files);
			Assert.Equal(fileNameNotFiltered, fileParams.Files.Keys.First());
			fileParams = fileParamsList[1];
			Assert.Single(fileParams.Files);
			Assert.Equal(fileNamePassesFilter, fileParams.Files.Keys.First());
		}
	}
}
