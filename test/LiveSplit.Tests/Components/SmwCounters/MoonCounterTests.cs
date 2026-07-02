using System.Xml;

using LiveSplit.SmwCounters.Counters;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class MoonCounterTests
{
    // SMW WRAM addresses used by MoonCounter (matches kaizosplits Memory.cs).
    private const int Moons = 0x13C5; // # of 3-up moons collected, per scene
    private const int Level = 0x13BF; // translevel number
    private const int Room  = 0x010B; // sublevel within current level

    private static void Set(FakeSnesMemory m, byte moons, byte level, byte room)
    {
        m.SetByte(Moons, moons);
        m.SetByte(Level, level);
        m.SetByte(Room,  room);
    }

    [Fact]
    public void IncrementsOnRisingMoonByte()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter();

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void DedupesAcrossRoomsInPerLevelMode()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter { DedupeMode = MoonDedupeMode.PerLevel };

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem); // count once for level 1
        Set(mem, 0, 1, 2); c.Poll(mem); // moved to room 2, moon byte resets
        Set(mem, 1, 1, 2); c.Poll(mem); // another moon byte rise in same level

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsEachRoomInPerRoomMode()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter { DedupeMode = MoonDedupeMode.PerRoom };

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);
        Set(mem, 0, 1, 2); c.Poll(mem);
        Set(mem, 1, 1, 2); c.Poll(mem);

        Assert.Equal(2, c.Value);
    }

    [Fact]
    public void CountsAcrossDifferentLevelsInPerLevelMode()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter { DedupeMode = MoonDedupeMode.PerLevel };

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem); // level 1
        Set(mem, 0, 2, 0); c.Poll(mem);
        Set(mem, 1, 2, 0); c.Poll(mem); // level 2

        Assert.Equal(2, c.Value);
    }

    [Fact]
    public void DoesNotIncrementOnFirstPoll()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter();

        Set(mem, 1, 1, 0); c.Poll(mem); // first poll, even if moons==1, no edge yet

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void ResetClearsValueAndDedupeMemory()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter { DedupeMode = MoonDedupeMode.PerLevel };

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);
        c.Reset();
        Assert.Equal(0, c.Value);

        // After reset, the same level should be eligible again.
        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);
        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void StateRoundTripsThroughXml()
    {
        var mem = new FakeSnesMemory();
        var a = new MoonCounter { DedupeMode = MoonDedupeMode.PerRoom };
        Set(mem, 0, 1, 0); a.Poll(mem);
        Set(mem, 1, 1, 0); a.Poll(mem);

        var doc = new XmlDocument();
        var parent = doc.CreateElement("CounterState");
        doc.AppendChild(parent);
        a.SaveState(doc, parent);

        var b = new MoonCounter();
        b.LoadState(parent);

        Assert.Equal(1, b.Value);
        Assert.Equal(MoonDedupeMode.PerRoom, b.DedupeMode);
    }
}
