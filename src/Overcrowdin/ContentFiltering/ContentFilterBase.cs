using System.IO;
using System.IO.Abstractions;

namespace Overcrowdin.ContentFiltering
{
	public abstract class ContentFilterBase
	{
		public abstract string FileExtension { get; }

		public abstract bool IsLocalizable(IFileSystem fs, string path, params object[] args);

		public virtual bool CanVerify(string path, params object[] args)
		{
			return FileExtension.Equals(Path.GetExtension(path));
		}
	}
}
