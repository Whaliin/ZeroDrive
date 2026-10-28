using System.Buffers;
using System.Threading.Channels;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using ZeroDrive.Core.Protos;

namespace ZeroDrive.Core.Services;

public partial class SyncWorkerService
{
	private readonly Channel<string> _uploadPaths = Channel.CreateUnbounded<string>();

	private async Task UploadChunkDataAsync(
		FileStream fs, 
		IEnumerable<FileChunkMeta> uploadChunks,
		CancellationToken ct = default)
	{
		// Get the maximum size of the chunks
		var maxChunkSize = uploadChunks.Max(s => s.Size);

		// Begin our upload call
		var uploadStreamingCall = _grpcClient.UploadBlockData(cancellationToken: ct);

		// Rent a byte buffer to write into
		var buffer = ArrayPool<byte>.Shared.Rent((int)maxChunkSize);
		try
		{
			foreach (var ms in uploadChunks)
			{
				// Find the first offset of the missing chunk in the file
				fs.Seek(ms.Offsets.First(), SeekOrigin.Begin);

				// Copy the data into the buffer
				await fs.ReadExactlyAsync(buffer.AsMemory(0, (int)ms.Size), ct);

				// Create the chunk message to upload
				var chunkMsg = new BlockChunk()
				{
					// Get the hash from the missing chunk
					BlockHash = ms.Hash,
					// Make sure we copy only the span we actually wrote (ms.Size)
					Data = ByteString.CopyFrom(buffer.AsSpan(0, (int)ms.Size))
				};

				// Make the upload call
				await uploadStreamingCall.RequestStream.WriteAsync(chunkMsg, ct);
			}

			// After we've finished iterating, mark the call as complete.
			await uploadStreamingCall.RequestStream.CompleteAsync();

			// Wait for the server response
			var response = await uploadStreamingCall.ResponseAsync;

			// Check the response for errors
			// TODO: Re-schedule the upload?
			if (response.HasError)
			{
				throw new SyncException($"Error in file upload: {response.Error}");
			}
		}
		finally
		{
			// Return the byte buffer to pool
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private async Task UploadWorker(CancellationToken ct)
	{
		await foreach (var filePath in _uploadPaths.Reader.ReadAllAsync(ct))
		{
			// Get the job data
			if (!_activeJobs.TryGetValue(filePath, out var job))
			{
				// Another thread got here before us
				_logger.LogInformation("Unable to get job data for job {fp}", filePath);
				continue;
			}

			if (job.Cts.IsCancellationRequested || job.Status != SyncStatus.Queued)
			{
				_logger.LogInformation("Job was cancelled before execution for {fp}", filePath);
				continue;
			}

			// Job is not an upload job. This is not an error per se, but it is a pretty bad case and shouldn't happen.
			if (job.Direction != SyncDirection.Upload)
			{
				_logger.LogCritical("Upload Channel mismatch: Job is not Upload: {syncDir}", job.Direction);
				continue;
			}

			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, job.Cts.Token);
			var job_ct = linkedCts.Token;

			try
			{
				// Find the file on disk
				if (!File.Exists(filePath))
					continue;
				
				var fileOnDisk = new FileInfo(filePath);

				// Chunk it
				job.Status = SyncStatus.Chunking;

				// Create the stage message
				var stageMessage = new StageFileInfoRequest()
				{
					FilePath = job.RelativePath,
					Modified = fileOnDisk.LastWriteTimeUtc.ToUnixTimeMilliseconds(),
					Size = fileOnDisk.Length,
					WorkspaceId = job.WorkspaceId.ToString()
				};

				var chunker = _chunkerFactory();

				// Create our file handle
				await using var fs = fileOnDisk.OpenRead();

				// Create the chunk map (dictionary for fast lookup)
				var chunkMap = new Dictionary<ByteString, FileChunkMeta>();
				await foreach (var chunk in chunker.StreamLocalChunks(fs, job_ct))
				{
					// If we can find the hash in our map, grab it, if not, create a new value and add it.
					if (!chunkMap.TryGetValue(chunk.Hash, out var fcm))
					{
						fcm = new FileChunkMeta()
						{
							Hash = chunk.Hash,
							Size = chunk.Data.Length
						};
						chunkMap[chunk.Hash] = fcm;
					}

					// Add the offset
					fcm.Offsets.Add(chunk.Offset);
				}

				// Add the chunk metas
				stageMessage.ChunkMeta.AddRange(chunkMap.Values);

				// Stage the file
				var stageCall = await _grpcClient.StageFileInfoAsync(stageMessage, cancellationToken: job_ct);

				// Compute the missing hash set
				var missingChunks = stageCall.MissingHashes.Select(h => chunkMap[h]).ToList();

				// If there are missing chunks, upload them
				if (missingChunks.Count != 0)
				{
					job.Status = SyncStatus.InFlight;
					await UploadChunkDataAsync(fs, missingChunks, job_ct);
				}

				// Commit the file
				var commitCall = await _grpcClient.CommitFileInfoAsync(new CommitFileInfoRequest()
				{
					FilePath = job.RelativePath,
					OldFilePath = job.OldFilePath,
					StageId = stageCall.HasStageId ? stageCall.StageId : null,
					WorkspaceId = job.WorkspaceId.ToString()
				}, cancellationToken: job_ct);

				if (commitCall.HasError)
				{
					throw new SyncException($"Commit failed for file {filePath}: {commitCall.Error}");
				}
			}
			catch (OperationCanceledException)
			{
				// Do nothing. This operation was cancelled and we should safely stop.
			}
			// For any other error, log it first
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to upload {fp}", filePath);
				job.Status = SyncStatus.Failed;
				// TODO: Track the failed somewhere else
				// _activeJobs.TryRemove(filePath, out var failedJob);
			}
			// Only remove the job IF it matches the existing job exactly.
			finally
			{
				_activeJobs.TryRemove(new KeyValuePair<string, SyncJob>(filePath, job));
			}
		}
	}
}