using System.Linq;
using Crowdin.Api.Typed;

namespace Overcrowdin
{
	public static class CrowdinExtensions
	{
		public static T ShallowClone<T>(this T source) where T : FileParameters, new()
		{
			var type = source.GetType();
			var target = new T();
			var properties = type.GetProperties();
			foreach (var property in properties.Where(prop => prop.CanWrite)) // Skip any read-only properties
			{
				var value = property.GetValue(source);
				property.SetValue(target, value);
			}
			return target;
		}
	}
}
