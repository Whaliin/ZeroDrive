using ZeroDrive.Core;
using ZeroDrive.Core.DTOs;
using ZeroDrive.Tests.Helpers;

namespace ZeroDrive.Core.Tests;

public class LocalWorkspaceMapTests : IDisposable
{
	private readonly TestDir dir = new();
	private readonly ZWorkspace ws = new(Guid.NewGuid(), "TestWorkspace");
	private readonly LocalWorkspaceMap lwsm;

	public LocalWorkspaceMapTests()
	{
		// Create a local workspace we can use; this also inherently tests the constructor for valid cases
		lwsm = new(dir.DirPath, ws);
	}

	public void Dispose()
	{
		dir.Dispose();
		GC.SuppressFinalize(this);
	}

	public static TheoryData<string> RelativePathData => new()
	{
		"testFileNoExt",
		"file.txt",
		Path.Combine("subdir", "subfile"),
		Path.Combine("subdir", "nested", "file.txt")

	};

	[Theory]
	[MemberData(nameof(RelativePathData))]
	public void Test_ConvertToRelativePath_ReturnsPathRelativeToWorkspace(string filePath)
	{
		Assert.Equal(filePath, lwsm.ConvertToRelativePath(dir.CreateTestPath(filePath)));
	}

	public static TheoryData<string> OutsidePathData => new()
	{
		Path.GetTempPath(),
		Path.Combine(Path.GetTempPath(), "outside.txt")
	};

	[Theory]
	[MemberData(nameof(OutsidePathData))]
	public void Test_ConvertToRelativePath_PathOutside(string testPath)
	{
		Assert.ThrowsAny<ArgumentException>(() => lwsm.ConvertToRelativePath(testPath));
	}

	[Fact]
	public void Test_IsFileInWorkspace_ValidFile()
	{
		var tp = dir.CreateTestPath("file.txt");
		Assert.True(lwsm.IsFileInWorkspace(tp));
	}

	[Fact]
	public void Test_IsFileInWorkspace_ValidNestedFile()
	{
		var tp = dir.CreateTestPath(Path.Combine("subdir", "nested.txt"));
		Assert.True(lwsm.IsFileInWorkspace(tp));
	}

	[Fact]
	public void Test_IsFileInWorkspace_OutsideDirectory()
	{
		using var otherDir = new TestDir();

		Assert.False(lwsm.IsFileInWorkspace(otherDir.CreateTestPath("test.txt")));
	}

	public static TheoryData<string> SimilarlyNamedFolders => new()
	{
		"_sibling",
		Path.Combine("_sibling", "siblingNested.txt")	
	};

	[Theory]
	[MemberData(nameof(SimilarlyNamedFolders))]
	public void Test_IsFileInWorkspace_SimilarlyNamedFolders(string pathExt)
	{
		// Get the path name of the testdir and append some string to it
		var dirPathWithoutTrailingSeparator = dir.DirPath.EndsWith(['/', '\\']) ? dir.DirPath[..^1] : dir.DirPath;
		var testPath = dirPathWithoutTrailingSeparator + pathExt;
		Assert.False(lwsm.IsFileInWorkspace(testPath));
	}

	[Fact]
	public void Test_FileExists_Does()
	{
		File.WriteAllText(dir.CreateTestPath("exists.txt"), "");
		Assert.True(lwsm.FileExists("exists.txt"));
	}

	[Fact]
	public void Test_FileExists_Not()
	{
		Assert.False(lwsm.FileExists("missing.txt"));
	}

	[Fact]
	public void Test_Ctor_NormalizeSeparator()
	{
		using var newTestDir = new TestDir();
		var testDirNoExtension = newTestDir.DirPath.EndsWith(['/', '\\']) ? newTestDir.DirPath[..^1] : newTestDir.DirPath;
		var testWs = new LocalWorkspaceMap(testDirNoExtension, ws);

		Assert.True(testWs.LocalPath.IsDirectoryPath());
	}

	public static TheoryData<string?> InvalidPaths => new()
	{
		null,
		"",
		new string(Path.GetInvalidPathChars())
	};

	[Theory]
	[MemberData(nameof(InvalidPaths))]
	public void Test_Ctor_InvalidPath(string? path)
	{
		Assert.ThrowsAny<InvalidOperationException>(() => new LocalWorkspaceMap(path!, ws));
	}
}