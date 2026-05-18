using System;

using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(SmwDeathCounterComponentFactory))]

namespace LiveSplit.UI.Components;

public class SmwDeathCounterComponentFactory : IComponentFactory
{
    public string ComponentName => "SMW Death Counter";

    public string Description => "Watches SNES WRAM in an emulator process and counts Mario deaths.";

    public ComponentCategory Category => ComponentCategory.Other;

    public IComponent Create(LiveSplitState state) => new SmwDeathCounterComponent(state);

    public string UpdateName => ComponentName;

    public string XMLURL => string.Empty;

    public string UpdateURL => string.Empty;

    public Version Version => Version.Parse("0.1.0");
}
