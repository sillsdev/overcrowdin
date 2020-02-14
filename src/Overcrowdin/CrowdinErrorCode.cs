namespace Overcrowdin
{
	/// <remarks>
	/// https://support.crowdin.com/api/error-codes/
	/// </remarks>
	public enum CrowdinErrorCode
	{
		// ENHANCE (Hasso) 2020.01: add a method to extract these from an HttpResponseMessage (see CreateFolderCommand for sample code)
		/// <summary>"success" XML returned from Crowdin</summary>
		Success = -2, // chosen to avoid a collision with "unknown internal error"
		/// <summary>Unable to parse XML, or the Root element was neither "success" nor "error"</summary>
		NoError = -1, // chosen to avoid a collision with "unknown internal error"
		UnknownInternalError = 0,
		ProjectDoesntExistOrInvalidKey = 1,
		UnknownApiAction = 2,
		InvalidKey = 3,
		AccountDoesntExist = 11,
		InvalidAccountKey = 12,
		NoUserIdSpecified = 18,
		UserNatFound = 20,
		InvalidParameters = 27,
		NoUsernameSpecified = 37,
		InvalidParameterValues = 38,
		PermissionDenied = 41,
		ProjectSuspended = 43,
		NoUserIdOrUsername = 44,
		DirectoryAlreadyExists = 50,
		DirectoryNameHasInvalidCharacters = 51,
		ProjectWasNotBuilt = 54, // the build was canceled or interrupted
		NoStringsFound = 56,
		NoTranslationsFound = 57
	}
}
