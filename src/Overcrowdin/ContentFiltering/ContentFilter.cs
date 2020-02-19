using System.IO;
using System.IO.Abstractions;

namespace Overcrowdin.contentFiltering
{
	public abstract class ContentFilter
	{
		public abstract string FileExtension { get; }

		public abstract bool IsLocalizable(IFileSystem fs, string path, params object[] args);

		public virtual bool CanVerify(string path)
		{
			return FileExtension.Equals(Path.GetExtension(path));
		}
	}
}
