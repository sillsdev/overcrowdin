namespace Overcrowdin
{
	/// <remarks>
	/// https://support.crowdin.com/api/error-codes/
	/// </remarks>
	public enum CrowdinErrorCode
	{
		// ENHANCE (Hasso) 2020.01: add a method to extract these from an HttpResponseMessage (see CreateFolderCommand for sample code)
		DirectoryAlreadyExists = 50,
		DirectoryNameHasInvalidCharacters = 51
	}
}
