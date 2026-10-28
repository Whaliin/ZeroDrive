using System.Buffers;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using ZeroDrive.Core.Hashing;

namespace ZeroDrive.Client.Chunking;

/// <summary>
/// Base class for chunking data into smaller parts for transmission or storage.
/// </summary>
public abstract class ChunkerBase(IContentHasher hasher)
{
	public abstract int MaxChunkSize { get; }

	// TODO: Maybe this should be handled by the caller in the future, but for now it's way easier to just do this
	/// <summary>
	/// The content hasher used to compute hashes for the chunks.
	/// </summary>
	protected IContentHasher Hasher { get; init; } = hasher ?? throw new ArgumentNullException(nameof(hasher));

	/// <summary>
	/// Get the next slice position in a sequence of bytes.
	/// </summary>
	/// <param name="stream"></param>
	/// <returns></returns>
	protected abstract int GetNextChunkSlice(ReadOnlySpan<byte> buffer);

	/// <summary>
	/// The base implementation for an asynchronous chunk stream.
	/// </summary>
	/// <param name="stream">The stream to read and chunk data from.</param>
	/// <param name="ct"></param>
	/// <returns></returns>
	public virtual async IAsyncEnumerable<ChunkData> StreamLocalChunks(Stream stream, [EnumeratorCancellation] CancellationToken ct = default)
	{
		if (!stream.CanRead)
			throw new ArgumentException("Input stream is not readable.", nameof(stream));
		
		// Create our buffer to read into
		var buffer = ArrayPool<byte>.Shared.Rent(MaxChunkSize);

		try
		{
			// Start offset of the currently yielded chunk
			long offset = 0;
			long startPosition = -1;

			if (stream.CanSeek)
			{
				// Seek to beginning
				startPosition = stream.Position;
				stream.Seek(0, SeekOrigin.Begin);		
			}

			// The amount of valid unconsumed bytes in the buffer
			int bufferLength = 0;
			bool endOfStream = false;
			while (true)
			{
				if (!endOfStream && bufferLength < MaxChunkSize)
				{
					// Read at most MaxChunkSize bytes into memory, reduced and offset by the existing amount of bytes. 
					var bytesRead = await stream.ReadAsync(buffer.AsMemory(bufferLength, MaxChunkSize - bufferLength), ct);
					
					// Update the read guard
					if (bytesRead == 0)
						endOfStream = true;

					// Update the amount of bytes in the buffer
					bufferLength += bytesRead;
				}

				// If the buffer doesn't contain any more data, and we've reached the end of file, break the loop.
				if (bufferLength == 0)
					break;

				// Get the next slice position (from the start to current length of the buffer)
				var nextSlice = GetNextChunkSlice(new ReadOnlySpan<byte>(buffer, 0, bufferLength));
				
				// Guard the loop from infinite loops from faulty chunker implementations.
				if (nextSlice <= 0)
					throw new InvalidOperationException("Chunk slice must be greater than zero.");
				
				// Create the span over the slice
				var span = new ReadOnlyMemory<byte>(buffer, 0, nextSlice);

				// Hash it
				var hash = ByteString.CopyFrom(Hasher.ComputeHash(span));

				// Return the data for the read chunk
				yield return new ChunkData(hash, span, offset);
				
				// Calculate the remaining bytes that were not used in the current chunk
				var remaining = bufferLength - nextSlice;

				// Copy the remaining bytes into the front of the buffer
				buffer.AsSpan(nextSlice, remaining).CopyTo(buffer);

				// Update the existing length of the buffer
				bufferLength = remaining;

				// Increment the start offset after the yield
				offset += nextSlice;
			}

			if (stream.CanSeek && startPosition != -1)
				stream.Seek(startPosition, SeekOrigin.Begin);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}