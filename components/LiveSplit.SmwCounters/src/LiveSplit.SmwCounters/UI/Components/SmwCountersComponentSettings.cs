using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;

namespace LiveSplit.UI.Components;

public class SmwCountersComponentSettings : UserControl
{
    public CompositeHook Hook { get; }

    private readonly HashSet<string> enabled = new();
    private readonly Dictionary<string, string> labels = new();

    public KeyOrButton ResetKey { get; set; }

    // Component sets this so the settings panel can show live attach state.
    public Func<string> StatusProvider { get; set; }

    public SmwCountersComponentSettings(bool allowGamepads)
    {
        Hook = new CompositeHook(allowGamepads);
        ResetKey = new KeyOrButton(Keys.F2);
        // UI is built in a later task — keep the control empty for now so tests can run.
        Size = new Size(420, 240);
    }

    public bool IsEnabled(string counterId) => enabled.Contains(counterId);

    public void SetEnabled(string counterId, bool value)
    {
        if (value) { enabled.Add(counterId); }
        else { enabled.Remove(counterId); }
    }

    public IEnumerable<string> EnabledIds => enabled;

    public string GetLabelOverride(string counterId) =>
        labels.TryGetValue(counterId, out string s) ? s : null;

    public void SetLabelOverride(string counterId, string label)
    {
        if (string.IsNullOrEmpty(label)) { labels.Remove(counterId); }
        else { labels[counterId] = label; }
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public void SetSettings(XmlNode node)
    {
        var e = (XmlElement)node;

        XmlElement rst = e["ResetKey"];
        ResetKey = rst != null && !string.IsNullOrEmpty(rst.InnerText) ? new KeyOrButton(rst.InnerText) : null;

        enabled.Clear();
        XmlElement enabledNode = e["EnabledCounters"];
        if (enabledNode != null)
        {
            foreach (XmlElement c in enabledNode.GetElementsByTagName("Counter"))
            {
                if (!string.IsNullOrEmpty(c.InnerText)) { enabled.Add(c.InnerText); }
            }
        }

        labels.Clear();
        XmlElement labelsNode = e["CounterLabels"];
        if (labelsNode != null)
        {
            foreach (XmlElement l in labelsNode.GetElementsByTagName("Label"))
            {
                string id = l.GetAttribute("id");
                if (!string.IsNullOrEmpty(id))
                {
                    labels[id] = l.InnerText ?? "";
                }
            }
        }
    }

    public int GetSettingsHashCode() => CreateSettingsNode(null, null);

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        int hash = SettingsHelper.CreateSetting(document, parent, "Version", "1");
        hash ^= SettingsHelper.CreateSetting(document, parent, "ResetKey", ResetKey);

        if (document != null && parent != null)
        {
            XmlElement enabledNode = document.CreateElement("EnabledCounters");
            foreach (string id in enabled)
            {
                XmlElement c = document.CreateElement("Counter");
                c.InnerText = id;
                enabledNode.AppendChild(c);
            }
            parent.AppendChild(enabledNode);

            XmlElement labelsNode = document.CreateElement("CounterLabels");
            foreach (KeyValuePair<string, string> kv in labels)
            {
                XmlElement l = document.CreateElement("Label");
                l.SetAttribute("id", kv.Key);
                l.InnerText = kv.Value;
                labelsNode.AppendChild(l);
            }
            parent.AppendChild(labelsNode);
        }

        foreach (string id in enabled) { hash ^= id.GetHashCode(); }
        foreach (KeyValuePair<string, string> kv in labels)
        {
            hash ^= kv.Key.GetHashCode() ^ (kv.Value ?? "").GetHashCode();
        }
        return hash;
    }
}
