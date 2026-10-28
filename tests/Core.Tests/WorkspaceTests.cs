using ZeroDrive.Core.DTOs;

namespace ZeroDrive.Core.Tests;

[Collection("ZWorkspaceData")]
public class WorkspaceDataTests
{
	public static TheoryData<Guid, string?> InvalidConstructors => new()
	{
		// Guid instanced, but invalid
		{Guid.Empty, "TestWs"},
		// Valid guid, invalid name
		{Guid.NewGuid(), null},
		// Valid guid, valid name
		{Guid.NewGuid(), "TestWs"},
		// Valid guid, invalid path chars
		{Guid.NewGuid(), Path.GetInvalidPathChars().ToString()}
	};

	[Theory]
	[MemberData(nameof(InvalidConstructors))]
	public void Test_WorkspaceData_Ctor(Guid guid, string? name)
	{
		// Arrange

		// Act

		// Assert
		Assert.ThrowsAny<Exception>(() => new ZWorkspace(guid, name!));
	}

	[Fact]
	public void Test_New_Workspace_Takes_Explicit_Guid()
	{
		Assert.NotNull(new ZWorkspace(Guid.NewGuid(), "TestWs"));
	}
}