namespace OvercrowdinTests
{
	internal static class TestUtils
	{
		public static bool True(bool? actual)
		{
			return actual.HasValue && actual.Value;
		}

		public static bool FalseOrUnset(bool? actual)
		{
			return !actual.HasValue || !actual.Value;
		}
	}
}
