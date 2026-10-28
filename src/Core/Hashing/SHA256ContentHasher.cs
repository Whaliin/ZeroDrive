namespace ZeroDrive.Core.Hashing;

public class SHA256ContentHasher : IContentHasher
{
	byte[] IContentHasher.ComputeHash(ReadOnlyMemory<byte> data) => System.Security.Cryptography.SHA256.HashData(data.ToArray());
}