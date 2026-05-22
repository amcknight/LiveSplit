# SMW counters: replace emoji glyphs with SMW sprites

## Problem

`SmwCountersComponent` currently displays each counter's `DefaultGlyph` —
`💀` for `DeathCounter` and `🌙` for `MoonCounter` — in the layout row. These
render as `□` boxes in practice because LiveSplit's text font is whatever
the user selected in layout settings (a normal display font), and those
fonts almost never carry color-emoji glyphs.

The component also draws a `◌` (dotted circle) at the bottom-right of the
row when the SNES emulator isn't attached. This sits in the same risky-
glyph territory and adds clutter where the speedrunner doesn't look.

## Goal

Replace the emoji glyphs with 16×16 sprites ripped from Super Mario World
(small-Mario death pose for deaths, 3-up moon for moons). Render them
inline as pixel art at the user's text size, without recoloring. Remove
the layout-row status indicator entirely; attach state stays available in
the settings dialog where it already lives.

## Non-goals

- No per-counter icon override in settings. The user can still override
  the label with text; if the override is empty the default sprite shows.
- No SVG, no multi-DPI variants, no tinting to match `TextColor`. The
  sprites are pixel art and look right at native colors.
- No fallback chain (e.g. "if icon resource fails to load, draw the old
  emoji glyph"). If the embedded resource is missing the row falls back
  to drawing `DefaultLabel` as text.

## Design

### Assets

- New folder `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Assets/`
  containing:
  - `death.png` — small-Mario death-pose sprite, 16×16, transparent background.
  - `moon.png` — 3-up moon sprite, 16×16, transparent background.
- Source: the canonical SMW sprite sheet on Spriters Resource. The
  death-pose frame is the start-of-death-animation frame (eyes closed,
  arms raised). The moon is the standard yellow crescent that's already
  what speedrunners recognize.
- Add `components/LiveSplit.SmwCounters/CREDITS.md` noting the sprites
  are from *Super Mario World* (Nintendo, 1990), used as fan-community
  speedrunning iconography. Consistent with other LiveSplit components
  that ship game art.

### csproj wiring

`LiveSplit.SmwCounters.csproj` gets:

```xml
<ItemGroup>
  <EmbeddedResource Include="Assets\death.png">
    <LogicalName>LiveSplit.SmwCounters.Assets.death.png</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Include="Assets\moon.png">
    <LogicalName>LiveSplit.SmwCounters.Assets.moon.png</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

`<LogicalName>` pins the manifest resource name. The csproj's
`RootNamespace` is `LiveSplit` (not `LiveSplit.SmwCounters`), so the
default-derived names would not match the directory-based namespace
convention `IconLoader.Load` callers will use. Pinning makes the
resource names predictable.

The sprites travel inside the component DLL — nothing new lands in the
`Components/` output folder, so no `PostBuildEvent` cleanup-list updates
are needed in the `LiveSplit` app csproj.

### `ISmwCounter` contract change

Replace `string DefaultGlyph` with `Image DefaultIcon`:

```csharp
public interface ISmwCounter
{
    string Id { get; }
    string DefaultLabel { get; }
    Image DefaultIcon { get; }     // new
    // removed: string DefaultGlyph { get; }
    int Value { get; set; }
    void Reset();
    void Poll(ISnesMemory memory);
    void SaveState(XmlDocument doc, XmlElement parent);
    void LoadState(XmlElement parent);
}
```

Each counter exposes a lazily-loaded `Bitmap` from its embedded resource.
A small `IconLoader` helper in `Counters/` centralizes the
`Assembly.GetManifestResourceStream(...)` → `Bitmap` plumbing:

```csharp
internal static class IconLoader
{
    public static Bitmap Load(string resourceName) { /* GetManifestResourceStream + new Bitmap */ }
}
```

`DeathCounter.DefaultIcon` returns `IconLoader.Load("LiveSplit.SmwCounters.Assets.death.png")`
(cached in a static field); same shape for `MoonCounter` with `moon.png`.

### Rendering

In `SmwCountersComponent.DrawGeneral`:

1. For each enabled counter, compute the "label slot":
   - If the user supplied a label override → draw it as text via the
     existing `SimpleLabel` path (`labelCells[c.Id]`).
   - Else if `c.DefaultIcon != null` → draw the icon via `g.DrawImage`.
   - Else → draw `c.DefaultLabel` as text (fallback if a future counter
     ships without an icon).

2. Icon draw block:
   ```csharp
   int iconSize = (int)Math.Round(0.85f * textHeight);
   var prevInterp = g.InterpolationMode;
   var prevOffset = g.PixelOffsetMode;
   g.InterpolationMode = InterpolationMode.NearestNeighbor;
   g.PixelOffsetMode = PixelOffsetMode.Half;
   g.DrawImage(c.DefaultIcon, x, (height - iconSize) / 2f, iconSize, iconSize);
   g.InterpolationMode = prevInterp;
   g.PixelOffsetMode = prevOffset;
   ```
   `NearestNeighbor` + `PixelOffsetMode.Half` keeps the pixel art crisp
   at any text size — at 12pt (~16px) it draws native, at 24pt it's a
   clean 2× upscale instead of a smear.

3. Width measurement: when the slot is an icon, the slot's width is
   `iconSize` (not `g.MeasureString(...)`).

4. `GraphicsCache` key for the label slot becomes either the override
   text (when one exists) or the literal string `"<icon>"`. The cache
   invalidates correctly when the user toggles between override-text
   and the default icon. No `TextColor` participation is needed since
   icons aren't tinted.

### Status indicator removal

In `Update` and `DrawGeneral`, delete:

- The `statusCell` field and its `cache["status"]` entry.
- The `string status = emu.IsAttached ? "" : "◌";` block.
- The `statusW` portion of `HorizontalWidth` and the trailing
  `if (!string.IsNullOrEmpty(statusCell.Text)) { ConfigureLabel(...); statusCell.Draw(g); }`.

`Settings.StatusProvider = () => emu.Describe();` and the
`(detecting…)` label in the settings dialog stay — that's where attach
state belongs.

### Settings UI hint text

In `SmwCountersComponentSettings.BuildUi`:

- Drop `string DefaultGlyph` from the row tuple's shape — the component
  no longer needs to pass it in.
- Change `Text = $"Label (blank = {defaultGlyph}):"` to
  `Text = "Label (blank = icon):"`.

### Tests

Existing counter tests don't touch glyphs or rendering — they stay
green as-is. No new tests are added:

- `ColorMatrix`/`ImageAttributes` tinting was the only piece worth
  unit-testing, and it's gone.
- The icon-vs-text branch is a single `if` in `DrawGeneral`; covering
  it with a unit test would require mocking `Graphics`, which costs
  more than it pays back.

Verification is manual: build, launch LiveSplit, add the SMW Counters
component to a layout, confirm both rows render the sprites cleanly at
the default font and at 2× font size, confirm no `◌` ever appears, and
confirm typing into the override box swaps the icon for the text.

## Out of scope

- Anything beyond this component (other emoji glyphs elsewhere in
  LiveSplit, font handling for the rest of the app).
- Replacing the SMW sprites with custom art or per-game icon packs.
- Adding new counters.
