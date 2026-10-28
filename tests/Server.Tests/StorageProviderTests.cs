using Microsoft.Extensions.Options;
using NSubstitute;
using ZeroDrive.Server;
using ZeroDrive.Server.Storage;

namespace Server.Tests;

// TODO: Fix this mess
public class SqliteStorageProviderTests : IAsyncLifetime
{
	private readonly IOptionsMonitor<SqliteStorageConfig> _optionsMonitor;
	private SQLiteStorageProvider storageProvider;
	private readonly string _tempDir;

	public SqliteStorageProviderTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "ZeroDriveTest");
		var dir = Directory.CreateDirectory(_tempDir);
		
		var _tempDbPath = Path.Combine(dir.FullName, "test_storage.db");

		// Set up the options monitor using NSub
		var optionsMonitor = Substitute.For<IOptionsMonitor<SqliteStorageConfig>>();
		var config = new SqliteStorageConfig { DatabasePath = _tempDbPath };
		optionsMonitor.CurrentValue.Returns(config);

		_optionsMonitor = optionsMonitor;
		storageProvider = new SQLiteStorageProvider(_optionsMonitor);
	}

	public Task DisposeAsync()
	{
		// Safely delete the tests directory after completion.
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);

		return Task.CompletedTask;
	}

	public Task InitializeAsync()
	{
		// Initialize the DI containers
		throw new NotImplementedException();
	}

	[Fact]
    public void StoreChunkTest()
    {
		storageProvider = new SQLiteStorageProvider(new OptionsWrapper<SqliteStorageConfig>(new SqliteStorageConfig { DatabasePath = "test_storage.db" }));
    }
}
