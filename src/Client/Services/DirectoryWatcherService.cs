using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ZeroDrive.Core.Services;

public sealed class DirectoryWatcherOptions
{
	/// <summary>
	/// The amount of milliseconds to wait before firing an event, to prevent actively modified files from flooding the service.
	/// </summary>
	public int DebounceTimeoutMs { get; init; } = 1000;

	/// <summary>
	/// The time delay between iterating over debounced events.
	/// </summary>
	public int PushLoopDelayMs { get; init; } = 250;

	/// <summary>
	/// The file type filter for the FileSystemWatcher. "*.*" (all files)
	/// </summary>
	public const string NameFilter = "*.*";

	/// <summary>
	/// The notify filters for the FileSystemWatcher.
	/// Since we use Size and LastWrite as a heuristic to determine if a file changed, we primarily check those.
	/// </summary>
	public const NotifyFilters NotifyFilter =
		NotifyFilters.FileName
		| NotifyFilters.DirectoryName
//		| NotifyFilters.Attributes
		| NotifyFilters.Size
		| NotifyFilters.LastWrite;
//		| NotifyFilters.LastAccess
//		| NotifyFilters.CreationTime
//		| (NotifyFilters)128 ???
//		| NotifyFilters.Security

	public static DirectoryWatcherOptions Default => new();
}

public class DirectoryWatcherService : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// A file that is pending addition to the emitted event channel.
	/// </summary>
	/// <param name="Timestamp"></param>
	/// <param name="OldFilePath"></param>
	private readonly record struct PendingDirtyFile(long Timestamp, string? OldFilePath = null);
	
	private readonly DirectoryWatcherOptions _options;
	private readonly ILogger<DirectoryWatcherService> _logger;
	private readonly ChannelWriter<DirtyFileInfo> _cWriter;

	// === Internal State ===
	/// <summary>
	/// Dictionary where the key is the file path of the modified file, and the value is the information about the change.
	/// </summary>
	private readonly ConcurrentDictionary<string, PendingDirtyFile> _pendingFiles = new();
	
	/// <summary>
	/// The dictionary to store the FileSystemWatcher instances.
	/// </summary>
	private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = [];

	// Task management
	private readonly CancellationTokenSource cts = new();
	private readonly Task pusherTask;
	
	public DirectoryWatcherService(
		ChannelWriter<DirtyFileInfo> channelWriter, 
		ILogger<DirectoryWatcherService> logger,
		DirectoryWatcherOptions? options = null)
	{
		_cWriter = channelWriter;
		_logger = logger;
		_options = options ?? DirectoryWatcherOptions.Default;

		// Start the task at the end.
		pusherTask = ReadPendingFilesAsync(cts.Token);
	}

	/// <summary>
	/// Loops over the pending files, and writes them to the output channel if they debounced.
	/// </summary>
	/// <param name="stoppingToken"></param>
	/// <returns></returns>
	private async Task ReadPendingFilesAsync(CancellationToken stoppingToken)
	{
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				// Small check before the loop (saves on heap allocation GetEnumerator())
				if (!_pendingFiles.IsEmpty)
				{
					foreach (var file in _pendingFiles)
					{
						// Ignore files that are still debounced
						// 500 + 1000 >= TickCount(1005), event should not fire.
						// 500 + 1000 >= TickCount(2005), event *should* fire.
						if (file.Value.Timestamp + _options.DebounceTimeoutMs >= Environment.TickCount64)
							continue;
						
						// Remove the info from the channel, if this function returns false; 
						// the exact KVP was not present and it was modified by another thread.
						if (_pendingFiles.TryRemove(file))
						{
							_cWriter.TryWrite(new DirtyFileInfo(file.Key, file.Value.OldFilePath));
						}
					}
				}

				// Introduce some delay so we don't destroy cpu cores
				await Task.Delay(_options.PushLoopDelayMs, stoppingToken);
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Do nothing. We manually cancelled this task.
		}
	}

