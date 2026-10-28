using ZeroDrive.Core.DTOs;
using ZeroDrive.Core.Protos;
using Google.Protobuf;
using ZeroDrive.Core;

namespace ZeroDrive.Client;

public static class ProtoExtensions
{
	extension(FileInfoData fi)
	{
		public ZFileInfo ToZFileInfo()
		{
			var chunkMetas = new List<ChunkMeta>();
			return new ZFileInfo(
					Guid.Parse(fi.WorkspaceId), 
					fi.FilePath, 
					fi.Size, 
					fi.Hash.ToByteArray(), 
					fi.Modified.ToDateTimeFromUnixEpoch(),
					[.. fi.ChunkMeta.Select(c => c.ToZChunkMeta())]);
		}
	}

	extension(ChunkMeta cm)
	{
		public ZChunkMeta ToZChunkMeta() => new(cm.Hash.ToByteArray(), cm.Index);
	}
}
