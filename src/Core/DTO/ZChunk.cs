namespace ZeroDrive.Core.DTOs;

/// <summary>
/// Represents metadata for a chunk of data in a file.
/// </summary>
/// <param name="hash">The hash information of the data.</param>
/// <param name="index">The index of the chunk in the file.</param>
public readonly struct ZChunkMeta(byte[] hash, ulong index)
{
	public readonly byte[] Hash = hash;
	public readonly ulong Index = index;
}

/// <summary>
/// Chunk class of data in a file. These objects are intended to be transient and short-lived in memory.
/// </summary>
/// <param name="Hash"></param>
/// <param name="Offset"></param>
/// <param name="data">Chunk data.</param>
public record ZChunk(byte[] Hash, ulong Offset, byte[] data)
{
	public readonly ZChunkMeta Meta = new(Hash, Offset);

	// Make sure that the data is copied into our own buffer, so the reference doesn't get invalidated.
	private readonly byte[] _data = data;
	
	public ReadOnlySpan<byte> Data => _data;
}