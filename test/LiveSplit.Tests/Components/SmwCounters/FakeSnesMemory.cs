using System.Collections.Generic;

using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.Tests.Components.SmwCounters;

internal sealed class FakeSnesMemory : ISnesMemory
{
    private readonly Dictionary<int, byte> bytes = new();

    public bool IsAttached { get; set; } = true;

    public void SetByte(int offset, byte value) => bytes[offset] = value;

    public bool ReadWramByte(int snesOffset, out byte value)
    {
        if (!IsAttached)
        {
            value = 0;
            return false;
        }
        value = bytes.TryGetValue(snesOffset, out byte b) ? b : (byte)0;
        return true;
    }
}
