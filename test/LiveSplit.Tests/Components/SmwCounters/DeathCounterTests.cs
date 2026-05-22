using System.Xml;

using LiveSplit.SmwCounters.Counters;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class DeathCounterTests
{
    // SMW Mario animation $7E:0071. 0x09 == dying.
    private const int Anim = 0x71;

    [Fact]
    public void NoEdgeOnFirstPollEvenIfDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x09);
        c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void IncrementsOnTransitionIntoDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00);
        c.Poll(mem);
        mem.SetByte(Anim, 0x09);
        c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void DoesNotDoubleCountWhileStillDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);
        c.Poll(mem); // still 0x09
        c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsAgainAfterLeavingAndReenteringDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);
        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);

        Assert.Equal(2, c.Value);
    }

    [Fact]
    public void DetachClearsPreviousSoNextAttachWontCountSpuriously()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.IsAttached = false; c.Poll(mem);
        mem.IsAttached = true;
        mem.SetByte(Anim, 0x09); c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void ResetZeroesValueAndClearsEdgeState()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);
        c.Reset();

        Assert.Equal(0, c.Value);

        // Currently 0x09 in memory; resetting should clear hasPrevious so we don't count it again next tick.
        c.Poll(mem);
        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void StateRoundTripsThroughXml()
    {
        var mem = new FakeSnesMemory();
        var a = new DeathCounter();
        mem.SetByte(Anim, 0x00); a.Poll(mem);
        mem.SetByte(Anim, 0x09); a.Poll(mem);

        var doc = new XmlDocument();
        var parent = doc.CreateElement("CounterState");
        doc.AppendChild(parent);
        a.SaveState(doc, parent);

        var b = new DeathCounter();
        b.LoadState(parent);

        Assert.Equal(1, b.Value);
    }
}
