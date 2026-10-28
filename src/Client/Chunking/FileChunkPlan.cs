using Google.Protobuf;
using ZeroDrive.Core.Protos;

namespace ZeroDrive.Client.Chunking;

public class FileChunkPlan(IEnumerable<FileChunkMeta> chunks)
{
	private readonly Dictionary<ByteString, FileChunkMeta> _pendingHashes = chunks.ToDictionary(
			// Select the key
			static c => c.Hash
		);

	public FileChunkMeta this[ByteString key] => _pendingHashes[key];

	public int Count => _pendingHashes.Count;

	public bool TryGetOffsets(ByteString hash, out FileChunkMeta offsets)
	{
		if (_pendingHashes.TryGetValue(hash, out var r))
		{
			offsets = r;
			return true;
		}
		offsets = null!;
		return false;
	}

	/// <summary>
	/// Mark a collection of chunks as satisfied in the plan.
	/// </summary>
	/// <param name="hashes"></param>
	/// <exception cref="NotImplementedException"></exception>
	public void MarkSatisfied(IEnumerable<ByteString> hashes)
	{
		foreach (var hash in hashes)
		{
			_pendingHashes.Remove(hash);
		}
	}

	public void MarkSatisfied(ByteString hash)
	{
		_pendingHashes.Remove(hash);
	}

	public bool HasMissingChunks => _pendingHashes.Count > 0;

	public bool Contains(ByteString key) => _pendingHashes.ContainsKey(key);

	public IEnumerable<ByteString> ChunkHashes => [.. _pendingHashes.Keys];
}