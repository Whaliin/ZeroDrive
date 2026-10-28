namespace ZeroDrive.Core.DTOs;

/// <summary>
/// Represents a file managed by ZeroDrive with its path, size and associated chunks.
/// </summary>
/// <param name="workspaceId">The unique identifier of the workspace this file belongs to.</param>
/// <param name="path">The path of the file.</param>
/// <param name="size">The size of the file in bytes.</param>
/// <param name="hash">The unique hash of the file's content.</param>
/// <param name="lastModified">The last modified timestamp of the file.</param>
/// <param name="chunks">List of <see cref="ZChunkMeta"/> data related to this file.</param>
public class ZFileInfo(Guid workspaceId, string path, long size, byte[] hash, DateTime lastModified, List<ZChunkMeta> chunks)
{
	/// <summary>
	/// The unique identifier of the workspace this file belongs to.
	/// </summary>
	public Guid WorkspaceId { get; init; } = workspaceId;

	/// <summary>
	/// The path of the file within the workspace.
	/// <em>
	/// Note: This path is relative to the workspace's root directory.
	/// </em>
	/// </summary>
	public string Path { get; init; } = path;

	/// <summary>
	/// The size of the file in bytes.
	/// </summary>
	public long Size { get; init; } = size;

	/// <summary>
	/// The unique hash of the file's content.
	/// <em>
	/// Note: This hash is computed based on the chunk hashes, not the entire file content.
	/// </em>
	/// </summary>
	public byte[] Hash { get; init; } = hash;

	/// <summary>
	/// The last modified timestamp of the file, in milliseconds since Unix Epoch.
	/// </summary>
	public DateTime LastModified { get; init; } = lastModified;

	/// <summary>
	/// List of <see cref="ZChunkMeta"/> data related to this file.
	/// </summary>
	public List<ZChunkMeta> Chunks { get; init; } = chunks;
}