using Google.Protobuf;

namespace ZeroDrive.Client.Chunking;

/// <summary>
/// An ephemeral representation of a chunk, either from disk or from server.
/// If the chunk was streamed from the server, offset will <b>always</b> be 0.
/// </summary>
/// <param name="Hash"></param>
/// <param name="Data"></param>
/// <param name="Offset"></param>
public readonly record struct ChunkData(ByteString Hash, ReadOnlyMemory<byte> Data, long Offset = 0);