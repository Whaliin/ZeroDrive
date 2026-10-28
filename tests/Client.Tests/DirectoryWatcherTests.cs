using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeroDrive.Core.Services;
using ZeroDrive.Tests.Helpers;

namespace ZeroDrive.Core.Tests;

public class DirectoryWatcherTests : IAsyncLifetime
{
	private readonly DirectoryWatcherService dws;
	private readonly Channel<DirtyFileInfo> eventChannel;
	private readonly DirectoryWatcherOptions options;

	private const int TestFileSize = 100;
	private int DebounceTimeoutOffset;
	private int TimeToWait;

	public DirectoryWatcherTests()
	{
		eventChannel = Channel.CreateBounded<DirtyFileInfo>(new BoundedChannelOptions(100)
		{
			FullMode = BoundedChannelFullMode.Wait
		});

		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();

		var factory = sp.GetService<ILoggerFactory>() ?? throw new Exception("Could not create ILogger?!");

		var logger = factory.CreateLogger<DirectoryWatcherService>();

		options = new()
		{
			// Test a shorter debounce window so tests finish faster.
			DebounceTimeoutMs = 250,
			PushLoopDelayMs = 10,
		};

		// Ensure that our configured DebounceTimeoutOffset is at least within the push loop delay.
		DebounceTimeoutOffset = (options.PushLoopDelayMs*2)+20;
		TimeToWait = options.DebounceTimeoutMs + DebounceTimeoutOffset;

		dws = new DirectoryWatcherService(eventChannel, logger, options);
	}

	public async Task DisposeAsync()
	{
		dws.Dispose();
	}

	public async Task InitializeAsync()
	{
		// Make sure the channel writer is started
		// await dws.StartAsync(CancellationToken.None);
	}

	[Fact]
    public void TestDWSWatchNullDirPath()
    {
		// Arrange

		// Act

		// Assert
		Assert.ThrowsAny<Exception>(() => dws.AddWatch(null!));
    }

	public static TheoryData<string> InvalidPaths
	{
		get
		{
			var data = new TheoryData<string>();

			foreach (var ic in Path.GetInvalidPathChars())
			{
				data.Add(ic.ToString());
			}

			return data;
		}
	}

	[Theory]
	[MemberData(nameof(InvalidPaths))]
	public void TestDWSWatchInvalidDirPath(string path)
	{
		Assert.ThrowsAny<Exception>(() => dws.AddWatch(path));
	}

	[Fact]
	public void TestDWSWatchValidDir()
	{
		// Arrange
		using var dir = new TestDir();

		// Act
		dws.AddWatch(dir.DirPath);	

		// Assert
		Assert.True(dws.RemoveWatch(dir.DirPath));
	}

/* 
	We shouldn't allow the users to specify what types of event to watch, 
	because this inherently breaks the concept of our storage system.
*/
	[Fact]
	public async Task TestDWSWatchCreateEvent()
	{
		// Arrange
		using var dir = new TestDir();
		await dws.AddWatchAsync(dir.DirPath);

		// Act
		var file = TestHelpers.GenerateRandomFile(TestFileSize, dir.CreateTestPath("file.tmp"));
		
		// Wait for the debounce event to fire
		await Task.Delay(TimeToWait);
		
		// Assert
		Assert.Equal(1, eventChannel.Reader.Count);
		Assert.True(eventChannel.Reader.TryRead(out var evt));
		Assert.Equal(file, evt.FilePath);
		Assert.Null(evt.OldFilePath);
	}

	[Fact]
	public async Task TestDWSWatchDeleteEvent()
	{
		// Arrange
		using var dir = new TestDir();
		var file = TestHelpers.GenerateRandomFile(TestFileSize, dir.CreateTestPath("file.tmp"));
		await dws.AddWatchAsync(dir.DirPath);

		// Act
		File.Delete(file);

		// Wait for the debounce event to fire
		await Task.Delay(TimeToWait);
		
		// Assert
		Assert.Equal(1, eventChannel.Reader.Count);
		Assert.True(eventChannel.Reader.TryRead(out var evt));
		Assert.Equal(file, evt.FilePath);
		Assert.Null(evt.OldFilePath);
	}

