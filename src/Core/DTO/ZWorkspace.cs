namespace ZeroDrive.Core.DTOs;

/// <summary>
/// 
/// </summary>
/// <param name="Id">Unique Id of the workspace</param>
/// <param name="WorkspaceName">The name of the workspace</param>
/// <param name="ExtensionExclusions"></param>
/// <param name="Files"></param>
public sealed record ZWorkspace(Guid Id, string WorkspaceName, IReadOnlyList<string>? ExtensionExclusions = null, IReadOnlyList<ZFileInfo>? Files = null)
{
	/// <summary>
	/// Extensions to ignore for this workspace.
	/// </summary>
	public IReadOnlyList<string> ExtensionExclusions { get; init; } = ExtensionExclusions ?? [];

	/// <summary>
	/// The list of files in the workspace, represented as <see cref="ZFileInfo"/> objects.
	/// </summary>
	public IReadOnlyList<ZFileInfo> Files { get; init; } = Files ?? [];
}