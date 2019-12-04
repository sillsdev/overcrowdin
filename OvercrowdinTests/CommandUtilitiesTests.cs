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
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles);
			}

			Assert.Single(foundFiles);
			Assert.Equal(@"C:\john\quincy\adams\test.txt", foundFiles.Values.First().FullName);
		}

		[Fact]
		public void GlobsFindFiles()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/adams/*.txt");
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles);
			}

			Assert.Equal(2, foundFiles.Count);
			var foundFilesArray = foundFiles.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\quincy\adams\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
		}

		[Fact]
		public void GlobsFindFilesRecursively()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/**/*.txt");
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles);
			}

			Assert.Equal(6, foundFiles.Count);
			var foundFilesArray = foundFiles.Values.Select(val => val.FullName).ToArray();
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
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles);
			}

			Assert.Equal(3, foundFiles.Count);
		}

		[Fact(Skip = "not implemented")]
		public void GlobsFindFilesInSiblingDirs()
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig("john/quincy/*/*.txt");
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles);
			}

			Assert.Equal(3, foundFiles.Count);
			var foundFilesArray = foundFiles.Values.Select(val => val.FullName).ToArray();
			Assert.Contains(@"C:\john\quincy\adams\test.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\adams\allonym.txt", foundFilesArray);
			Assert.Contains(@"C:\john\quincy\doe\test.txt", foundFilesArray);
		}

		[Theory]
		[InlineData("john/*/*.txt")]
		[InlineData("**/quincy/**/*.txt")]
		[InlineData("john/**/doe/*.txt")]
		public void HelpfulErrorsForUnsupportedSyntax(string sourceExpression)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig(sourceExpression);
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				var e = Assert.Throws<NotImplementedException>(() => CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles));
				Assert.Contains("Please submit a pull request", e.Message);
				Assert.Contains(sourceExpression, e.Message);
			}
		}

		[Theory]
		[InlineData(".", @"jane\doe\test.txt")]
		[InlineData(@"jane\doe", @"test.txt", Skip = "for followup commit. Ship early; ship often.")] // TODO: implement absolute base path handling
		public void BasePathExcludedFromKeys(string basePath, string subPath)
		{
			var mockFileSystem = SetUpDirectoryStructure();
			var configJson = SetUpConfig(subPath, basePath);
			var foundFiles = new Dictionary<string, FileInfo>();

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var config = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();
				CommandUtilities.GetFilesFromConfiguration(config, mockFileSystem, foundFiles);
			}

			Assert.Single(foundFiles);
			Assert.Equal(@"C:\jane\doe\test.txt", foundFiles.Values.First().FullName);
			Assert.Equal(subPath, foundFiles.Keys.First());
		}
	}
}
