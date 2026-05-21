using System.Collections.Generic;
using System.Xml;

using LiveSplit.SmwCounters.Snes;
using LiveSplit.UI;

namespace LiveSplit.SmwCounters.Counters;

internal sealed class MoonCounter : ISmwCounter
{
    // SNES WRAM addresses (from kaizosplits Memory.cs).
    private const int MoonCounterOffset = 0x13C5; // # of 3-up moons collected, per scene
    private const int LevelNumOffset    = 0x13BF; // translevel number
    private const int RoomNumOffset     = 0x010B; // sublevel within current level

    private byte previousMoon;
    private bool hasPrevious;

    // Keys (level, or level+room) where a moon has been counted this session.
    private readonly HashSet<int> countedKeys = new();

    public string Id => "moons";
    public string DefaultGlyph => "🌙";
    public string DefaultLabel => "Moons";

    public int Value { get; private set; }

    // false => count one moon per translevel
    // true  => count one moon per (translevel, sublevel)
    public bool DedupePerRoom { get; set; }

    public void Reset()
    {
        Value = 0;
        countedKeys.Clear();
        hasPrevious = false;
    }

    public void Poll(ISnesMemory memory)
    {
        if (!memory.IsAttached)
        {
            hasPrevious = false;
            return;
        }

        if (!memory.ReadWramByte(MoonCounterOffset, out byte moon)
            || !memory.ReadWramByte(LevelNumOffset, out byte level)
            || !memory.ReadWramByte(RoomNumOffset, out byte room))
        {
            hasPrevious = false;
            return;
        }

        if (hasPrevious && moon > previousMoon)
        {
            int key = DedupePerRoom ? ((level << 8) | room) : level;
            if (countedKeys.Add(key))
            {
                Value++;
            }
        }
        previousMoon = moon;
        hasPrevious = true;
    }

    public void SaveState(XmlDocument doc, XmlElement parent)
    {
        SettingsHelper.CreateSetting(doc, parent, "Moons", Value);
        SettingsHelper.CreateSetting(doc, parent, "DedupePerRoom", DedupePerRoom);
    }

    public void LoadState(XmlElement parent)
    {
        Value = SettingsHelper.ParseInt(parent["Moons"], 0);
        DedupePerRoom = SettingsHelper.ParseBool(parent["DedupePerRoom"], false);
        // countedKeys is not persisted; after a layout reload, previously-seen
        // (level | level+room) keys can trigger another increment when revisited.
        countedKeys.Clear();
        hasPrevious = false;
    }
}
