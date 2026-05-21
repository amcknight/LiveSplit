# SMW Counters Unification — Design

**Date**: 2026-05-21
**Status**: Approved for planning

## Problem

`LiveSplit.SmwDeathCounter` and the in-progress `LiveSplit.SmwMoonCounter` are near-duplicate plugins. Each has its own:
- `SnesEmu` (emulator detection, WRAM base resolution, byte reads) — identical between the two
- `Offsets` table (process names, version table, RetroArch core pointers) — identical between the two
- Component + Settings + Factory pair, differing only in which WRAM bytes are watched and how the increment is debounced

This means the user adds two layout components, gets two separate "attach to emulator" status indicators, two reset hotkeys, two settings dialogs — when conceptually there's one source (SMW running in an emulator) producing several counters.

## Goal

Replace the two plugins with one plugin (`LiveSplit.SmwCounters`) that:
1. Lets the user pick which SMW counters they want from a single settings dialog.
2. Renders the enabled counters as a single horizontal row when more than one is enabled.
3. Shares one `SnesEmu` instance, one poll loop, one attach-status indicator, one reset hotkey.
4. Is structured so new SMW counters (coins, exits, Yoshi coins, …) can be added without touching component/rendering/settings code.

Non-goals:
- Bundling counters for games other than SMW. (PressCounter stays independent.)
- Per-counter hotkeys, per-counter colors, per-counter fonts.
- Bundling icon fonts or PNG assets in v1 — text/emoji glyphs only.

## Architecture

### New project

A new component submodule, `components/LiveSplit.SmwCounters`, replacing both `LiveSplit.SmwDeathCounter` and `LiveSplit.SmwMoonCounter`. Both existing plugins are removed from `LiveSplit.sln` and from the super-repo's submodule list. They were never released, so no migration path is needed for end users.

Project layout mirrors the existing component template:

```
components/LiveSplit.SmwCounters/
  Directory.Build.props
  props/
  src/LiveSplit.SmwCounters/
    LiveSplit.SmwCounters.csproj
    Snes/
      SnesEmu.cs
      Offsets.cs
    Counters/
      ISmwCounter.cs
      DeathCounter.cs
      MoonCounter.cs
    UI/Components/
      SmwCountersComponent.cs
      SmwCountersComponentFactory.cs
      SmwCountersComponentSettings.cs
      SmwCountersComponentSettings.Designer.cs
```

### Counter abstraction

```csharp
internal interface ISmwCounter
{
    string Id { get; }            // stable serialization key, e.g. "deaths"
    string DefaultGlyph { get; }  // "💀", "🌙"
    string DefaultLabel { get; }  // "Deaths", "Moons" — for the settings row label
    int Value { get; }
    void Reset();
    void Poll(SnesEmu emu);       // reads its own offsets, increments Value
    void SaveValue(XmlDocument doc, XmlElement parent);
    void LoadValue(XmlElement parent);
}
```

Concrete implementations:

- `DeathCounter` — watches `$7E:0071` (Mario animation), increments on `0x09` edge. Same logic as today's `SmwDeathCounterComponent.Poll`.
- `MoonCounter` — watches `$7E:13C5` (moon count) edge plus `$7E:13BF`/`$7E:010B` (level/room) for dedupe. Same logic as today's `SmwMoonCounterComponent.Poll`. Carries its own `DedupePerRoom` setting and `HashSet<int> countedKeys`.

The component instantiates the full set of counters at construction (`new DeathCounter()`, `new MoonCounter()`) and stores them in a list. Whether each is "active" is a separate enabled-set on the component.

### Component

`SmwCountersComponent : IComponent` holds:
- `IReadOnlyList<ISmwCounter> allCounters` — the registry, populated in the constructor.
- One shared `SnesEmu emu`.
- One shared `Timer pollTimer` at 15ms.
- `SmwCountersComponentSettings Settings` — owns the enabled set and per-counter label overrides.

Each poll tick:
1. `emu.TryAttach()`. On failure, return.
2. For each `c` in `allCounters` where `Settings.IsEnabled(c.Id)`: `c.Poll(emu)`.

Reset hotkey calls `Reset()` on every enabled counter.

### Settings storage

`SmwCountersComponentSettings` extends the shared base used by the other components and adds:

| Field | Type | Notes |
|---|---|---|
| `EnabledIds` | `HashSet<string>` | Counter `Id`s the user has ticked. |
| `LabelOverrides` | `Dictionary<string, string>` | `Id → label text`. Missing key = use `DefaultGlyph`. |
| `ResetKey` | `KeyOrButton` | Single shared hotkey, same as today. |
| Per-counter values | written via `ISmwCounter.SaveValue` | Each counter writes its own state under its `Id`. |

