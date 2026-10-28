using Google.Protobuf;
using Grpc.Core;
using ZeroDrive.Core;
using ZeroDrive.Core.Protos;
using ZeroDrive.Server.Storage;

namespace ZeroDrive.Server.Services;

public class SyncServiceImpl : SyncService.SyncServiceBase
{
    private readonly ILogger<SyncServiceImpl> _logger;
	private readonly IStorageProvider _storageProvider;

    public SyncServiceImpl(ILogger<SyncServiceImpl> logger, IStorageProvider storageProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
    }

	public async override Task<BlockExistsResponse> DoBlocksExist(BlockDataRequest request, ServerCallContext context)
	{
		var response = new BlockExistsResponse();
		for (int i = 0; i < request.BlockHashes.Count; i++)
		{
			var hash = request.BlockHashes[i];
			var cHash = Convert.ToHexString(hash.ToByteArray());
			var exists = await _storageProvider.ChunkExists(cHash);
			if (!exists)
				response.MissingHashes.Add(hash);
			
		}

		if (response.MissingHashes.Count != 0)
		{
			if (_logger.IsEnabled(LogLevel.Information))
				_logger.LogInformation("Missing {Count} blocks from client request.", response.MissingHashes.Count);
		}
		return response;
	}

	public async override Task GetBlockData(BlockDataRequest request, IServerStreamWriter<BlockChunk> responseStream, ServerCallContext context)
	{
		var blockHashes = request.BlockHashes;
		foreach (var hash in blockHashes)
		{
			// TODO: This can be converted to a streaming response
			var cHash = Convert.ToHexString(hash.ToByteArray());
			var chunkData = await _storageProvider.RetrieveChunk(cHash);
			var blockChunk = new BlockChunk()
			{
				BlockHash = hash,
				Data = ByteString.CopyFrom(chunkData)
			};
			if (_logger.IsEnabled(LogLevel.Information))
				_logger.LogInformation("Sending block {Hash} to client.", hash);
			await responseStream.WriteAsync(blockChunk);
		}
	}

	public async override Task<FileInfoData> GetFileInfo(FileInfoRequest request, ServerCallContext context)
	{
		var filePath = request.FilePath;
		var fileInfo = await _storageProvider.RetrieveFileInfo(filePath) ?? throw new RpcException(new Status(StatusCode.NotFound, $"File info for {filePath} not found."));
		
		var response = new FileInfoData()
		{
			FilePath = fileInfo.Path,
			Hash = ByteString.CopyFrom(Convert.FromHexString(fileInfo.Hash)),
			Size = fileInfo.Size,
			Modified = ((DateTimeOffset)fileInfo.LastModified).ToUnixTimeMilliseconds()
		};

		foreach (var chunk in fileInfo.Chunks)
		{
			response.ChunkMeta.Add(new FileInfoData.Types.ChunkMeta()
			{
				Hash = ByteString.CopyFrom(Convert.FromHexString(chunk.Hash)),
				Index = chunk.Index,
				Size = chunk.Size
			});
		}

		if (_logger.IsEnabled(LogLevel.Information))
			_logger.LogInformation("Sending file info for {FilePath} to client.", filePath);

		return response;
	}

	public async override Task<UploadResponse> SetFileInfo(FileInfoData request, ServerCallContext context)
	{
		var fileInfo = new ZFileInfo(
			request.FilePath, 
			request.Size, 
			Convert.ToHexString(request.Hash.ToByteArray()),
			DateTime.UtcNow,
			[.. request.ChunkMeta.Select(
				(chunk, index) => new ZChunkMeta(
					Convert.ToHexString(chunk.Hash.ToByteArray()), 
					(ulong)index, 
					chunk.Size))]);
		await _storageProvider.StoreFileInfo(fileInfo);
		return new UploadResponse();
	}

	public async override Task<UploadResponse> UploadBlockData(IAsyncStreamReader<BlockChunk> requestStream, ServerCallContext context)
	{
		await foreach (var blockChunk in requestStream.ReadAllAsync())
		{
			var hash = Convert.ToHexString(blockChunk.BlockHash.ToByteArray());
			var data = blockChunk.Data.ToByteArray();
			await _storageProvider.StoreChunk(hash, data);
		}
		return new UploadResponse();
	}
}
