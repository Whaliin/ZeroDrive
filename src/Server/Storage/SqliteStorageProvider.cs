namespace ZeroDrive.Server.Storage;

using ZeroDrive.Core.Types;
using Microsoft.Data.Sqlite;
using RepoDb;
using Microsoft.Extensions.Options;

public class SQLiteStorageProvider : ConfigurableStorageProviderBase<SqliteStorageConfig>, IStorageProvider
{
	private class ZChunkEntity
	{
		// Primary key (relationship with ZChunkMetaEntity)
		public string Hash { get; set; } = string.Empty;
		public byte[] Data { get; set; } = [];

		public static explicit operator ZChunkEntity(ZChunk chunk) => new()
		{
			Hash = chunk.Meta.Hash,
			// TODO: Zstd compression can be applied here if needed, or perhaps it should be handled by the base storage provider?
			Data = chunk.Data
		};

		// We can not explicitly convert from ZChunkEntity because this would require the ChunkMeta (which is stored in a separate table)
	}

	private class ZChunkMetaEntity
	{
		// Primary key (relationship with ZChunkEntity)
		public string Hash { get; set; } = string.Empty;
		// SQLite does not support ulong type so we must convert it to long for storage.
		public long ChunkIndex { get; set; }
		public long Size { get; set; }

		public static explicit operator ZChunkMetaEntity(ZChunkMeta chunkMeta) => new()
		{
			Hash = chunkMeta.Hash,
			ChunkIndex = (long)chunkMeta.Index,
			Size = (long)chunkMeta.Size
		};

		public static explicit operator ZChunkMeta(ZChunkMetaEntity entity) => new(
				entity.Hash,
				(ulong)entity.ChunkIndex,
				(ulong)entity.Size
			);
	}

	private class ZFileInfoEntity
	{
		public string WorkspaceId { get; set; } = string.Empty;
		public string Path { get; set; } = string.Empty;
		public string FileHash { get; set; } = string.Empty;
		public long Size { get; set; }
		public long LastModified { get; set; }
		
		public static explicit operator ZFileInfoEntity(ZFileInfo fileInfo) => new() 
		{
			WorkspaceId = fileInfo.WorkspaceId,
			Path = fileInfo.Path,
			FileHash = fileInfo.Hash,
			Size = (long)fileInfo.Size,
			LastModified = new DateTimeOffset(fileInfo.LastModified).ToUnixTimeMilliseconds()
		};

		public static explicit operator ZFileInfo(ZFileInfoEntity entity) => new(
				entity.WorkspaceId,
				entity.Path,
				(ulong)entity.Size,
				entity.FileHash,
				DateTimeOffset.FromUnixTimeMilliseconds(entity.LastModified).UtcDateTime,
				[]);
	}

	private class ZWorkspaceDataEntity
	{
		// Primary key (relationship with ZFileInfoEntity)
		public string WorkspaceId { get; set; } = string.Empty;
		public string WorkspaceName { get; set; } = string.Empty;
	}

	private readonly string _connectionString;

	public SQLiteStorageProvider(IOptionsMonitor<SqliteStorageConfig> options) : base(options)
	{
		var connectionStringBuilder = new SqliteConnectionStringBuilder
		{
			DataSource = Configuration.DatabasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Cache = SqliteCacheMode.Shared,
			ForeignKeys = true
		};
		_connectionString = connectionStringBuilder.ConnectionString;
	}

	public async Task InitializeAsync()
	{
		// Initialize RepoDb for SQLite
		RepoDb.GlobalConfiguration.Setup()
			.UseSqlite();

		// Set up the SQLite database and create tables if they do not exist
		using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync();

		// Set the journal mode to WAL (Write-Ahead Logging) once on initialization.
		await connection.ExecuteNonQueryAsync("PRAGMA journal_mode = WAL;");

		// Create tables if they do not exist
		var createChunkTablesSql = @"
			CREATE TABLE IF NOT EXISTS ZChunks (
				Hash TEXT PRIMARY KEY,
				Size INTEGER NOT NULL,
				Data BLOB NOT NULL
			);
			CREATE TABLE IF NOT EXISTS ZWorkspaces (
				WorkspaceId TEXT PRIMARY KEY,
				WorkspaceName TEXT NOT NULL
			);
			CREATE TABLE IF NOT EXISTS ZFileInfos (
				WorkspaceId TEXT NOT NULL,
				Path TEXT NOT NULL,
				FileHash TEXT NOT NULL,
				Size INTEGER NOT NULL,
				LastModified INTEGER NOT NULL,
				PRIMARY KEY (WorkspaceId, Path),
				FOREIGN KEY (WorkspaceId) REFERENCES ZWorkspaces(WorkspaceId) ON DELETE CASCADE
			);
			CREATE TABLE IF NOT EXISTS ZChunkMetas (
				WorkspaceId TEXT NOT NULL,
				Hash TEXT NOT NULL,
				Path TEXT NOT NULL,
				ChunkIndex INTEGER NOT NULL,
				PRIMARY KEY (WorkspaceId, Path, ChunkIndex),
				FOREIGN KEY (WorkspaceId, Path) REFERENCES ZFileInfos(WorkspaceId, Path) ON DELETE CASCADE,
				FOREIGN KEY (Hash) REFERENCES ZChunks(Hash) ON DELETE NO ACTION
			);

			CREATE INDEX IF NOT EXISTS IDX_ZChunkMetas_Hash ON ZChunkMetas(Hash);
			";
		await connection.ExecuteNonQueryAsync(createChunkTablesSql);
	}

	public Task<bool> ChunkExists(string hash)
	{
		throw new NotImplementedException();
	}

	public Task<byte[]> RetrieveChunk(string hash)
	{
		throw new NotImplementedException();
	}

	public Task<bool> StoreChunk(string hash, byte[] chunk)
	{
		throw new NotImplementedException();
	}

	public Task<bool> FileExists(string filePath)
	{
		throw new NotImplementedException();
	}

	public Task<ZFileInfo> RetrieveFileInfo(string filePath)
	{
		throw new NotImplementedException();
	}

	public Task StoreFileInfo(ZFileInfo fileInfo)
	{
		throw new NotImplementedException();
	}

	public Task<ZWorkspace> RetrieveManifest(string workspaceId)
	{
		throw new NotImplementedException();
	}

	public Task StoreManifest(ZWorkspace manifestData)
	{
		throw new NotImplementedException();
	}
}