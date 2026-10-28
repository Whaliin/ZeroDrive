namespace ZeroDrive.Core;

public static class DateTimeExtensions
{
	extension(DateTime dt)
	{
		/// <summary>
		/// Convert a DateTime to Unix time in ms.
		/// <em>NOTE: Make sure that the time is UTC before calling this.</em>
		/// </summary>
		/// <returns></returns>
		public long ToUnixTimeMilliseconds() => new DateTimeOffset(dt).ToUnixTimeMilliseconds();
	}

	extension(long l)
	{
		public DateTime ToDateTimeFromUnixEpoch() => DateTimeOffset.FromUnixTimeMilliseconds(l).UtcDateTime; 
		// Alternative method:
		public DateTime ToDateTimeFromUnixEpoch2() => DateTime.UnixEpoch.AddMilliseconds(l);
	}
}

public static class StringExtensions
{
	// https://stackoverflow.com/a/78760919
	private const string WinLongPathPrefix = @"\\?\";

	extension(string str)
	{
		/// <summary>
		/// Shorthand helper for Path.EndsInDirectorySeparator(str)
		/// </summary>
		/// <returns></returns>
		public bool IsDirectoryPath() => Path.EndsInDirectorySeparator(str);
		
		/// <summary>
		/// Check that a path is valid; not null/empty, has no invalid path characters, is fully qualified, and contains a root.
		/// </summary>
		/// <returns></returns>
		public bool IsValidPath()
		{
			if (string.IsNullOrWhiteSpace(str))
				return false;

			// Strip the Windows long-path prefix before validating
			var stripped = str;
			if (OperatingSystem.IsWindows() && str.StartsWith(WinLongPathPrefix, StringComparison.Ordinal))
				stripped = str[WinLongPathPrefix.Length..];

			if (stripped.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
				return false;

			if (!Path.IsPathFullyQualified(stripped))
				return false;

			string? root = Path.GetPathRoot(stripped);
			if (root is null || !Environment.GetLogicalDrives().Contains(root))
				return false;

			return true;
		}

		/// <summary>
		/// Checks that a path is valid, is not a directory, and that it doesn't contain any invalid filename characters.
		/// </summary>
		/// <returns></returns>
		public bool IsValidFilePath()
		{
			// First, check the path
			if (!str.IsValidPath())
				return false;

			// Check if it's a directory
			if (str.IsDirectoryPath())
				return false;

			// Finally, check if it contains any invalid characters
			var fn = Path.GetFileName(str);
			// The null check here is not strictly neccessary but guards us from any strange exceptions.
			if (fn == "" || fn?.ContainsAny(Path.GetInvalidFileNameChars()) != false)
				return false;

			return true;
		}
	}
}