#region Event "Registrations"
	/// <summary>
	/// Add a directory watch to the service. This asynchronous method ensures that events are fired properly before returning.
	/// </summary>
	/// <param name="directoryPath"></param>
	/// <returns></returns>
	public async Task AddWatchAsync(string directoryPath)
	{
		// TODO: For robust initialization, we can use this:
		// Create a temporary file in the folder.
		// Add a watch to the folder.
		// Modify the file. If no events are fired from this watcher, loop until it does.
		// When the watcher has fired an events, we are 100% certain the watcher is working.
		// Delete the file.

		// For now, initialize the watch and introduce some artificial delay.
		AddWatch(directoryPath);

		await Task.Delay(50);
	}

	/// <summary>
	/// Add a directory watch to the service. This will fire events into the channel automatically.
	/// </summary>
	/// <param name="directoryPath"></param>
	/// <param name="notifyFilters"></param>
	/// <param name="filesToWatch"></param>
	/// <exception cref="InvalidOperationException"></exception>
	public void AddWatch(string directoryPath)
	{
		if (_watchers.ContainsKey(directoryPath))
		{
			// TODO: Perhaps we should throw an exception OR just re-configure the existing watcher instead
			_logger.LogWarning("Directory {DirectoryPath} is already being watched.", directoryPath);
			return;
		}

		// Throws on invalid path
		var watcher = new FileSystemWatcher(directoryPath)
		{
			NotifyFilter = DirectoryWatcherOptions.NotifyFilter,
			Filter = DirectoryWatcherOptions.NameFilter,
			IncludeSubdirectories = true
		};

		watcher.Created += OnEvent;
		watcher.Changed += OnEvent;
		watcher.Deleted += OnEvent;
		watcher.Renamed += OnRename;
		watcher.Error += OnError;

		if (_watchers.TryAdd(directoryPath, watcher))
		{
			watcher.EnableRaisingEvents = true;
			_logger.LogInformation("Started watching directory: {DirectoryPath}", directoryPath);
		}
		else
		{
			watcher.Dispose();
			// Realistically, if we ever reach here, a race condition happened where another thread added the key before us, so we can safely ignore.
			_logger.LogWarning("Watcher for {DirectoryPath} was added concurrently.", directoryPath);
		}
	}

	public bool RemoveWatch(string unwatchDirectoryPath)
	{
		if (_watchers.TryRemove(unwatchDirectoryPath, out var watcher))
		{
			watcher.EnableRaisingEvents = false;
			watcher.Dispose();
			_logger.LogInformation("Stopped watching directory: {DirectoryPath}", unwatchDirectoryPath);
			return true;
		}
		else
		{
			_logger.LogWarning("Directory {DirectoryPath} was not being watched.", unwatchDirectoryPath);
			return false;
		}
	}
#endregion

#region Event Handlers
	/// <summary>
	/// Handles file system events and writes them to the channel.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void OnEvent(object sender, FileSystemEventArgs e)
	{
		_pendingFiles.AddOrUpdate(
			e.FullPath, 
			// Add the value normally.
			addValueFactory: static (k) => new PendingDirtyFile(Environment.TickCount64), 
			// If it already exists, make sure we grab the old file path.
			updateValueFactory: static (k, o) => new PendingDirtyFile(Environment.TickCount64, o.OldFilePath)
		);
	}

	private void OnRename(object sender, RenamedEventArgs e)
	{
		// Remove the existing entry for this file
		_pendingFiles.TryRemove(e.OldFullPath, out var priorState);

		// Check the original path based on the priorState. If no prior state, the current event is used.
		var originalPath = priorState.OldFilePath ?? e.OldFullPath;
		// Prevent circular renaming (A.txt -> B.txt -> A.txt should not be considered a rename event).
		var resolvedOldPath = (originalPath == e.FullPath) ? null : originalPath;

		_pendingFiles[e.FullPath] = new PendingDirtyFile(Environment.TickCount64, resolvedOldPath);

/*
		FileModifications.AddOrUpdate(
			e.FullPath,
			addValueFactory: (k) => new PendingDirtyFile(Environment.TickCount64, resolvedOldPath),
			// Somehow, another thread added a new event to the dictionary while we were modifying it.
			updateValueFactory: (k, o) => new PendingDirtyFile(Environment.TickCount64, resolvedOldPath)
		);
*/
	}

	private void OnError(object sender, ErrorEventArgs e)
	{
		_logger.LogError(e.GetException(), "FileSystemWatcher encountered an error.");
	}
#endregion

#region Disposal
	/// <summary>
	/// Disposes the resources used by the DirectoryWatcherService.
	/// </summary>
	public void Dispose()
	{
		foreach (var watcher in _watchers.Values)
		{
			watcher.Dispose();
		}
		_watchers.Clear();
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		// Cancel the cts
		await cts.CancelAsync();
		// Wait for the task to complete
		await pusherTask;

		Dispose();
		GC.SuppressFinalize(this);
	}
#endregion
}