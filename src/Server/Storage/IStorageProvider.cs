namespace ZeroDrive.Server.Storage;

using ZeroDrive.Core;
using ZeroDrive.Core.Types;

/// <summary>
/// Defines the interface for a storage provider that handles chunk and file storage operations.
/// (For local file system or SQL database storage, etc.)
/// </summary>
public interface IStorageProvider
{
	/// <summary>
	/// Asynchronously initializes the storage provider, preparing it for operations such as chunk and file management.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation.</returns>
	Task InitializeAsync();

#region Chunk operations
	Task<bool> ChunkExists(string hash);
	Task<ReadOnlyMemory<byte>> RetrieveChunk(string hash);
	Task StoreChunk(ZChunk chunk);

	/// <summary>
	/// Removes orphaned chunks from the storage provider. This is considered a time-consuming operation.
	/// </summary>
	/// <returns>The number of orphaned chunks that were removed.</returns>
	Task<int> PurgeOrphanedChunksAsync();
#endregion

#region File operations
	/// <summary>
	/// Checks if a file exists in the storage provider for a given workspace and file path.
	/// </summary>
	/// <param name="workspaceId">The ID of the workspace containing the file.</param>
	/// <param name="filePath">The path of the file to check.</param>
	/// <returns></returns>
	Task<bool> FileExists(string workspaceId, string filePath);

	/// <summary>
	/// Retrieves the file information for a given workspace and file path.
	/// </summary>
	/// <param name="workspaceId">The ID of the workspace containing the file.</param>
	/// <param name="filePath">The path of the file to retrieve.</param>
	/// <returns>ZFileInfo representing the file information.</returns>
	Task<ZFileInfo> RetrieveFileInfo(string workspaceId, string filePath);

	/// <summary>
	/// Deletes the file information for a given workspace and file path.
	/// <em>Note: This should NOT trigger an automatic orphan removal.</em>
	/// </summary>
	/// <param name="workspaceId">The ID of the workspace containing the file.</param>
	/// <param name="filePath">The path of the file to delete.</param>
	/// <returns></returns>
	Task DeleteFileInfo(string workspaceId, string filePath);
	
	/// <summary>
	/// Stage a file for upload (store it's metadata and chunk references, without any data).
	/// </summary>
	/// <param name="fileInfo">The file metadata to stage</param>
	/// <returns>List of missing chunk references that need to be uploaded by the client.</returns>
	Task<IReadOnlyList<string>> StageFileInfoAsync(ZFileInfo fileInfo);

	/// <summary>
	/// Commit a staged file, marking it as fully uploaded and available for retrieval.
	/// </summary>
	/// <param name="workspaceId">The workspace ID of the file to commit.</param>
	/// <param name="filePath">The path of the file to commit.</param>
	/// <returns>A list of missing chunk references that need to be uploaded by the client. Empty if all chunks are available. <em>Note: If this list is not empty, the file will NOT be committed to storage.</em></returns>
	Task<IReadOnlyList<string>> CommitFileInfoAsync(string workspaceId, string filePath);
#endregion

#region Manifest operations for workspaces
	/// <summary>
	/// Retrieves the manifest data for a given workspace, which includes the list of files and their metadata.
	/// </summary>
	/// <param name="workspaceId"></param>
	/// <returns></returns>
	Task<ZWorkspace> RetrieveManifest(string workspaceId);

	/// <summary>
	/// Stores a complete workspace manifest, replacing any existing manifest for the given workspace.
	/// </summary>
	/// <param name="manifestData">The manifest data to store.</param>
	/// <returns></returns>
	Task StoreManifest(ZWorkspace manifestData);

	/// <summary>
	/// Deletes a workspace and all associated files from the storage provider.
	/// </summary>
	/// <param name="workspaceId">The workspace ID to remove.</param>
	/// <returns></returns>
	Task DeleteWorkspace(string workspaceId);
#endregion
	/*
	Client:
		* Detect file changes (both from server and local, transmit vs receive)
		* Upload new files
		* Upload modifications to existing files
		* Download files from server
		* Download modifications from server
	
	Server:
		* Accept new files
		* Accept modifications to files
		* Give the client file and folder information
		* ^ Send updated data to client
	*/
}