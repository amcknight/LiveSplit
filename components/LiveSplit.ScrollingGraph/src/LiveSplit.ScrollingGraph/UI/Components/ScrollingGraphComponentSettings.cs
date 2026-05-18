using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

public class ScrollingGraphComponentSettings : UserControl
{
    public string SourceComponentName { get; set; }
    public int WindowSeconds { get; set; }
    public bool RateMode { get; set; }
    public string Comparison { get; set; }

    public float GraphWidth { get; set; }
    public float GraphHeight { get; set; }

    public Color BackgroundColor { get; set; }
    public Color LineColor { get; set; }
    public Color AheadColor { get; set; }
    public Color BehindColor { get; set; }

    // Set by the component so the dropdown can populate.
    public Func<List<string>> SourcesProvider { get; set; }

    private ComboBox cmbSource;
    private NumericUpDown numWindow;
    private CheckBox chkRate;
    private NumericUpDown numWidth;
    private NumericUpDown numHeight;

    public ScrollingGraphComponentSettings()
    {
        WindowSeconds = 30;
        RateMode = true;
        Comparison = "Current Comparison";
        GraphWidth = 200;
        GraphHeight = 80;
        BackgroundColor = Color.FromArgb(180, 0, 0, 0);
        LineColor = Color.FromArgb(255, 80, 180, 255);
        AheadColor = Color.FromArgb(255, 0, 200, 100);
        BehindColor = Color.FromArgb(255, 220, 70, 70);

        BuildUi();
    }

    private void BuildUi()
    {
        Size = new Size(420, 240);

        int y = 10;
        void row(string text, Control c)
        {
            Controls.Add(new Label { Text = text, Location = new Point(10, y + 3), AutoSize = true });
            c.Location = new Point(160, y);
            c.Width = 240;
            Controls.Add(c);
            y += 28;
        }

        cmbSource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        cmbSource.DropDown += (_, __) => RefreshSources();
        cmbSource.TextChanged += (_, __) => SourceComponentName = cmbSource.Text;
        row("Line source (component):", cmbSource);

        chkRate = new CheckBox { Text = "Plot rate per second (vs raw value)", AutoSize = true, Checked = RateMode };
        chkRate.CheckedChanged += (_, __) => RateMode = chkRate.Checked;
        row("Display:", chkRate);

        numWindow = new NumericUpDown { Minimum = 5, Maximum = 600, Value = WindowSeconds };
        numWindow.ValueChanged += (_, __) => WindowSeconds = (int)numWindow.Value;
        row("Window (seconds):", numWindow);

        numWidth = new NumericUpDown { Minimum = 60, Maximum = 2000, Value = (decimal)GraphWidth };
        numWidth.ValueChanged += (_, __) => GraphWidth = (float)numWidth.Value;
        row("Width (horizontal layout):", numWidth);

        numHeight = new NumericUpDown { Minimum = 30, Maximum = 1000, Value = (decimal)GraphHeight };
        numHeight.ValueChanged += (_, __) => GraphHeight = (float)numHeight.Value;
        row("Height (vertical layout):", numHeight);

        AddColorButton("Line color:", () => LineColor, c => LineColor = c, y); y += 28;
        AddColorButton("Ahead (negative Δ):", () => AheadColor, c => AheadColor = c, y); y += 28;
        AddColorButton("Behind (positive Δ):", () => BehindColor, c => BehindColor = c, y); y += 28;
    }

    private void AddColorButton(string label, Func<Color> get, Action<Color> set, int y)
    {
        Controls.Add(new Label { Text = label, Location = new Point(10, y + 3), AutoSize = true });
        var btn = new Button
        {
            Location = new Point(160, y),
            Size = new Size(60, 22),
            BackColor = get(),
            FlatStyle = FlatStyle.Flat,
        };
        btn.Click += (_, __) =>
        {
            using var dlg = new ColorDialog { Color = get(), FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                set(dlg.Color);
                btn.BackColor = dlg.Color;
            }
        };
        Controls.Add(btn);
    }

    private void RefreshSources()
    {
        if (SourcesProvider == null) { return; }
        string current = cmbSource.Text;
        cmbSource.Items.Clear();
        foreach (string s in SourcesProvider()) { cmbSource.Items.Add(s); }
        cmbSource.Text = current;
    }

    // --------------------------------------------------------------------
    // Persistence

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateNode(document, parent);
        return parent;
    }

    public void SetSettings(XmlNode node)
    {
        var e = (XmlElement)node;
        SourceComponentName = SettingsHelper.ParseString(e["SourceComponentName"]);
        WindowSeconds = ParseInt(e["WindowSeconds"], 30);
        RateMode = SettingsHelper.ParseBool(e["RateMode"], true);
        Comparison = SettingsHelper.ParseString(e["Comparison"], "Current Comparison");
        GraphWidth = ParseFloat(e["GraphWidth"], 200);
        GraphHeight = ParseFloat(e["GraphHeight"], 80);
        BackgroundColor = SettingsHelper.ParseColor(e["BackgroundColor"]);
        LineColor = SettingsHelper.ParseColor(e["LineColor"]);
        AheadColor = SettingsHelper.ParseColor(e["AheadColor"]);
        BehindColor = SettingsHelper.ParseColor(e["BehindColor"]);

        if (cmbSource != null) { cmbSource.Text = SourceComponentName ?? ""; }
        if (chkRate != null) { chkRate.Checked = RateMode; }
        if (numWindow != null) { numWindow.Value = Clamp(WindowSeconds, 5, 600); }
        if (numWidth != null) { numWidth.Value = Clamp((int)GraphWidth, 60, 2000); }
        if (numHeight != null) { numHeight.Value = Clamp((int)GraphHeight, 30, 1000); }
    }

    public int GetSettingsHashCode() => CreateNode(null, null);

    private int CreateNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0") ^
               SettingsHelper.CreateSetting(document, parent, "SourceComponentName", SourceComponentName ?? "") ^
               SettingsHelper.CreateSetting(document, parent, "WindowSeconds", WindowSeconds) ^
               SettingsHelper.CreateSetting(document, parent, "RateMode", RateMode) ^
               SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison ?? "Current Comparison") ^
               SettingsHelper.CreateSetting(document, parent, "GraphWidth", GraphWidth) ^
               SettingsHelper.CreateSetting(document, parent, "GraphHeight", GraphHeight) ^
               SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
               SettingsHelper.CreateSetting(document, parent, "LineColor", LineColor) ^
               SettingsHelper.CreateSetting(document, parent, "AheadColor", AheadColor) ^
               SettingsHelper.CreateSetting(document, parent, "BehindColor", BehindColor);
    }

    private static int ParseInt(XmlElement e, int fallback)
    {
        if (e != null && int.TryParse(e.InnerText, out int v)) { return v; }
        return fallback;
    }

    private static float ParseFloat(XmlElement e, float fallback)
    {
        if (e != null && float.TryParse(e.InnerText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
        {
            return v;
        }
        return fallback;
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
