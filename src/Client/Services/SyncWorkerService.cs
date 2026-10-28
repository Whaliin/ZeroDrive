using System.Collections.Concurrent;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroDrive.Client.Chunking;
using ZeroDrive.Core.DTOs;
using ZeroDrive.Core.Protos;

namespace ZeroDrive.Core.Services;

[Serializable]
internal class SyncException : Exception
{
	public SyncException() {}
	public SyncException(string? message) : base(message) {}
	public SyncException(string? message, Exception? innerException) : base(message, innerException) {}
}

public partial class SyncWorkerService : BackgroundService
{
	private readonly ILogger<SyncWorkerService> _logger;
	private readonly WorkspaceService _ws;
	private readonly SyncService.SyncServiceClient _grpcClient;
	private readonly Func<ChunkerBase> _chunkerFactory;

	private Task clientTask = Task.CompletedTask;
	private Task serverTask = Task.CompletedTask;

	public SyncWorkerService(
		WorkspaceService workspaceService, 
		ILogger<SyncWorkerService> logger, 
		SyncService.SyncServiceClient grpcClient,
		Func<ChunkerBase> chunkerFactory)
	{
		_ws = workspaceService;
		_logger = logger;
		_grpcClient = grpcClient;
		_chunkerFactory = chunkerFactory;
	}

	public enum SyncDirection { Upload, Download }
	public enum SyncStatus { Queued, Chunking, InFlight, Cancelled, Failed }

	public record SyncJob(
		string RelativePath,
		Guid WorkspaceId,
		SyncDirection Direction,
		CancellationTokenSource Cts,
		string? OldFilePath = null
	)
	{
		public SyncStatus Status { get; set; } = SyncStatus.Queued;
	}

	private readonly ConcurrentDictionary<string, SyncJob> _activeJobs = new();

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("SyncWorkerService is starting.");

		while (!stoppingToken.IsCancellationRequested)
		{
			// First, reconcilliate files on disk
			await ReconcilliateFiles(stoppingToken);

			// Then, start listening for updates
			serverTask = ProcessServerFileChangeAsync(stoppingToken);
			
			// Then, start our local file change monitor loop
			clientTask = ProcessFileChangesAsync(stoppingToken);

			await Task.WhenAll(clientTask, serverTask);

			// Something probably failed, or caused the service to cancel.
			// Restart the loop? Log an error?
		}
	}

	private async Task ProcessFileChangesAsync(CancellationToken ct)
	{
		await foreach (var fileChangeEvent in _ws.FileChanges.ReadAllAsync(ct))
		{
			_logger.LogInformation("Processing file change: {FilePath}", fileChangeEvent.FilePath);
			try
			{
				// Check what workspace the file belongs to based on the path
				// Check if the file exists in our baseline
				// Check if the file exists on disk
				// Check the File.Size and File.LastWritten parameters
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing file change: {FilePath}", fileChangeEvent.FilePath);
			}
		}
	}

	private async Task ProcessServerFileChangeAsync(CancellationToken ct)
	{
		// Read the server file changes from grpcClient and await continuously. If the connection is cancelled/dropped/whatever
		// run the reconcilliation before proceeding with handshake. 
		// This task must basically only run after ReconcilliateFiles
	}

#region Reconcilliation
	private async Task<List<ZFileInfo>> GetWorkspaceFilesAsync(Guid wsId, CancellationToken ct)
	{
		var call = _grpcClient.GetWorkspaceManifest(new()
		{
			WorkspaceId = wsId.ToString()
		}, cancellationToken: ct);

		var files = new List<ZFileInfo>();
		// Read response from server
		await foreach (var fi in call.ResponseStream.ReadAllAsync(ct))
		{
			files.Add(fi.ToZFileInfo());
		}
		return files;
	}

	private async Task ReconcilliateFiles(CancellationToken ct)
	{
		// Check the file information from the server
		// Get the available workspaces from the server
		var serverWorkspaces = new List<ZWorkspace>();
		var wsQuery = _grpcClient.GetWorkspaces(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: ct, deadline: DateTime.Now + new TimeSpan(0, 1, 0));
		await foreach (var wsData in wsQuery.ResponseStream.ReadAllAsync(ct))
		{
			if (!wsData.IsInitialized())
			{
				_logger.LogWarning("WorkspaceData from GetWorkspaces is not initialized!");
				continue;
			}

			serverWorkspaces.Add(new ZWorkspace(Guid.Parse(wsData.WorkspaceId), wsData.WorkspaceName));
		}

		_ws.SetAvailableServerWorkspaces(serverWorkspaces);

		var filesToCheck = new List<string>();
		var filesToUpload = new List<string>();
		var filesToDownload = new List<string>();

		// Sync files that dont match
		// Workspaces that exist on disk, but NOT on server
		// This will require full chunking, so it can (will) take a damn long time. 
		// Perhaps exposing another Task wrapper for this would be good?
		// How do we handle the DWS on this folder?
		var serverWorkspaceData = new List<LocalWorkspaceMap>();
		var unsyncedWorkspaces = _ws.UnsyncedWorkspaces;

		// Workspaces that exist on disk, AND on server
		// Fetch each metadata and compare to local workspace.
		var syncedWorkspaces = _ws.MappedServerWorkspaces;
		foreach (var ws in syncedWorkspaces)
		{
			// Create the list of files from server
			var serverFiles = await GetWorkspaceFilesAsync(ws.Id, ct);
			var localFiles = _ws.GetWorkspaceFilesOnDisk(ws.Id);

			// Get files on disk that aren't in the server
			var localPendingFiles = localFiles.ExceptBy(
				serverFiles.Select(sf => sf.Path), 
				lf => lf.RelativePath, 
				StringComparer.OrdinalIgnoreCase
			);

			// Get files on server that aren't on disk
			var serverPendingFiles = serverFiles.ExceptBy(
				localFiles.Select(lf => lf.RelativePath), 
				sf => sf.Path, 
				StringComparer.OrdinalIgnoreCase
			);
			
			// Check local files for their modification by intersecting (elems that exist in both)
			var pairs = localFiles.Join(
				serverFiles,
				lf => lf.RelativePath,
				sf => sf.Path,
				(lf, sf) => (Local: lf, Server: sf),
				StringComparer.OrdinalIgnoreCase
			);

			var serverNewerFiles = pairs.Where(t => t.Local.LastModified < t.Server.LastModified);
			var clientNewerFiles = pairs.Where(t => t.Server.LastModified < t.Local.LastModified);
			
			// Files that have changed size (but not write time)
			var sizeChangedFiles = pairs
				// Allow some tolerance for file system precision differences
				.Where(t => Math.Abs((decimal)(ulong)t.Local.Size - t.Server.Size) > 1000)
				.Except(serverNewerFiles)
				.Except(clientNewerFiles);
		}
	}
#endregion
}