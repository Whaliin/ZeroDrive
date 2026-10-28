using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ZeroDrive.Client.Chunking;
using ZeroDrive.Core.Protos;

namespace ZeroDrive.Core.Services;

public partial class SyncWorkerService
{
	private readonly Channel<string> _downloadPaths = Channel.CreateUnbounded<string>();

	private static ByteString FCMHashSelector(FileChunkMeta fcm) => fcm.Hash;

	private static string ByteStringListToHexStrings(IEnumerable<ByteString> bytes) => string.Join(", ", bytes.Select(b => Convert.ToHexString(b.Span)));

	/// <summary>
	/// Wrapper for getting blocks from server. Returns an async iterator over the response stream of blocks.
	/// </summary>
	/// <param name="chunkHashes">List of ByteString hashes to download</param>
	/// <param name="ct"></param>
	/// <returns></returns>
	private async IAsyncEnumerable<ChunkData> GetChunksFromServer(IEnumerable<ByteString> chunkHashes, [EnumeratorCancellation] CancellationToken ct)
	{
		var bdr = new BlockDataRequest();
		bdr.BlockHashes.AddRange(chunkHashes);
		var call = _grpcClient.GetBlockData(bdr, cancellationToken: ct);

		await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
		{
			yield return new ChunkData(chunk.BlockHash, chunk.Data.Memory);
		}
	}

	/// <summary>
	/// Write a collection of chunks to a file
	/// </summary>
	/// <param name="fileDest">The destination filestream</param>
	/// <param name="plan">The plan to get chunk offsets from</param>
	/// <param name="chunks">A list of chunks to process</param>
	/// <param name="ct"></param>
	/// <returns></returns>
	private async Task WriteChunksAsync(
		FileStream fileDest, 
		FileChunkPlan plan, 
		IAsyncEnumerable<ChunkData> chunks,
		CancellationToken ct)
	{
		if (!fileDest.CanWrite)
			throw new SyncException("Write handle not acquired");
		
		// For each chunk from the output (file chunk or download)
		await foreach (var chunk in chunks)
		{
			// Check if the plan contains this byte hash and get the offsets for it
			if (plan.TryGetOffsets(chunk.Hash, out var fcm))
			{
				// For each offset, write the data
				foreach (var oi in fcm.Offsets)
				{
					// Seek in the destination file
					fileDest.Seek(oi, SeekOrigin.Begin);
					await fileDest.WriteAsync(chunk.Data, ct);
				}

				// Mark the hash as satisfied in the plan (no further writes for this hash needed)
				plan.MarkSatisfied(chunk.Hash);
			}
		}
	}

	private async Task DownloadWorker(CancellationToken ct)
	{
		await foreach (var filePath in _downloadPaths.Reader.ReadAllAsync(ct))
		{
			// Get the job data
			if (!_activeJobs.TryGetValue(filePath, out var job))
			{
				// Another thread got here before us?
				_logger.LogInformation("Unable to get job data for job {fp}", filePath);
				continue;
			}

			if (job.Cts.IsCancellationRequested || job.Status != SyncStatus.Queued)
			{
				_logger.LogInformation("Job was cancelled before execution for {fp}", filePath);
				continue;
			}

			// Job is not correct direction. This is not an error per se, but it is a pretty bad case and shouldn't happen.
			if (job.Direction != SyncDirection.Download)
			{
				_logger.LogCritical("Download Channel mismatch: Job is not Download: {syncDir}", job.Direction);
				continue;
			}

			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, job.Cts.Token);
			var job_ct = linkedCts.Token;
				
			// Create a temporary file name
			var tempFileName = filePath + ".ztmp";

			// TODO: Make sure renaming files works.
			try
			{
				// Check the file that needs to be downloaded from the server
				var fileDownloadInfo = await _grpcClient.GetFileInfoAsync(new FileInfoRequest()
				{
					FilePath = job.RelativePath,
					WorkspaceId = job.WorkspaceId.ToString()
				}, cancellationToken: job_ct);

				// Ensure the any subdirectories exist
				var dir = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);

				// Create the destination file
				await using var newFile = File.OpenWrite(tempFileName);
				
				// Set the length (size)
				newFile.SetLength(fileDownloadInfo.Size);

				// Create the plan
				var plan = new FileChunkPlan(fileDownloadInfo.ChunkMeta);

				// Local pass (copy existing data from the old file)
				if (File.Exists(filePath))
				{
					var fileInfo = new FileInfo(filePath);
					// Make sure we have enough space to create a working copy or download
					var drive = new DriveInfo(Path.GetPathRoot(filePath)!);
					if (drive.AvailableFreeSpace < fileInfo.Length)
					{
						throw new SyncException($"Not enough space to create a working copy for {job.RelativePath} (Need {fileInfo.Length/1024:F1})");
					}

					job.Status = SyncStatus.Chunking;
					var chunker = _chunkerFactory();
					using var oldFile = File.OpenRead(filePath);
					await WriteChunksAsync(newFile, plan, chunker.StreamLocalChunks(oldFile, job_ct), job_ct);
				}

				// Network download
				if (plan.HasMissingChunks)
				{
					job.Status = SyncStatus.InFlight;
					await WriteChunksAsync(newFile, plan, GetChunksFromServer(plan.ChunkHashes, job_ct), job_ct);
				}

				// Final error check
				if (plan.HasMissingChunks)
				{
					job.Status = SyncStatus.Failed;
					throw new SyncException($"Could not clone file data from local and server source: {filePath}");
				}

				// Don't forget to dispose our file handle
				await newFile.DisposeAsync();

				// Move the file into the correct path
				File.Move(tempFileName, filePath, overwrite: true);
			}
			catch (OperationCanceledException)
			{
				// Do nothing. This operation was cancelled and we should safely stop.
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to download {fp}", filePath);
				job.Status = SyncStatus.Failed;
				// TODO: Track the failed somewhere else
				// _activeJobs.TryRemove(filePath, out var failedJob);
			}
			finally
			{
				_activeJobs.TryRemove(new KeyValuePair<string, SyncJob>(filePath, job));

				if (File.Exists(tempFileName))
					File.Delete(tempFileName);
			}
		}
	}
}