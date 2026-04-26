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

            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var buffer = new char[length];

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = chars[_random.NextInt(0, chars.Length)];
            }

            return new string(buffer);
        }

        public Guid NextGuid()
        {
            var bytes = new byte[16];

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
