using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
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
	}
}
