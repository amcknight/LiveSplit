using System.Xml;

using LiveSplit.SmwCounters.Counters;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class ExitCounterTests
{
    // SMW WRAM $1F2E (ExitsCompleted): single byte, incremented in bank_04
    // when the overworld event-process routine resolves a goal/secret event.
    private const int Exits = 0x1F2E;

    [Fact]
    public void NoEdgeOnFirstPollEvenIfExitsNonZero()
    {
        var mem = new FakeSnesMemory();
        var c = new ExitCounter();

        mem.SetByte(Exits, 1);
        c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void IncrementsOnRisingExitByte()
    {
        var mem = new FakeSnesMemory();
        var c = new ExitCounter();

        mem.SetByte(Exits, 0); c.Poll(mem);
        mem.SetByte(Exits, 1); c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void DoesNotDoubleCountWhileExitsByteIsStable()
    {
        var mem = new FakeSnesMemory();
        var c = new ExitCounter();

        mem.SetByte(Exits, 0); c.Poll(mem);
        mem.SetByte(Exits, 1); c.Poll(mem);
        c.Poll(mem); // still 1
        c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsMultipleExits()
    {
        var mem = new FakeSnesMemory();
        var c = new ExitCounter();

        mem.SetByte(Exits, 0); c.Poll(mem);
        mem.SetByte(Exits, 1); c.Poll(mem);
        mem.SetByte(Exits, 2); c.Poll(mem);
        mem.SetByte(Exits, 3); c.Poll(mem);

        Assert.Equal(3, c.Value);
    }

    [Fact]
    public void DetachClearsPreviousSoNextAttachWontCountSpuriously()
    {
        var mem = new FakeSnesMemory();
        var c = new ExitCounter();

        mem.SetByte(Exits, 0); c.Poll(mem);
        mem.IsAttached = false; c.Poll(mem);
        mem.IsAttached = true;
        mem.SetByte(Exits, 1); c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void ResetZeroesValueAndClearsEdgeState()
    {
        var mem = new FakeSnesMemory();
        var c = new ExitCounter();

        mem.SetByte(Exits, 0); c.Poll(mem);
        mem.SetByte(Exits, 1); c.Poll(mem);
        c.Reset();

        Assert.Equal(0, c.Value);

        c.Poll(mem); // exits byte still 1; no previous after reset, so no increment
        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void StateRoundTripsThroughXml()
    {
        var mem = new FakeSnesMemory();
        var a = new ExitCounter();
        mem.SetByte(Exits, 0); a.Poll(mem);
        mem.SetByte(Exits, 2); a.Poll(mem);

        var doc = new XmlDocument();
        var parent = doc.CreateElement("CounterState");
        doc.AppendChild(parent);
        a.SaveState(doc, parent);

        var b = new ExitCounter();
        b.LoadState(parent);

        Assert.Equal(1, b.Value);
    }
}
