namespace ZeroDrive.Core.Hashing;

public interface IContentHasher
{
	/// <summary>
	/// Computes the hash of the provided data view using the implemented hashing algorithm.
	/// </summary>
	/// <param name="data">The data to be hashed.</param>
	/// <returns>The computed hash.</returns>
	byte[] ComputeHash(ReadOnlyMemory<byte> data);
}