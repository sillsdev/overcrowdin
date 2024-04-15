// // Copyright (c) $year$ SIL International
// // This software is licensed under the LGPL, version 2.1 or later
// // (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using Crowdin.Api;

namespace Overcrowdin
{
	public class CrowdinApiFactory : ICrowdinClientFactory
	{
		public ICrowdinApiClient Create(string apiKey)
		{
			return new CrowdinApiClient(new CrowdinCredentials { AccessToken = apiKey });
		}
	}
}