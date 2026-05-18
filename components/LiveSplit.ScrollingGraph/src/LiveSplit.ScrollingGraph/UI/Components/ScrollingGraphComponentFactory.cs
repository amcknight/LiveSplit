using System;

using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(ScrollingGraphComponentFactory))]

namespace LiveSplit.UI.Components;

public class ScrollingGraphComponentFactory : IComponentFactory
{
    public string ComponentName => "Scrolling Graph";

    public string Description =>
        "Live scrolling graph: continuous line for a chosen counter, with green/red bars per completed split for pace delta.";

    public ComponentCategory Category => ComponentCategory.Other;

    public IComponent Create(LiveSplitState state) => new ScrollingGraphComponent(state);

    public string UpdateName => ComponentName;

    public string XMLURL => string.Empty;

    public string UpdateURL => string.Empty;

    public Version Version => Version.Parse("0.1.0");
}
