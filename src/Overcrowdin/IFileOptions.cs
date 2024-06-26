using System.Collections.Generic;

namespace Overcrowdin
{
	/// <summary>
	/// Options that apply to file-related verbs
	/// </summary>
	public interface IFileOptions : IBranchOptions
	{
		IEnumerable<string> Files { get; set; }
	}
}