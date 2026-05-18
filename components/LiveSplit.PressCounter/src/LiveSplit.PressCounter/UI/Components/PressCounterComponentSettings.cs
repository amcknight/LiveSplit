using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model.Input;
using LiveSplit.Options;

namespace LiveSplit.UI.Components;

public class PressCounterComponentSettings : UserControl
{
    public CompositeHook Hook { get; }

    public string Label { get; set; }
    public bool GlobalHotkeysEnabled { get; set; }
    public KeyOrButton IncrementKey { get; set; }
    public KeyOrButton ResetKey { get; set; }

    private TextBox txtLabel;
    private TextBox txtIncrement;
    private TextBox txtReset;
    private CheckBox chkGlobal;

    public PressCounterComponentSettings(bool allowGamepads)
    {
        Hook = new CompositeHook(allowGamepads);

        Label = "Presses:";
        IncrementKey = new KeyOrButton(Keys.F1);
        ResetKey = new KeyOrButton(Keys.F2);
        GlobalHotkeysEnabled = false;

        BuildUi();
        RegisterHotKeys();
    }

    private void BuildUi()
    {
        Size = new Size(360, 160);

        var lblLabel = new Label { Text = "Label:", Location = new Point(10, 12), AutoSize = true };
        txtLabel = new TextBox { Location = new Point(110, 9), Width = 220, Text = Label };
        txtLabel.TextChanged += (s, e) => Label = txtLabel.Text;

        var lblInc = new Label { Text = "Increment hotkey:", Location = new Point(10, 42), AutoSize = true };
        txtIncrement = new TextBox { Location = new Point(110, 39), Width = 220, ReadOnly = true, Text = FormatKey(IncrementKey) };
        txtIncrement.Enter += (s, e) => CaptureKey(txtIncrement, k => IncrementKey = k);

        var lblReset = new Label { Text = "Reset hotkey:", Location = new Point(10, 72), AutoSize = true };
        txtReset = new TextBox { Location = new Point(110, 69), Width = 220, ReadOnly = true, Text = FormatKey(ResetKey) };
        txtReset.Enter += (s, e) => CaptureKey(txtReset, k => ResetKey = k);

        chkGlobal = new CheckBox
        {
            Text = "Global hotkeys (work when LiveSplit is unfocused)",
            Location = new Point(10, 102),
            AutoSize = true,
            Checked = GlobalHotkeysEnabled,
        };
        chkGlobal.CheckedChanged += (s, e) => GlobalHotkeysEnabled = chkGlobal.Checked;

        Controls.AddRange([lblLabel, txtLabel, lblInc, txtIncrement, lblReset, txtReset, chkGlobal]);
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
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu)
            {
                return;
            }

            var k = e.KeyCode == Keys.Escape ? null : new KeyOrButton(e.KeyCode | e.Modifiers);
            setter(k);
            unhook();
            box.Text = FormatKey(k);
            ActiveControl = chkGlobal;
            RegisterHotKeys();
        };

        leave = (s, e) =>
        {
            unhook();
            if (box.Text == "Set Hotkey...")
            {
                box.Text = previous;
            }
        };

        gamepad = (s, btn) =>
        {
            var k = new KeyOrButton(btn);
            setter(k);
            unhook();
            void apply()
            {
                box.Text = FormatKey(k);
                ActiveControl = chkGlobal;
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
            if (IncrementKey != null) { Hook.RegisterHotKey(IncrementKey); }
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
        Label = SettingsHelper.ParseString(e["Label"], "Presses:");
        GlobalHotkeysEnabled = SettingsHelper.ParseBool(e["GlobalHotkeysEnabled"]);

        XmlElement inc = e["IncrementKey"];
        IncrementKey = inc != null && !string.IsNullOrEmpty(inc.InnerText) ? new KeyOrButton(inc.InnerText) : null;

        XmlElement rst = e["ResetKey"];
        ResetKey = rst != null && !string.IsNullOrEmpty(rst.InnerText) ? new KeyOrButton(rst.InnerText) : null;

        if (txtLabel != null) { txtLabel.Text = Label; }
        if (txtIncrement != null) { txtIncrement.Text = FormatKey(IncrementKey); }
        if (txtReset != null) { txtReset.Text = FormatKey(ResetKey); }
        if (chkGlobal != null) { chkGlobal.Checked = GlobalHotkeysEnabled; }

        RegisterHotKeys();
    }

    public int GetSettingsHashCode() => CreateSettingsNode(null, null);

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0") ^
               SettingsHelper.CreateSetting(document, parent, "Label", Label) ^
               SettingsHelper.CreateSetting(document, parent, "GlobalHotkeysEnabled", GlobalHotkeysEnabled) ^
               SettingsHelper.CreateSetting(document, parent, "IncrementKey", IncrementKey) ^
               SettingsHelper.CreateSetting(document, parent, "ResetKey", ResetKey);
    }
}
