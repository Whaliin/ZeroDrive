using ZeroDrive.Core.Chunking;
using ZeroDrive.Core.DTOs;
using ZeroDrive.Core.Hashing;
using ZeroDrive.Tests.Helpers;

namespace ZeroDrive.Core.Tests;

[Collection("Chunking")]
public class FixedSizeChunkerTests : ChunkerBaseTests
{
	// Because the fixed size chunker is more deterministic and predictable, we can test for specific chunk sizes and counts.

	private const int ChunkSizeToTest = 1024;

	protected override ChunkerBase CreateChunker(IContentHasher hasher)
	{
		return new FixedSizeChunker(hasher, ChunkSizeToTest);
	}

	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestFixedSizeChunkerGivesFixedSizeOutput(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		Assert.All(chunks, (c) => Assert.Equal(ChunkSizeToTest, c.Data.Length));
	}

	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestFixedSizeChunkerGivesCorrectNumberOfChunks(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize % ChunkSizeToTest + 1); // Add some offset to the data size to ensure we get uneven data.
								// Ceil rounds upward from 1.1 to 2 so we get the right number of chunks for uneven data sizes.
		var expectedChunkCount = (int)Math.Ceiling((double)data.Length / ChunkSizeToTest);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		Assert.Equal(expectedChunkCount, chunks.Count);
	}
}

public abstract class ChunkerBaseTests
{
#region Test helpers
	// Define hashers once
	public static TheoryData<IContentHasher> Hashers => new()
	{
		new SHA256ContentHasher()
	};

	// Define valid kinds of input
	public enum InputKind
	{
		Stream,
		ByteArray,
		File
	};

	// Generate valid inputs dynamically
	public static TheoryData<IContentHasher, InputKind> ValidInputs
	{
		get
		{
			var data = new TheoryData<IContentHasher, InputKind>();
			foreach (var hasher in Hashers)
			{
				foreach (var kind in Enum.GetValues<InputKind>())
				{
					data.Add(hasher, kind);
				}
			}
			return data;
		}
	}

	protected abstract ChunkerBase CreateChunker(IContentHasher hasher);

	public static List<ZChunk> GetChunks(
		ChunkerBase chunker,
		byte[] data,
		InputKind inputKind)
	{
		// Using ToList() forces materialization on yield statements
		switch (inputKind)
		{
			case InputKind.Stream:
				using (var stream = new MemoryStream(data))
				{
					return chunker.ChunkData(stream).ToList();
				}
			case InputKind.ByteArray:
				return chunker.ChunkData(data).ToList();
			case InputKind.File:
				var tempFilePath = TestHelpers.GenerateRandomFile(data.Length);
				try
				{
					return chunker.ChunkFile(tempFilePath).ToList();
				}
				finally
				{
					File.Delete(tempFilePath);
				}
			default:
				throw new ArgumentOutOfRangeException(nameof(inputKind), inputKind, null);
		}
	}

	protected const int TestDataSize = 1024 * 1024 * 50; // 50 MB
#endregion


	[Theory]
	[MemberData(nameof(Hashers))]
	public void TestChunkerNotNull(IContentHasher hasher)
	{
		// Arrange & Act
		var chunker = CreateChunker(hasher);

		// Assert
		Assert.NotNull(chunker);
	}

	[Theory]
	[MemberData(nameof(Hashers))]
	public void TestChunkerWithEmptyStream(IContentHasher hasher)
	{
		// Arrange
		var chunker = CreateChunker(hasher);

		// Act & Assert
		Assert.Throws<ArgumentException>(() => chunker.ChunkData(new MemoryStream()).ToList());
	}

	[Theory]
	[MemberData(nameof(Hashers))]
	public void TestChunkerWithNullStream(IContentHasher hasher)
	{
		// Arrange
		var chunker = CreateChunker(hasher);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => chunker.ChunkData((Stream)null!).ToList());
	}

	[Theory]
	[MemberData(nameof(Hashers))]
	public void TestChunkerWithNullByteArray(IContentHasher hasher)
	{
		// Arrange
		var chunker = CreateChunker(hasher);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => chunker.ChunkData((byte[])null!).ToList());
	}

	[Theory]
	[MemberData(nameof(Hashers))]
	public void TestChunkerWithEmptyFilePath(IContentHasher hasher)
	{
		// Arrange
		var chunker = CreateChunker(hasher);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => chunker.ChunkFile("").ToList());
	}

	[Theory]
	[MemberData(nameof(Hashers))]
	public void TestChunkerWithNullFilePath(IContentHasher hasher)
	{
		// Arrange
		var chunker = CreateChunker(hasher);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => chunker.ChunkFile(null!).ToList());
	}

	public static TheoryData<IContentHasher, string> InvalidPathNames 
	{
		get
		{
			var data = new TheoryData<IContentHasher, string>();
			var invalidChars = Path.GetInvalidPathChars().Union(Path.GetInvalidFileNameChars());
			foreach (var hasher in Hashers)
			{
				foreach (var invalidChar in invalidChars)
				{
					data.Add(hasher, invalidChar.ToString());
				}
			}
			return data;
		}
	}

	[Theory]
	[MemberData(nameof(InvalidPathNames))]
	public void TestChunkerWithInvalidFilePath(IContentHasher hasher, string filename)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		//using var tempDir = new TestDir();
		//var fp = tempDir.CreateTestPath(filename);

		// Act & Assert - I don't know what exception this should raise so just any is fine.
		// TODO: Make this more specific
		Assert.Throws<Exception>(() => chunker.ChunkFile(filename).ToList());
	}

	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestChunkerWithValidInput(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		Assert.True(chunks.Count > 0);
	}

	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestChunkerGivesHashedOutput(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		Assert.All(chunks, (c) => Assert.NotNull(c.Meta.Hash));
	}

	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestChunkerGivesIndexedOutput(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		Assert.All(chunks, (c) => Assert.True(c.Meta.Index >= 0));
	}

	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestChunkerGivesNonEmptyOutput(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		// Because the length of the data can not be determined prior to chunking, 
		// we can only assert that valid data generates at least one non-empty chunk.
		Assert.All(chunks, (c) => Assert.True(c.Data.Length > 0));
	}

	/// <summary>
	/// Test that each chunker gives valid order of indices.
	/// This means: Chunker indices go from 1,2,3,4...etc. 
	/// Because most content chunking algorithms should be a sequential. 
	/// </summary>
	/// <param name="hasher"></param>
	/// <param name="inputKind"></param>
	[Theory]
	[MemberData(nameof(ValidInputs))]
	public void TestChunkerGivesChunkOrder(IContentHasher hasher, InputKind inputKind)
	{
		// Arrange
		var chunker = CreateChunker(hasher);
		var data = TestHelpers.GenerateRandomData(TestDataSize);

		// Act
		var chunks = GetChunks(chunker, data, inputKind);

		// Assert
		Assert.NotEmpty(chunks);

		var expectedIndices = Enumerable
			.Range(0, chunks.Count) // Create numbers between 0 and the number of chunks
			.Select(index => (ulong)index) // Convert to ulong for comparison equality
			.ToArray();

		var actualIndices = chunks
			.Select(chunk => chunk.Meta.Index) // Extract indices
			.OrderBy(index => index) // Order by the indices
			.ToArray();

		Assert.Equal(expectedIndices, actualIndices);
	}
}