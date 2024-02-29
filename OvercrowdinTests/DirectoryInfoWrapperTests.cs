using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Xunit;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using IFileSystem = System.IO.Abstractions.IFileSystem;
using DirectoryInfoWrapper = Overcrowdin.DirectoryInfoWrapper;

namespace OvercrowdinTests
{
	public class DirectoryInfoWrapperTests
	{
		private static readonly IFileSystem FileSystem = new MockFileSystem();

		[Theory]
		[InlineData(@"C:", @"C:\")]
		[InlineData(@"C:\testDir\", @"C:\testDir")]
		public static void FullName(string input, string normalized)
		{
			var sut = new DirectoryInfoWrapper(FileSystem.DirectoryInfo.New(input));
			Assert.Equal(normalized, sut.FullName);
		}

		[Theory]
		[InlineData(@"C:\", "")]
		[InlineData(@"C:\superDir\subDir", "subDir")]
		public static void Name(string fullName, string name)
		{
			var sut = new DirectoryInfoWrapper(FileSystem.DirectoryInfo.New(fullName));
			Assert.Equal(name, sut.Name);
		}

		[Theory]
		[InlineData(@"C:\", null)]
		[InlineData(@"C:\superDir\subDir", @"C:\superDir")]
		public static void ParentDirectory(string path, string parent)
		{
			var sut = new DirectoryInfoWrapper(FileSystem.DirectoryInfo.New(path));
			Assert.Equal(parent, sut.ParentDirectory?.FullName);
		}

		[Fact]
		public static void GetDirectory()
		{
			const string superDir = @"C:\superDir";
			const string subDir = "subDir";
			var sut = new DirectoryInfoWrapper(FileSystem.DirectoryInfo.New(superDir));
			var result = sut.GetDirectory(subDir);
			Assert.Equal(Path.Combine(superDir, subDir), result.FullName);
			// The subdirectory is not required to exist. It does not in this test, and it should not be created.
			Assert.False(FileSystem.Directory.Exists(result.FullName));
		}

		[Fact]
		public static void GetFile()
		{
			const string directory = @"C:\dir";
			const string file = "file.txt";
			var sut = new DirectoryInfoWrapper(FileSystem.DirectoryInfo.New(directory));
			var result = sut.GetFile(file);
			Assert.Equal(Path.Combine(directory, file), result.FullName);
		}

		[Fact]
		public static void EnumerateFileSystemInfos()
		{
			var fileSys = new MockFileSystem(new MockFileSystemOptions { CreateDefaultTempDir = false });
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

			var sut = new DirectoryInfoWrapper(fileSys.DirectoryInfo.New("/"));
			var results = sut.EnumerateFileSystemInfos().ToArray();

			Assert.Equal(3, results.Length);
			Assert.Contains(results, info => IsFileInfoMatching(info, @"C:\test.txt"));
			Assert.Contains(results, info => IsDirInfoMatching(info, @"C:\jane"));

			var john = (DirectoryInfoBase)results.First(info => IsDirInfoMatching(info, @"C:\john"));
			Assert.Equal(3, john.EnumerateFileSystemInfos().Count());

			var adams = john.GetDirectory("quincy").GetDirectory("adams");
			var adamses = adams.EnumerateFileSystemInfos().ToArray();
			Assert.Equal(2, adamses.Length);
			Assert.Contains(adamses, info => IsFileInfoMatching(info, @"C:\john\quincy\adams\test.txt"));
			Assert.Contains(adamses, info => IsFileInfoMatching(info, @"C:\john\quincy\adams\allonym.txt"));
		}

		private static bool IsFileInfoMatching(FileSystemInfoBase info, string path)
		{
			return info is FileInfoBase && info.FullName.Equals(path);
		}

		private static bool IsDirInfoMatching(FileSystemInfoBase info, string path)
		{
			return info is DirectoryInfoBase && info.FullName.Equals(path);
		}
	}
}