using ZeroDrive.Core.Hashing;

namespace ZeroDrive.Client.Chunking;

/// <summary>
/// Utility class for chunking files into smaller parts for transmission.
/// </summary>
public class FixedSizeChunker(IContentHasher hasher, int chunkSize = 1024) : ChunkerBase(hasher)
{
	/// <summary>
	/// Default chunk size in bytes for file chunking operations.
	/// </summary>
	private readonly int ChunkSize = chunkSize > 0 ? chunkSize : throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));

	public override int MaxChunkSize { get => ChunkSize; }

	// Return at most chunk size, if the buffer is not less
	protected override int GetNextChunkSlice(ReadOnlySpan<byte> buffer) => Math.Min(buffer.Length, ChunkSize);
}