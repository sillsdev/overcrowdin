using System.Collections.Generic;

namespace Overcrowdin
{
	/// <summary>
	/// Options that apply to every verb
	/// </summary>
	public interface IFileOptions
	{
		IEnumerable<string> Files { get; set; }
	}
}
