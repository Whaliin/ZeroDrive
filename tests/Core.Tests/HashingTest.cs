using ZeroDrive.Core.Hashing;

namespace ZeroDrive.Core.Tests;

[Collection("Hashing")]
public sealed class SHA256ContentHasherTest : HashingTest
{
	protected override IContentHasher GetHasher()
		=> new SHA256ContentHasher();

	[Fact]
	public void TestSHA256ContentHasherIs32Bytes()
	{
		// Arrange
		var hasher = GetHasher();
		var data = new byte[] { 1, 2, 3 };

		// Act
		var hash = hasher.ComputeHash(data);

		// Assert
		Assert.Equal(32, hash.Length);
	}
}

/*
public sealed class MD5ContentHasherTest : HashingTest
{
	protected override IContentHasher GetHasher()
		=> new MD5ContentHasher();
}
*/

public abstract class HashingTest
{
	protected abstract IContentHasher GetHasher();

	[Fact]
	public void TestContentHasherNotNull()
	{
		// Arrange
		var hasher = GetHasher();
		var data = new byte[] { 1 };

		// Act
		var hash = hasher.ComputeHash(data);

		// Assert
		Assert.NotNull(hash);
	}

	[Fact]
	public void TestContentHasherEmptyData()
	{
		// Arrange
		var hasher = GetHasher();

		// Act & Assert
		Assert.Throws<ArgumentException>(() => hasher.ComputeHash([]));
	}

	[Fact]
	public void TestContentHasherSameData()
	{
		// Arrange
		var hasher = GetHasher();
		var data1 = new byte[] { 1, 2, 3 };
		var data2 = new byte[] { 1, 2, 3 };

		// Act
		var hash1 = hasher.ComputeHash(data1);
		var hash2 = hasher.ComputeHash(data2);

		// Assert
		Assert.Equal(hash1, hash2);
	}

	[Fact]
	public void TestContentHasherDifferentData()
	{
		// Arrange
		var hasher = GetHasher();
		var data1 = new byte[] { 1, 2, 3 };
		var data2 = new byte[] { 4, 5, 6 };

		// Act
		var hash1 = hasher.ComputeHash(data1);
		var hash2 = hasher.ComputeHash(data2);

		// Assert
		Assert.NotEqual(hash1, hash2);
	}
}