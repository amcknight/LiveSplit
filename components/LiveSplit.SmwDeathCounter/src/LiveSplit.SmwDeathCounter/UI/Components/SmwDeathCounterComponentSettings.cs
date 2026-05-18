using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model.Input;
using LiveSplit.Options;

namespace LiveSplit.UI.Components;

public class SmwDeathCounterComponentSettings : UserControl
{
    public CompositeHook Hook { get; }

    public string Label { get; set; }
    public KeyOrButton ResetKey { get; set; }

    // Component sets this so the settings panel can show live attach state.
    public Func<string> StatusProvider { get; set; }

    private TextBox txtLabel;
    private TextBox txtReset;
    private Label lblStatus;
    private Timer statusTimer;

    public SmwDeathCounterComponentSettings(bool allowGamepads)
    {
        Hook = new CompositeHook(allowGamepads);

        Label = "Deaths";
        ResetKey = new KeyOrButton(Keys.F2);

        BuildUi();
        RegisterHotKeys();
    }

    private void BuildUi()
    {
        Size = new Size(420, 200);

        int y = 10;
        void row(string text, Control input)
        {
            Controls.Add(new Label { Text = text, Location = new Point(10, y + 3), AutoSize = true });
            input.Location = new Point(140, y);
            input.Width = 260;
            Controls.Add(input);
            y += 30;
        }

        txtLabel = new TextBox { Text = Label };
        txtLabel.TextChanged += (_, __) => Label = txtLabel.Text;
        row("Label:", txtLabel);

        txtReset = new TextBox { ReadOnly = true, Text = FormatKey(ResetKey) };
        txtReset.Enter += (_, __) => CaptureKey(txtReset, k => ResetKey = k);
        row("Reset hotkey (global):", txtReset);

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
            Location = new Point(140, y + 3),
            AutoSize = true,
            ForeColor = Color.Gray,
        };
        Controls.Add(lblStatus);
        y += 25;

        Controls.Add(new Label
        {
            Text = "Supports snes9x, snes9x-x64, snes9x-rr, bsnes, higan, BizHawk,\n" +
                   "and RetroArch (snes9x_libretro / bsnes_libretro / snes9x2010_libretro).",
            Location = new Point(10, y),
            AutoSize = true,
            ForeColor = Color.Gray,
        });

        // Refresh attach status text twice a second.
        statusTimer = new Timer { Interval = 500 };
        statusTimer.Tick += (_, __) =>
        {
            if (StatusProvider != null && lblStatus != null) { lblStatus.Text = StatusProvider(); }
        };
        statusTimer.Enabled = true;
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

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public void SetSettings(XmlNode node)
    {
        var e = (XmlElement)node;
        Label = SettingsHelper.ParseString(e["Label"], "Deaths");

        XmlElement rst = e["ResetKey"];
        ResetKey = rst != null && !string.IsNullOrEmpty(rst.InnerText) ? new KeyOrButton(rst.InnerText) : null;

        if (txtLabel != null) { txtLabel.Text = Label; }
        if (txtReset != null) { txtReset.Text = FormatKey(ResetKey); }

        RegisterHotKeys();
    }

    public int GetSettingsHashCode() => CreateSettingsNode(null, null);

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "2.1") ^
               SettingsHelper.CreateSetting(document, parent, "Label", Label) ^
               SettingsHelper.CreateSetting(document, parent, "ResetKey", ResetKey);
    }
}
