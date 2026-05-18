# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

LiveSplit targets **.NET Framework 4.8.1** but builds with the **.NET 8.0 SDK** (via `Microsoft.NET.Sdk`). C# `LangVersion` is 12, `Nullable` is disabled, and `EnforceCodeStyleInBuild` is on. Windows-only (WinForms).

```powershell
# Build everything (after submodules are checked out — see Submodules below)
dotnet build LiveSplit.sln

# Release build matching CI
dotnet build -c Release -p:DebugType=None LiveSplit.sln

# Run the whole test suite
dotnet test LiveSplit.sln

# Run a single test class / single test
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~TimeMust"
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName=LiveSplit.Tests.Model.TimeMust.ConstructZeroCorrectly"

# Build only the auto-update tool (uses a custom config name, not Debug/Release)
dotnet build -c UpdateManagerExe src\UpdateManager\UpdateManager.csproj
```

Build output goes to `bin\<configuration>\` (per `props\LiveSplit.Paths.props`); package/intermediate artifacts go to `artifacts\` because `UseArtifactsOutput=true` is set globally in `Directory.Build.props`. The runnable app is `bin\Debug\LiveSplit.exe` (or `bin\Release\LiveSplit.exe`).

Tests use **xUnit**. The test project (`test\LiveSplit.Tests`) references most component projects directly so component-level logic is exercised in-process.

## Submodules

**Almost everything under `components\` and `lib\` is a git submodule** (each component, plus `livesplit-core`, `SpeedrunComSharp`, `CustomFontDialog`, `WinForms Color Picker`, `VLC`). A fresh clone without submodules **will not build**. After cloning or pulling:

```powershell
git submodule update --init --recursive
```

When working on a component (e.g. `components\LiveSplit.Splits`), you're inside a separate repository — commits/branches there are independent of the LiveSplit super-repo, and changes need their own PR in the component's repo before being picked up here as a submodule bump.

## Architecture

### Three core projects (`src\`)

- **`LiveSplit.Core`** — model layer. Contains the timer state machine (`Model\LiveSplitState.cs`, `ITimerModel`), run / segment / attempt types, comparison generators (`Model\Comparisons\`), run loaders/savers for various splits formats (`Model\RunFactories\`, `Model\RunSavers\`), input handling, NTP sync, the component plugin contracts (`UI\Components\`), the layout system (`UI\Layout.cs`, `UI\LayoutFactories\`, `UI\LayoutSavers\`), and the **CommandServer** (`Server\CommandServer.cs`, `Server\Connection.cs`, `Server\WsConnection.cs`) implementing the named-pipe / TCP / WebSocket server documented in README. New server commands are added in `ProcessMessage` in `CommandServer.cs`.
- **`LiveSplit.View`** — WinForms UI. `View\TimerForm.cs` is the main window; other dialogs live alongside it (`RunEditorDialog`, `LayoutEditorDialog`, `SettingsDialog`, etc.). `Properties\Settings.Designer.cs` holds persisted user settings.
- **`LiveSplit`** — thin `WinExe` entry point (`Program.cs` → `TimerForm`). Accepts `-s <splits>` and `-l <layout>` CLI args. On non-DEBUG builds it registers `.lss`/`.lsl` file associations on startup.

`LiveSplit.Register` and `UpdateManager` are auxiliary projects for installer/file-association and the auto-updater respectively.

### livesplit-core native interop

`src\LiveSplit.Core\LiveSplitCore.g.cs` is a generated P/Invoke binding to the Rust **livesplit-core** library. `LiveSplitCoreFactory.cs` loads `x86\livesplit_core.dll` or `x64\livesplit_core.dll` at runtime depending on process bitness. The pre-built DLLs live in `src\LiveSplit.Core\x86\` and `src\LiveSplit.Core\x64\` and are copied to output. Don't edit `LiveSplitCore.g.cs` by hand — it's regenerated from the `livesplit-core` submodule (`lib\livesplit-core`) by `src\LiveSplit.Core\build_livesplit_core.sh`.

### Component plugin model

Every layout part (Splits, Timer, Title, Graph, Sound, …) is its own csproj that compiles to a DLL. `ComponentManager` (`src\LiveSplit.Core\UI\Components\ComponentManager.cs`) discovers them at runtime: it scans `bin\<config>\Components\*.dll`, reads `ComponentFactoryAttribute` via reflection, and instantiates the factory. The factory's `Create(LiveSplitState)` returns an `IComponent` that the layout renderer composes.

Practical consequences:
- A new component is a new csproj in `components\` whose output drops a DLL into the `Components\` subfolder; nothing else needs to register it.
- The `LiveSplit` app csproj has a `PostBuildEvent` that *deletes* certain DLLs from `Components\` (shared libraries like `SpeedrunComSharp.dll`, `WinFormsColor.dll`, font/color pickers) to prevent them being treated as components. If you add a new shared DLL that components reference, you likely need to add it to that cleanup list.
- `IComponent` vs `LogicComponent` vs `ControlComponent`: visual components implement `IComponent`; non-visual logic-only components extend `LogicComponent`; components with WinForms controls (e.g. video) use `ControlComponent`.

### Localization

JSON locale files live in `src\LiveSplit.Core\Localization\Locales\` and are copied into the app's `Localization\` folder at build (see `LiveSplit.csproj` `<Content>` include). New user-facing strings are looked up via `UiLocalizer.TranslateKey(LocalizationKeys.Foo, "English fallback")`; add the key to `LocalizationKeys.cs` and provide translations in the locale JSONs.

### Server protocol

The README has the full command list. When adding a command: implement it in `ProcessMessage` in `src\LiveSplit.Core\Server\CommandServer.cs`, then **also document it in README.md** under the appropriate response-type section (no-response / returns time / returns int / returns bool / returns string).

## Release flow

Releases are not automated. Tag the LiveSplit repo, download the `LiveSplit_Build` and `UpdateManagerExe` artifacts from the corresponding GitHub Actions run, then update `LiveSplit.github.io/update/` (binaries + `update.xml`) by hand. Full checklist is in the **Releasing** section of README.md. Component versions are bumped independently — each component repo has its own tag and `update.LiveSplit.<Name>.xml`.
