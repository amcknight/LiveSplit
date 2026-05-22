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
        Size = new Size(420, 240);
    }

    private readonly List<CounterRow> rows = new();
    private TextBox txtReset;
    private Label lblStatus;
    private Timer statusTimer;

    private sealed class CounterRow
    {
        public string Id;
        public CheckBox Enable;
        public TextBox Label;
        public Button ResetValue;
        public Action OnResetValue; // set by the component
        public Control CounterSpecific; // optional extra control (e.g. moon dedupe radio)
    }

    // Component calls this once at construction with the list of known counters.
    public void BuildUi(IReadOnlyList<(string Id, string DefaultLabel, Control Extras, Action ResetValue)> counters)
    {
        Controls.Clear();
        rows.Clear();

        int y = 10;

        foreach ((string id, string defaultLabel, Control extras, Action resetValue) in counters)
        {
            var row = new CounterRow { Id = id, OnResetValue = resetValue };

            row.Enable = new CheckBox
            {
                Text = defaultLabel,
                Location = new Point(10, y),
                AutoSize = true,
                Checked = IsEnabled(id),
            };
            row.Enable.CheckedChanged += (_, __) => SetEnabled(id, row.Enable.Checked);
            Controls.Add(row.Enable);

            row.Label = new TextBox
            {
                Text = GetLabelOverride(id) ?? "",
                Location = new Point(120, y - 2),
                Width = 120,
            };
            row.Label.TextChanged += (_, __) => SetLabelOverride(id, row.Label.Text);
            Controls.Add(row.Label);

            row.ResetValue = new Button
            {
                Text = "Reset value",
                Location = new Point(250, y - 4),
                AutoSize = true,
            };
            row.ResetValue.Click += (_, __) => row.OnResetValue?.Invoke();
            Controls.Add(row.ResetValue);

            y += 28;

            if (extras != null)
            {
                extras.Location = new Point(30, y);
                Controls.Add(extras);
                row.CounterSpecific = extras;
                y += extras.Height + 4;
            }

            rows.Add(row);
        }

        Controls.Add(new Label
        {
            Text = "Reset hotkey (global):",
            Location = new Point(10, y + 3),
            AutoSize = true,
        });
        txtReset = new TextBox
        {
            ReadOnly = true,
            Text = FormatKey(ResetKey),
            Location = new Point(120, y),
            Width = 220,
        };
        txtReset.Enter += (_, __) => CaptureKey(txtReset, k => ResetKey = k);
        Controls.Add(txtReset);
        y += 30;

        Controls.Add(new Label
        {
            Text = "Emulator:",
            Location = new Point(10, y + 3),
            AutoSize = true,
            ForeColor = Color.Gray,
        });
        lblStatus = new Label
        {
            Text = "(detecting…)",
            Location = new Point(120, y + 3),
            AutoSize = true,
            ForeColor = Color.Gray,
        };
        Controls.Add(lblStatus);
        y += 25;

        Controls.Add(new Label
        {
            Text = "Supports snes9x family, bsnes, higan, BizHawk,\n" +
                   "and RetroArch SNES cores (snes9x / bsnes / snes9x2010).",
            Location = new Point(10, y),
            AutoSize = true,
            ForeColor = Color.Gray,
        });

        Size = new Size(360, y + 60);

        statusTimer?.Dispose();
        statusTimer = new Timer { Interval = 500 };
        statusTimer.Tick += (_, __) =>
        {
            if (StatusProvider != null && lblStatus != null) { lblStatus.Text = StatusProvider(); }
        };
        statusTimer.Enabled = true;

        RegisterHotKeys();
    }

    // Re-syncs visible row widgets from the data model after SetSettings is called.
    public void RefreshFromModel()
    {
        foreach (CounterRow row in rows)
        {
            row.Enable.Checked = IsEnabled(row.Id);
            row.Label.Text = GetLabelOverride(row.Id) ?? "";
        }
        if (txtReset != null) { txtReset.Text = FormatKey(ResetKey); }
        RegisterHotKeys();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { statusTimer?.Dispose(); }
        base.Dispose(disposing);
    }

    private void CaptureKey(TextBox box, Action<KeyOrButton> setter)
    {
        string previous = box.Text;
        box.Text = "Set Hotkey...";

        KeyEventHandler keyDown = null;
        EventHandler leave = null;
        EventHandlerT<GamepadButton> gamepad = null;

        void unhook()
        {
            box.KeyDown -= keyDown;
            box.Leave -= leave;
            Hook.AnyGamepadButtonPressed -= gamepad;
        }

        keyDown = (s, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu) { return; }

            var k = e.KeyCode == Keys.Escape ? null : new KeyOrButton(e.KeyCode | e.Modifiers);
            setter(k);
            unhook();
            box.Text = FormatKey(k);
            ActiveControl = null;
            RegisterHotKeys();
        };

        leave = (_, __) =>
        {
            unhook();
            if (box.Text == "Set Hotkey...") { box.Text = previous; }
        };

        gamepad = (_, btn) =>
        {
            var k = new KeyOrButton(btn);
            setter(k);
            unhook();
            void apply()
            {
                box.Text = FormatKey(k);
                ActiveControl = null;
                RegisterHotKeys();
            }
            if (InvokeRequired) { Invoke(apply); } else { apply(); }
        };

        box.KeyDown += keyDown;
        box.Leave += leave;
        Hook.AnyGamepadButtonPressed += gamepad;
    }

    private void RegisterHotKeys()
    {
        try
        {
            Hook.UnregisterAllHotkeys();
            if (ResetKey != null) { Hook.RegisterHotKey(ResetKey); }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static string FormatKey(KeyOrButton key)
    {
        if (key == null) { return "None"; }
        string s = key.ToString();
        if (key.IsButton)
        {
            int i = s.LastIndexOf(' ');
            if (i != -1) { s = s[..i]; }
        }
        return s;
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

        RefreshFromModel();
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
