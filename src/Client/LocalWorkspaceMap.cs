using ZeroDrive.Core;
using ZeroDrive.Core.DTOs;

namespace ZeroDrive.Core;

public class LocalWorkspaceMap
{
	public LocalWorkspaceMap(string LocalPath, ZWorkspace Workspace)
	{
		this.LocalPath = LocalPath;
		this.Workspace = Workspace;
	}

	public LocalWorkspaceMap(string LocalPath, string WorkspaceName)
	{
		this.LocalPath = LocalPath;
		Workspace = new ZWorkspace(Guid.NewGuid(), WorkspaceName);
		// Infer the workspace ctor args from the path?
		// Workspace = new ZWorkspace(Guid.NewGuid(), Path.GetFileName(Path.TrimEndingDirectorySeparator(LocalPath)));
	}

	public LocalWorkspaceMap(string LocalPath)
	{
		this.LocalPath = LocalPath;
		// Infer the workspace ctor args from the path?
		Workspace = new ZWorkspace(Guid.NewGuid(), Path.GetFileName(Path.TrimEndingDirectorySeparator(LocalPath)));
	}

	public string LocalPath 
	{ 
		get;
		set
		{
			// Check that the path is valid
			if (!value.IsValidPath())
				throw new InvalidOperationException("Path must be valid");

			if (!value.IsDirectoryPath())
				value += Path.DirectorySeparatorChar;

			// TODO: Run File.Move() maybe?
			// NOPE! This is managed by WorkspaceService.

			field = value;
		}
	}

	public ZWorkspace Workspace { get; set; }

	/// <summary>
	/// Check if a given local file path belongs to this workspace mapping.
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public bool IsFileInWorkspace(string filePath)
	{
		if (!filePath.IsValidFilePath())
			return false;

		// Example:
		// 	LocalPath: X:\ZeroDrive\Workspace1\
		// 		Input: X:\Other\file.txt = FALSE
		// 		Input: X:\ZeroDrive\Workspace1_backup\file.txt = FALSE
		// 		Input: X:\ZeroDrive\Workspace1\file.txt = TRUE
		// 		Input: X:\ZeroDrive\Workspace1\Subdir\Subsubdir\file.txt = TRUE
		if (!filePath.StartsWith(LocalPath))
			return false;

		return true;
	}

	/// <summary>
	/// Convert a local file path to a relative path
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public string ConvertToRelativePath(string filePath)
	{
		// 	LocalPath: X:\ZeroDrive\Workspace1\
		// 		Input: X:\Other\file.txt
		// 	   Output: error
		// 		Input: X:\ZeroDrive\Workspace1_backup\file.txt
		// 	   Output: error
		// 		Input: X:\ZeroDrive\Workspace1\file.txt
		// 	   Output: file.txt
		// 		Input: X:\ZeroDrive\Workspace1\Subdir\Subsubdir\file.txt
		// 	   Output: Subdir\Subsubdir\file.txt

		if (!IsFileInWorkspace(filePath))
			throw new ArgumentException("File is not in workspace");

		// Convert \ into / before returning
		return filePath.Substring(LocalPath.Length).Replace('\\', '/');
	}

	/// <summary>
	/// Check if a relative file path exists on disk.
	/// </summary>
	/// <param name="relativePath"></param>
	/// <returns></returns>
	public bool FileExists(string relativePath)
	{
		// Convert the relative path to absolute path
		var absolutePath = this.LocalPath + relativePath;
		return File.Exists(absolutePath);
	}
}