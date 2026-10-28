// TODO: Zstd compression?

public static class EntropyCalculator
{
    public static double Calculate(ReadOnlySpan<byte> data)
    {
        // 1. Stack-allocate an integer histogram of size 256
        // 2. Linear scan over the span to tally byte frequencies
        // 3. Loop over the histogram:
        //    - If count > 0, compute probability: p = count / (double)data.Length
        //    - Add (-p * Math.Log2(p)) to the running total
        // 4. Return total entropy (in bits per byte, range [0.0, 8.0])
		return 0;
    }
}