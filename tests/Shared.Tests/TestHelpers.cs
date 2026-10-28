namespace ZeroDrive.Tests.Helpers;

/// <summary>
/// Disposable testdir (so we can use the "using" statement to create a very fast-lived temp dir)
/// </summary>
public class TestDir() : IDisposable 
{
	public DirectoryInfo Dir = Directory.CreateTempSubdirectory();
	public string DirPath => Dir.FullName;

	public string CreateTestPath(string fileName) => Path.Combine(DirPath, fileName);

	public void Dispose()
	{
		Directory.Delete(Dir.FullName, true);
		GC.SuppressFinalize(this);
	}
}

public static class TestHelpers
{
	/// <summary>
	/// Generate a random byte sequence with defined size
	/// </summary>
	/// <param name="size"></param>
	/// <returns></returns>
	public static byte[] GenerateRandomData(int size)
	{
		var random = new Random();
		var data = new byte[size];
		random.NextBytes(data);
		return data;
	}

	/// <summary>
	/// Generate a random Stream with defined size
	/// </summary>
	/// <param name="size"></param>
	/// <returns></returns>
	public static MemoryStream GenerateRandomStream(int size)
	{
		var data = GenerateRandomData(size);
		return new MemoryStream(data);
	}

	/// <summary>
	/// Generates a temporary file with random data of the specified size and returns the file path.
	/// </summary>
	/// <param name="size"></param>
	/// <returns>The path to the generated temporary file. Note that you must delete this file yourself.</returns>
	public static string GenerateRandomFile(int size, string filePath = "")
	{
		var tempFilePath = filePath == "" ? Path.GetTempFileName() : filePath;
		var data = GenerateRandomData(size);
		File.WriteAllBytes(tempFilePath, data);
		return tempFilePath;
	}
}