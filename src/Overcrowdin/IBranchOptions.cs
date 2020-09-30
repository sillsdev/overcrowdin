namespace Overcrowdin
{
	/// <summary>
	/// Options that apply to verbs that can operate on specific version branches
	/// </summary>
	public interface IBranchOptions
	{
		string Branch { get; set; }
	}
}
