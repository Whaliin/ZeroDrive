namespace ZeroDrive.Server.Storage;

using ZeroDrive.Core;
using System.Text.Json;

public class FileStorageProvider : IStorageProvider
{
	private readonly string _storageDirectory;
	private readonly string _fileDataPath;

	private readonly Dictionary<string, ZFileInfo> _fileInfoCache = [];

	private void UpdateManifestData()
	{
		var json = JsonSerializer.Serialize(_fileInfoCache.Values);
		File.WriteAllText(_fileDataPath, json);
	}

	private void LoadManifestData()
	{
		if (!File.Exists(_fileDataPath))
		{
			return;
		}

		var json = File.ReadAllText(_fileDataPath);
		var fileInfos = JsonSerializer.Deserialize<List<ZFileInfo>>(json) ?? [];
		foreach (var fileInfo in fileInfos)
		{
			_fileInfoCache[fileInfo.Path] = fileInfo;
		}
	}

	public FileStorageProvider(FileStorageConfig config)
	{
		_storageDirectory = config.StorageDirectory;
		Directory.CreateDirectory(_storageDirectory);
		_fileDataPath = Path.Combine(_storageDirectory, config.FileDataPath);
		LoadManifestData();
	}

	public async Task<bool> ChunkExists(string hash)
	{
		if (string.IsNullOrWhiteSpace(hash))
		{
			throw new ArgumentException("Hash cannot be null or whitespace.", nameof(hash));
		}

		var chunkPath = Path.Combine(_storageDirectory, hash);
		return File.Exists(chunkPath);
	}

	public async Task<bool> FileExists(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
		}

		var fullPath = Path.Combine(_storageDirectory, filePath);
		return File.Exists(fullPath);
	}

	public async Task<byte[]> RetrieveChunk(string hash)
	{
		if (string.IsNullOrWhiteSpace(hash))
		{
			throw new ArgumentException("Hash cannot be null or whitespace.", nameof(hash));
		}

		var chunkPath = Path.Combine(_storageDirectory, hash);
		if (!File.Exists(chunkPath))
		{
			throw new FileNotFoundException($"Chunk with hash {hash} not found.", chunkPath);
		}

		// NOTE: This could be optimized to use streaming
		return await File.ReadAllBytesAsync(chunkPath);
	}

	public async Task<ZFileInfo> RetrieveFileInfo(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
		}

		if (_fileInfoCache.TryGetValue(filePath, out var cachedInfo))
		{
			return cachedInfo;
		}

		throw new FileNotFoundException($"File info for {filePath} not found in storage.");
	}

	public async Task<bool> StoreChunk(string hash, byte[] chunk)
	{
		if (string.IsNullOrWhiteSpace(hash))
		{
			throw new ArgumentException("Hash cannot be null or whitespace.", nameof(hash));
		}

		if (chunk == null || chunk.Length == 0)
		{
			throw new ArgumentException("Chunk data cannot be null or empty.", nameof(chunk));
		}

		var chunkPath = Path.Combine(_storageDirectory, hash);
		await File.WriteAllBytesAsync(chunkPath, chunk);
		return true;
	}

	public async Task StoreFileInfo(ZFileInfo fileInfo)
	{
		_fileInfoCache[fileInfo.Path] = fileInfo;

		UpdateManifestData();
	}

	public Task<ZWorkspaceData> RetrieveManifest(string directoryPath)
	{
		throw new NotImplementedException();
	}
}