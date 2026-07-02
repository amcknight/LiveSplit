using System.Xml;

using LiveSplit.SmwCounters.Counters;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class JumpCounterTests
{
    // Source: SMWDisX rammap.asm + bank_00/bank_01.
    //   $7E:0072  PlayerInAir       $00 on ground, $0B rising/jumping, $0C
    //                               P-speed rising, $24 falling.
    //   $7E:0077  PlayerBlockedDir  bit 2 ($04) = blocked below (= touching
    //                               ground/solid below). Cleared during enemy
    //                               bounces, which is the disambiguator
    //                               between a real ground-jump and a bounce.
    private const int PlayerInAir   = 0x0072;
    private const int BlockedDir    = 0x0077;

    private const byte OnGround     = 0x00;
    private const byte AirRising    = 0x0B;
    private const byte AirRisingP   = 0x0C; // P-speed jump (held jump at high speed)
    private const byte AirFalling   = 0x24;

    private const byte BlockedBelow = 0x04;

    private static void Set(FakeSnesMemory m, byte air, byte blocked)
    {
        m.SetByte(PlayerInAir, air);
        m.SetByte(BlockedDir, blocked);
    }

    [Fact]
    public void CountsGroundJump()
    {
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem); // standing
        Set(mem, AirRising, 0);           c.Poll(mem); // pressed B

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsPSpeedJump()
    {
        // P-speed jump uses $0C instead of $0B; same rising-edge semantics.
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        Set(mem, AirRisingP, 0);          c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void DoesNotCountWalkOffLedge()
    {
        // Walking off a ledge transitions $00 → $24 (falling), never to $0B.
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        Set(mem, AirFalling, 0);          c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void DoesNotCountMidAirBPress()
    {
        // Already in the air on the previous poll, so was_on_ground is false
        // even if the engine briefly re-asserts $0B.
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, AirRising, 0); c.Poll(mem); // already rising
        Set(mem, AirRising, 0); c.Poll(mem); // still rising

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void DoesNotCountKoopaBounceWhenPollerCatchesIntermediateGroundFrame()
    {
        // The bounce-off-enemy routine briefly does STZ $0072 then writes
        // $0B if A/B is held. At 15ms polling vs 16.67ms frames, the poller
        // can sample $0072 == $00 mid-bounce. The $0077 & $04 gate is what
        // keeps this from being counted — during a sprite bounce, Mario is
        // not touching ground, so the blocked-below bit is clear.
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, AirFalling, 0); c.Poll(mem); // descending toward Koopa
        Set(mem, OnGround,   0); c.Poll(mem); // bounce frame: $00 but NOT on ground
        Set(mem, AirRising,  0); c.Poll(mem); // engine writes $0B post-bounce

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void CountsGroundJumpEvenIfPlayerWasFallingTwoPollsAgo()
    {
        // Landing then immediately jumping: previous poll caught the on-ground
        // frame, so the rising-edge counts.
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, AirFalling, 0);          c.Poll(mem); // descending
        Set(mem, OnGround, BlockedBelow); c.Poll(mem); // landed
        Set(mem, AirRising, 0);           c.Poll(mem); // jumped

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void NoEdgeOnFirstPollEvenIfAlreadyJumping()
    {
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, AirRising, 0);
        c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void DoesNotDoubleCountWhileAirStateIsStable()
    {
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        Set(mem, AirRising, 0);           c.Poll(mem); // jump
        c.Poll(mem); c.Poll(mem); c.Poll(mem);          // still rising — no new edge

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsMultipleJumpsAcrossSession()
    {
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        Set(mem, AirRising, 0);           c.Poll(mem); // jump 1
        Set(mem, AirFalling, 0);          c.Poll(mem); // peaked, falling
        Set(mem, OnGround, BlockedBelow); c.Poll(mem); // landed
        Set(mem, AirRising, 0);           c.Poll(mem); // jump 2
        Set(mem, AirFalling, 0);          c.Poll(mem);
        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        Set(mem, AirRisingP, 0);          c.Poll(mem); // jump 3 (P-speed)

        Assert.Equal(3, c.Value);
    }

    [Fact]
    public void DetachClearsPreviousSoNextAttachWontCountSpuriously()
    {
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        mem.IsAttached = false;           c.Poll(mem);
        mem.IsAttached = true;
        Set(mem, AirRising, 0);           c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void ResetZeroesValueAndClearsEdgeState()
    {
        var mem = new FakeSnesMemory();
        var c = new JumpCounter();

        Set(mem, OnGround, BlockedBelow); c.Poll(mem);
        Set(mem, AirRising, 0);           c.Poll(mem);
        c.Reset();

        Assert.Equal(0, c.Value);

        // After reset there is no previous sample, so even with rising state
        // visible there should be no increment until a fresh edge forms.
        c.Poll(mem);
        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void StateRoundTripsThroughXml()
    {
        var mem = new FakeSnesMemory();
        var a = new JumpCounter();
        Set(mem, OnGround, BlockedBelow); a.Poll(mem);
        Set(mem, AirRising, 0);           a.Poll(mem);
        Set(mem, AirFalling, 0);          a.Poll(mem);
        Set(mem, OnGround, BlockedBelow); a.Poll(mem);
        Set(mem, AirRising, 0);           a.Poll(mem);

        var doc = new XmlDocument();
        var parent = doc.CreateElement("CounterState");
        doc.AppendChild(parent);
        a.SaveState(doc, parent);

        var b = new JumpCounter();
        b.LoadState(parent);

        Assert.Equal(2, b.Value);
    }
}
