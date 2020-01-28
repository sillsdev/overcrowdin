using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using IDirectoryInfo = System.IO.Abstractions.IDirectoryInfo;
using IFileInfo = System.IO.Abstractions.IFileInfo;

namespace Overcrowdin
{
	public class DirectoryInfoWrapper : DirectoryInfoBase
	{
		private readonly IDirectoryInfo _directoryInfo;

		public override string Name => _directoryInfo.Name;
		public override string FullName => _directoryInfo.FullName;

		public DirectoryInfoWrapper(IDirectoryInfo directoryInfo)
		{
			_directoryInfo = directoryInfo;
		}

		public override DirectoryInfoBase ParentDirectory
		{
			get
			{
				var parent = _directoryInfo.Parent;
				return parent == null ? null : new DirectoryInfoWrapper(parent);
			}
		}

		public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
		{
			foreach (var info in _directoryInfo.EnumerateFileSystemInfos())
			{
				switch (info)
				{
					case IFileInfo fileInfo:
						yield return new FileInfoWrapper(new FileInfo(fileInfo.FullName));
						break;
					case IDirectoryInfo dirInfo:
						yield return new DirectoryInfoWrapper(dirInfo);
						break;
				}
			}
		}

		public override DirectoryInfoBase GetDirectory(string path)
		{
			var info = _directoryInfo.FileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(FullName, path));
			return new DirectoryInfoWrapper(info);
		}

		public override FileInfoBase GetFile(string path)
		{
			return new FileInfoWrapper(new FileInfo(Path.Combine(FullName, path)));
		}
	}
}
