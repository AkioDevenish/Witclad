namespace Wildtide.Core
{
    /// <summary>Randomness behind an interface so battles can be replayed and tested.</summary>
    public interface IRng
    {
        /// <summary>Integer in [min, max).</summary>
        int Range(int min, int max);
        /// <summary>Float in [0, 1).</summary>
        float Value();
    }

    public sealed class SystemRng : IRng
    {
        readonly System.Random random;
        public SystemRng(int? seed = null) { random = seed.HasValue ? new System.Random(seed.Value) : new System.Random(); }
        public int Range(int min, int max) => random.Next(min, max);
        public float Value() => (float)random.NextDouble();
    }
}
