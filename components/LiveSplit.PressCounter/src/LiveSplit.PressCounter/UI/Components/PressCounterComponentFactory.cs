using System;

using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(PressCounterComponentFactory))]

namespace LiveSplit.UI.Components;

public class PressCounterComponentFactory : IComponentFactory
{
    public string ComponentName => "Press Counter";

    public string Description => "Counts how many times a configured key or controller button is pressed.";

    public ComponentCategory Category => ComponentCategory.Other;

    public IComponent Create(LiveSplitState state) => new PressCounterComponent(state);

    public string UpdateName => ComponentName;

    public string XMLURL => string.Empty;

    public string UpdateURL => string.Empty;

    public Version Version => Version.Parse("0.1.0");
}