XML shape:
```xml
<Settings>
  <Version>1</Version>
  <ResetKey>...</ResetKey>
  <EnabledCounters>
    <Counter>deaths</Counter>
    <Counter>moons</Counter>
  </EnabledCounters>
  <CounterLabels>
    <Label id="deaths">💀</Label>
    <Label id="moons">🌙</Label>
  </CounterLabels>
  <CounterState>
    <Deaths>...</Deaths>
    <Moons>...</Moons>
    <MoonDedupe>...</MoonDedupe>
  </CounterState>
</Settings>
```

`SetSettings` tolerates missing entries (forward-compat for new counters; backward-compat for older saved layouts during development).

### Rendering

Layout per cell: `<label><sp><value>` rendered with the layout's text font/color/shadow/outline — same `ConfigureLabel` pattern as the existing components.

Horizontal layout mode:
- Cells laid out left-to-right with a fixed gap (e.g. 12px).
- `HorizontalWidth` = sum(cell widths) + gaps + status-glyph width + paddings.

Vertical layout mode:
- Cells *still* laid out horizontally on one line. `VerticalHeight` = one text row.
- This intentionally diverges from how Splits-style components grow vertically — the user asked for "nice horizontal way on one line" regardless of LiveSplit orientation. A runner who wants two SMW counters stacked vertically can add the component twice with one counter enabled per instance.

Status glyph (`" ◌"` when `!emu.IsAttached`) is appended **once**, at the right edge, after the last value cell — not per counter.

`GraphicsCache` keys on the joined `(label, value)` pairs plus the status glyph so re-paints only happen when something actually changed.

### Settings UI

WinForms designer-driven control matching the existing `Smw*ComponentSettings` style. Two sections:

**Counters** (top): a row per known counter containing:
- Enable checkbox
- Label textbox (placeholder = `DefaultGlyph`, blank = use default)
- "Reset value" button (resets just this counter to 0)
- Counter-specific options shown beneath when expanded (e.g. Moons' "dedupe per room" checkbox)

**Shared** (bottom):
- Reset hotkey (with gamepad pass-through honoring `state.Settings.HotkeyProfiles.First().Value.AllowGamepadsAsHotkeys`, same as today)
- Read-only status line bound to `emu.Describe()`

### Component naming

- `ComponentName` returns `"SMW Counters"`.
- Factory's `ComponentName`/`Description` reflect the same.
- Built DLL is `LiveSplit.SmwCounters.dll`, picked up automatically by `ComponentManager` from `bin/<config>/Components/`.

## Defaults

| Counter | DefaultGlyph | DefaultLabel |
|---|---|---|
| Deaths | 💀 | Deaths |
| Moons | 🌙 | Moons |

Runners whose font doesn't render emoji can replace the glyph with any text (e.g. "D" or "Deaths") via the settings dialog. PNG/SVG icons are explicitly out of scope for v1.

## Testing

`test/LiveSplit.Tests` already references component projects directly, so add coverage in-process:

- `DeathCounter` and `MoonCounter` unit tests use a stub/fake `SnesEmu` (extract a small interface for `ReadWramByte` plus `TryAttach`/`IsAttached`) and feed scripted byte sequences:
  - Death: `0x00 → 0x09` increments by 1; `0x09 → 0x09` does not.
  - Moon: rising moon-byte edge counts once per `levelKey` (and per `(level,room)` when `DedupePerRoom` is on).
- `SmwCountersComponent` settings round-trip test: build settings with two counters enabled and non-default labels, `GetSettings` → `SetSettings` into a new instance, assert equality of enabled set, labels, and counter values.
- Settings forward-compat test: parse a `<Settings>` blob missing `CounterLabels` and `CounterState` entries; assert defaults apply.

## Risks / open trade-offs

- **Emoji rendering** depends on the layout font. If the chosen font lacks emoji glyphs, the user sees a tofu box until they change the label. Mitigated by the inline editable label and a visible default in the settings textbox placeholder.
- **Removing the two existing submodules** means anyone with a half-built local layout pointing at the old DLLs will need to re-add the new component. Acceptable because neither plugin has shipped a release.
- **Single shared hotkey** means a runner can't reset just deaths without also resetting moons. The "Reset value" button per counter in the settings dialog covers the rare manual case. Per-counter hotkeys are deferred until a runner actually asks.

## Out of scope

- Generalizing to non-SMW counter aggregators.
- Bundled icon assets, icon-font rendering.
- Per-counter color/font overrides.
- Counter chaining/conditional display (e.g. "show moons only after first moon collected").
