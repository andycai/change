using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class DefaultMockValueFactory : IMockValueFactory
    {
        private readonly DeterministicRandom _random;

        public DefaultMockValueFactory(DeterministicRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public void Reset(int seed)
        {
            _random.Reset(seed);
        }

        public bool NextBool()
        {
            return _random.NextBool();
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.NextInt(minInclusive, maxExclusive);
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            return _random.NextFloat(minInclusive, maxInclusive);
        }

        public string NextString(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (length == 0) return string.Empty;

            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            
            // Using stackalloc for small strings to avoid intermediate array
            if (length <= 256)
            {
                Span<char> buffer = stackalloc char[length];
                for (var i = 0; i < length; i++)
                {
                    buffer[i] = chars[_random.NextInt(0, chars.Length)];
                }
                return new string(buffer);
            }
            else
            {
                var buffer = new char[length];
                for (var i = 0; i < length; i++)
                {
                    buffer[i] = chars[_random.NextInt(0, chars.Length)];
                }
                return new string(buffer);
            }
        }

        public Guid NextGuid()
        {
            Span<byte> bytes = stackalloc byte[16];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)_random.NextInt(0, 256);
            }
            return new Guid(bytes);
        }

        public DateTimeOffset NextTimeUtc()
        {
            var seconds = _random.NextInt(0, 7 * 24 * 60 * 60);
            return DateTimeOffset.UnixEpoch.AddSeconds(seconds);
        }
    }
}
