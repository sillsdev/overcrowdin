using CommandLine;

namespace Overcrowdin
{
	/// <summary>
	/// Options that apply to every verb
	/// </summary>
	public class GlobalOptions
	{
		[Option('v', HelpText = "Show verbose output.")]
		public bool Verbose { get; set; }
	}
}