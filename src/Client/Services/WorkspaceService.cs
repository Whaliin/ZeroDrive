using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroDrive.Core;
using ZeroDrive.Core.DTOs;

namespace ZeroDrive.Core.Services;

/// <summary>
/// This class should contain all the data we need to operate
/// </summary>
public class WorkspaceService : IHostedService, IAsyncDisposable, IDisposable
{
	// Attribute data
	private readonly Lock _lock = new();
	private readonly List<LocalWorkspaceMap> _workspaces = [];
	private readonly List<ZWorkspace> _serverWorkspaces = []; 
	public IReadOnlyList<ZWorkspace> AvailableServerWorkspaces
	{
		get
		{
			lock (_lock)
			{
				return [.. _serverWorkspaces];
			}
		}
	}
	public IReadOnlyList<ZWorkspace> UnmappedServerWorkspaces
	{
		get
		{
			lock (_lock)
			{
				return [.. _serverWorkspaces
					.ExceptBy(_workspaces
						.Select(w => w.Workspace.Id),
						sws => sws.Id)
					];
			}
		}
	}
	public IReadOnlyList<ZWorkspace> MappedServerWorkspaces
	{
		get
		{
			lock (_lock)
			{
				return [.. _serverWorkspaces
					.IntersectBy(_workspaces
						.Select(w => w.Workspace.Id),
						w => w.Id)
					];
			}
		}
	}
	public IReadOnlyList<ZWorkspace> UnsyncedWorkspaces
	{
		get
		{
			lock (_lock)
			{
				return [.. _workspaces.Select(lws => lws.Workspace)
					.ExceptBy(_serverWorkspaces
						.Select(sws => sws.Id), 
						lws => lws.Id)
					];
			}
		}
	}

	// Services
	private readonly ILogger<WorkspaceService> _logger;
	private readonly DirectoryWatcherService _dws;
	private readonly Channel<DirtyFileInfo> _fileChanges;
	private readonly ClientConfigService _cfgSvc;

	public ChannelReader<DirtyFileInfo> FileChanges => _fileChanges.Reader;

	public WorkspaceService(
		ILoggerFactory loggerFactory,
		ClientConfigService configService,
		DirectoryWatcherOptions? watcherOptions = null
	)
	{
		// Initialize the DWS
		_fileChanges = Channel.CreateBounded<DirtyFileInfo>(new BoundedChannelOptions(10000)
		{
			FullMode = BoundedChannelFullMode.Wait
		});

		_dws = new DirectoryWatcherService(_fileChanges.Writer, loggerFactory.CreateLogger<DirectoryWatcherService>(), watcherOptions);

		// Initialize ourselves
		_logger = loggerFactory.CreateLogger<WorkspaceService>();
		_cfgSvc = configService;
	}

	public void SetAvailableServerWorkspaces(IEnumerable<ZWorkspace> serverWorkspaces)
	{
		lock (_lock)
		{
			_serverWorkspaces.Clear();
			_serverWorkspaces.AddRange(serverWorkspaces);
		}
	}
	
	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		// read the config from disk and load them into workspace def
		var clientConfig = await _cfgSvc.LoadAsync(cancellationToken);

		var lwsFromDisk = clientConfig.Workspaces.Select((v) =>
			new LocalWorkspaceMap(
				v.LocalPath, 
				new ZWorkspace(
					v.WorkspaceId, 
					Path.GetFileName(Path.TrimEndingDirectorySeparator(v.LocalPath))
				)
			)
		);

		SetWorkspaces(lwsFromDisk);
	}

	public void AddWorkspace(LocalWorkspaceMap map)
	{
		lock (_lock)
		{
			// Add the workspace
			_workspaces.Add(map);
		}

		// Create the directory if it doesn't exist
		Directory.CreateDirectory(map.LocalPath);

		// Add watcher to this workspace
		_dws.AddWatch(map.LocalPath);
	}

	public void RemoveWorkspace(Guid workspaceId, bool delete = false)
	{
		LocalWorkspaceMap? foundWorkspace;
		lock (_lock)
		{
			foundWorkspace = _workspaces.FirstOrDefault((v) => v.Workspace.Id == workspaceId);
			if (foundWorkspace == null)
				return;

			_workspaces.Remove(foundWorkspace);
		}

		_dws.RemoveWatch(foundWorkspace.LocalPath);

		if (delete)
			Directory.Delete(foundWorkspace.LocalPath, true);
	}

	public void SetWorkspaces(IEnumerable<LocalWorkspaceMap> maps)
	{
		// Materialize the enumerable
		maps = [.. maps];

		// Diff
		IEnumerable<LocalWorkspaceMap> toRemove;
		IEnumerable<LocalWorkspaceMap> toAdd;

		lock (_lock)
		{
			// Calculate diff
			toRemove = [.. _workspaces.ExceptBy(maps.Select(w => w.Workspace.Id), w => w.Workspace.Id)];
			toAdd = [.. maps.ExceptBy(_workspaces.Select(w => w.Workspace.Id), w => w.Workspace.Id)];

			// Add the workspace defs
			_workspaces.Clear();
			_workspaces.AddRange(maps);
		}

		// Add watchers
		foreach (var map in toAdd)
		{
			Directory.CreateDirectory(map.LocalPath);
			_dws.AddWatch(map.LocalPath);
		}

		// Remove watchers
		foreach (var map in toRemove)
		{
			_dws.RemoveWatch(map.LocalPath);
		}
	}

	public bool TryFindWorkspaceForPath(string absolutePath, out LocalWorkspaceMap map)
	{
		if (!absolutePath.IsValidPath())
			throw new InvalidOperationException("Path must be fully qualified");

		lock (_lock)
		{
			foreach (var ws in _workspaces)
			{
				if (ws.IsFileInWorkspace(absolutePath))
				{
					map = ws;
					return true;
				}
			}
		}

		map = null!;
		return false;
	}

	public bool TryGetWorkspace(Guid workspaceId, out LocalWorkspaceMap map)
	{
		lock (_lock)
		{
			foreach (var ws in _workspaces)
			{
				if (ws.Workspace.Id == workspaceId)
				{
					map = ws;
					return true;
				}
			}
		}

		map = null!;
		return false;
	}

	private static List<string> GetFilesOnDisk(string path)
	{
		var files = new List<string>();

		files.AddRange(Directory.GetFiles(path));

		;

		foreach (var dirPath in Directory.GetDirectories(path))
		{
			files.AddRange(GetFilesOnDisk(dirPath));
		}

		return files;
	}

	public List<(string RelativePath, long Size, DateTime LastModified)> GetWorkspaceFilesOnDisk(Guid workspaceId)
	{
		if (TryGetWorkspace(workspaceId, out var map))
		{
			// TODO: Filter by extensions!
			var dirInfo = new DirectoryInfo(map.LocalPath);
			return [.. dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
				.Select(fi => (map.ConvertToRelativePath(fi.FullName), fi.Length, fi.LastWriteTimeUtc))];
		}
		else
		{
			return [];
		}
	}

	public int WorkspaceCount => _workspaces.Count;

#region Disposal
	public async ValueTask DisposeAsync()
	{
		await _dws.DisposeAsync();
		_fileChanges.Writer.Complete();
		GC.SuppressFinalize(this);
	}

	public void Dispose()
	{
		_dws.Dispose();
		_fileChanges.Writer.Complete();
		GC.SuppressFinalize(this);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await DisposeAsync();
	}
#endregion
}