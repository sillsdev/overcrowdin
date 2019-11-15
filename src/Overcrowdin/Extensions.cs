using Microsoft.Extensions.Configuration;

namespace Overcrowdin
{
	public static class Extensions
	{
		public static T GetConfigValue<T>(this IConfiguration config, string key)
		{
			return config.GetSection(key).Get<T>();
		}
	}
}
