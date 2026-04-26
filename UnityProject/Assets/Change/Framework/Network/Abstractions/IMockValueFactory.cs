using System;

namespace Change.Framework.Network
{
    public interface IMockValueFactory
    {
        bool NextBool();

        int NextInt(int minInclusive, int maxExclusive);

        float NextFloat(float minInclusive, float maxInclusive);

        string NextString(int length);

        Guid NextGuid();

        DateTimeOffset NextTimeUtc();
    }
}