	[Fact]
	public async Task TestDWSWatchRenameEvent()
	{
		// Arrange
		using var dir = new TestDir();
		var file = TestHelpers.GenerateRandomFile(TestFileSize, dir.CreateTestPath("file.tmp"));
		var renamePath = dir.CreateTestPath("file_rename.tmp");
		await dws.AddWatchAsync(dir.DirPath);

		// Act
		File.Move(file, renamePath);

		// Wait for the debounce event to fire
		await Task.Delay(TimeToWait);
		
		// Assert
		Assert.Equal(1, eventChannel.Reader.Count);
		Assert.True(eventChannel.Reader.TryRead(out var evt));
		Assert.Equal(file, evt.OldFilePath); // The *old* path is the original filename.
		Assert.Equal(renamePath, evt.FilePath);
	}

	[Fact]
	public async Task TestDWSWatchChangeEvent()
	{
		// Arrange
		using var dir = new TestDir();
		var path = dir.CreateTestPath("test.tmp");
		var file = TestHelpers.GenerateRandomFile(TestFileSize, path);

		// Act
		await dws.AddWatchAsync(dir.DirPath);
		
		// Append one byte of random data
		File.AppendAllBytes(file, TestHelpers.GenerateRandomData(1));

		// Wait for any debounce event to fire
		await Task.Delay(TimeToWait);
		
		// Assert
		Assert.Equal(1, eventChannel.Reader.Count);
		Assert.True(eventChannel.Reader.TryRead(out var evt));
		Assert.Equal(file, evt.FilePath);
		Assert.Null(evt.OldFilePath);
	}

	[Fact]
	public async Task TestDWSWatchDebounceShortLived()
	{
		// Arrange
		using var dir = new TestDir();
		var path = dir.CreateTestPath("test.tmp");
		var renamePath = dir.CreateTestPath("test2.tmp");
		await dws.AddWatchAsync(dir.DirPath);

		// Act
		var file = TestHelpers.GenerateRandomFile(TestFileSize, path);
		File.AppendAllBytes(file, TestHelpers.GenerateRandomData(1));
		File.Move(file, renamePath);
		File.Delete(renamePath);

		// Wait for final debounce event to fire
		await Task.Delay(TimeToWait);
		
		// Assert
		Assert.Equal(1, eventChannel.Reader.Count);
	}

	[Fact]
	public async Task Test_DWSWatch_DebouncesProperly()
	{
		// Arrange
		using var dir = new TestDir();
		var path = dir.CreateTestPath("test.tmp");
		var renamePath = dir.CreateTestPath("test2.tmp");
		await dws.AddWatchAsync(dir.DirPath);

		// Act
		var file = TestHelpers.GenerateRandomFile(TestFileSize, path);
		File.AppendAllBytes(file, TestHelpers.GenerateRandomData(1));
		File.Move(file, renamePath);
		File.Delete(renamePath);

		// Wait shorter than the debounce timeout.
		// This means all file changes happen, but the debounce window will not have triggered yet.
		await Task.Delay(options.DebounceTimeoutMs / 2);
		
		// Assert
		Assert.Equal(0, eventChannel.Reader.Count);
	}

// Not handled anymore by this service
/*
	public static TheoryData<string, string, string> FileNameFilters => new()
	{
		// Filter, matching, non-matching
		{"*.tmp", "test.tmp", "test.txt"},
		{"test.*", "test.txt", "other.tmp"},
		{"test.tmp", "test.tmp", "other.tmp"},
		{"test*.tmp", "test1.tmp", "sample.tmp"},
		{"*-final.txt", "report-final.txt", "report-draft.txt"}
	};

	[Theory]
	[MemberData(nameof(FileNameFilters))]
	public async Task TestDWSWatchFollowsFileNameFilters(
		string fileNameFilter,
		string matchingFileName,
		string nonMatchingFileName)
	{
		// Arrange
		using var dir = new TestDir();
		var matchingFile = TestHelpers.GenerateRandomFile(
			TestFileSize,
			dir.CreateTestPath(matchingFileName));
		var nonMatchingFile = TestHelpers.GenerateRandomFile(
			TestFileSize,
			dir.CreateTestPath(nonMatchingFileName));
		
		await dws.AddWatchAsync(dir.DirPath, fileNameFilter);

		// Act
		File.Delete(matchingFile);
		File.Delete(nonMatchingFile);

		// Wait for any debounce event to fire
		await Task.Delay(TimeToWait);
		
		// Assert
		Assert.Equal(1, eventChannel.Reader.Count);
	}
*/
}
