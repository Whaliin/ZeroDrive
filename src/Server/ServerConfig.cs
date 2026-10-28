namespace ZeroDrive.Server;

/// <summary>
/// Interface for server configurations.
/// Each configuration type should implement this interface to ensure type safety.
/// </summary>
/// <typeparam name="IStorageProvider"></typeparam>
public interface IStorageConfig {}

/// <summary>
/// Configuration for file-based storage provider.
/// </summary>
public class FileStorageConfig : IStorageConfig
{
	public string StorageDirectory { get; set; } = "blobs";
	public string FileDataPath { get; set; } = "manifest.json";
}

/// <summary>
/// Configuration for SQLite-based storage provider.
/// </summary>
public class SqliteStorageConfig : IStorageConfig
{
	public string DatabasePath { get; set; } = "storage.db";
}

public enum StorageProviderType
{
	FileSystem,
	SQLite
}

public class ServerConfig
{
	public StorageProviderType StorageProviderType { get; set; } = StorageProviderType.SQLite;
	public FileStorageConfig? FileStorage { get; set; }
	public SqliteStorageConfig? SQLiteStorage { get; set; }
	public int? MaxConcurrentUploads { get; set; }
	public int? MaxConcurrentDownloads { get; set; }
	public int? GRPCMaxMessageSize { get; set; }
}