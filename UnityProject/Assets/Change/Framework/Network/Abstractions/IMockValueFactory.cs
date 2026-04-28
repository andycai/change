using System;

namespace Change.Framework.Network
{
    public interface IMockValueFactory
    {
        void Reset(int seed);

        bool NextBool();

        int NextInt(int minInclusive, int maxExclusive);

        float NextFloat(float minInclusive, float maxInclusive);

        string NextString(int length);

        Guid NextGuid();

        DateTimeOffset NextTimeUtc();
    }
}
