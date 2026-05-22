using System.Xml;

using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class SmwCountersSettingsTests
{
    [Fact]
    public void DefaultsHaveNoCountersEnabled()
    {
        var s = new SmwCountersComponentSettings(allowGamepads: false);

        Assert.False(s.IsEnabled("deaths"));
        Assert.False(s.IsEnabled("moons"));
    }

    [Fact]
    public void EnabledSetRoundTripsThroughXml()
    {
        var a = new SmwCountersComponentSettings(allowGamepads: false);
        a.SetEnabled("deaths", true);
        a.SetEnabled("moons", true);

        XmlNode node = a.GetSettings(new XmlDocument { PreserveWhitespace = false });

        var b = new SmwCountersComponentSettings(allowGamepads: false);
        b.SetSettings(node);

        Assert.True(b.IsEnabled("deaths"));
        Assert.True(b.IsEnabled("moons"));
    }

    [Fact]
    public void LabelOverridesRoundTrip()
    {
        var a = new SmwCountersComponentSettings(allowGamepads: false);
        a.SetEnabled("deaths", true);
        a.SetLabelOverride("deaths", "D");

        XmlNode node = a.GetSettings(new XmlDocument());

        var b = new SmwCountersComponentSettings(allowGamepads: false);
        b.SetSettings(node);

        Assert.Equal("D", b.GetLabelOverride("deaths"));
    }

    [Fact]
    public void MissingLabelOverrideReturnsNull()
    {
        var s = new SmwCountersComponentSettings(allowGamepads: false);

        Assert.Null(s.GetLabelOverride("moons"));
    }

    [Fact]
    public void ToleratesMissingSectionsOnLoad()
    {
        // Hand-rolled minimal XML with neither EnabledCounters nor CounterLabels.
        var doc = new XmlDocument();
        XmlElement root = doc.CreateElement("Settings");
        doc.AppendChild(root);
        XmlElement version = doc.CreateElement("Version");
        version.InnerText = "1";
        root.AppendChild(version);

        var s = new SmwCountersComponentSettings(allowGamepads: false);
        s.SetSettings(root); // must not throw

        Assert.False(s.IsEnabled("deaths"));
        Assert.Null(s.GetLabelOverride("deaths"));
    }
}
