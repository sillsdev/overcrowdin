using System;
using System.Linq;
using Crowdin.Api.Typed;

namespace Overcrowdin
{
	public static class CrowdinExtensions
	{
		public static FileParameters ShallowClone(this FileParameters source)
		{
			var type = source.GetType();
			var ctor = type.GetConstructor(new Type[0]);
			// ReSharper disable once PossibleNullReferenceException - all implementations of FileParameters have a default constructor
			var dest = ctor.Invoke(new object[0]);
			var properties = type.GetProperties();
			foreach (var property in properties.Where(prop => prop.CanWrite)) // Skip any read-only properties
			{
				var value = property.GetValue(source);
				property.SetValue(dest, value);
			}
			return (FileParameters)dest;
		}
	}
}
