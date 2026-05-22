using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.SmwCounters.Counters;
using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.UI.Components;

public class SmwCountersComponent : IComponent
{
    private const float CellGap = 14f;

    private readonly LiveSplitState state;
    private readonly Timer pollTimer;
    private readonly SnesEmu emu = new();

    // All known counters, registered at construction. The Settings hold the
    // user's enabled subset and label overrides.
    private readonly IReadOnlyList<ISmwCounter> counters;

    private readonly Dictionary<string, SimpleLabel> labelCells = new();
    private readonly Dictionary<string, SimpleLabel> valueCells = new();
    private readonly GraphicsCache cache = new();

    public SmwCountersComponentSettings Settings { get; }

    public string ComponentName => "SMW Counters";

    public float VerticalHeight { get; private set; } = 10f;
    public float MinimumHeight { get; private set; }
    public float HorizontalWidth { get; private set; }
    public float MinimumWidth => 80f;

    public float PaddingTop { get; private set; }
    public float PaddingBottom { get; private set; }
    public float PaddingLeft => 7f;
    public float PaddingRight => 7f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public SmwCountersComponent(LiveSplitState state)
    {
        this.state = state;

        // Build the registry of known counters.
        var moon = new MoonCounter();
        counters = new ISmwCounter[]
        {
            new DeathCounter(),
            moon,
        };

        foreach (ISmwCounter c in counters)
        {
            labelCells[c.Id] = new SimpleLabel();
            valueCells[c.Id] = new SimpleLabel();
        }

        bool allowGamepads = state.Settings.HotkeyProfiles.First().Value.AllowGamepadsAsHotkeys;
        Settings = new SmwCountersComponentSettings(allowGamepads);
        Settings.Hook.KeyOrButtonPressed += Hook_KeyOrButtonPressed;
        Settings.StatusProvider = () => emu.Describe();

        // Wire up per-counter rows. Counter-specific extras live here so the
        // settings UserControl doesn't know about individual counter types.
        var rows = new List<(string Id, string DefaultLabel, Control Extras, Action ResetValue)>();
        foreach (ISmwCounter c in counters)
        {
            Control extras = BuildExtras(c);
            rows.Add((c.Id, c.DefaultLabel, extras, () => c.Reset()));
        }
        Settings.BuildUi(rows);

        pollTimer = new Timer { Interval = 15 };
        pollTimer.Tick += (_, __) => Poll();
        pollTimer.Enabled = true;
    }

    private Control BuildExtras(ISmwCounter counter)
    {
        if (counter is MoonCounter moon)
        {
            var panel = new Panel { Width = 400, Height = 24, Padding = new Padding(0) };
            var rdoLevel = new RadioButton
            {
                Text = "Per level",
                AutoSize = true,
                Checked = !moon.DedupePerRoom,
                Location = new Point(0, 4),
            };
            var rdoRoom = new RadioButton
            {
                Text = "Per room (level + sublevel)",
                AutoSize = true,
                Checked = moon.DedupePerRoom,
                Location = new Point(90, 4),
            };
            rdoLevel.CheckedChanged += (_, __) => { if (rdoLevel.Checked) { moon.DedupePerRoom = false; } };
            rdoRoom.CheckedChanged  += (_, __) => { if (rdoRoom.Checked)  { moon.DedupePerRoom = true; } };
            panel.Controls.Add(rdoLevel);
            panel.Controls.Add(rdoRoom);
            return panel;
        }
        return null;
    }

    private void Hook_KeyOrButtonPressed(object sender, KeyOrButton e)
    {
        if (e == Settings.ResetKey)
        {
            foreach (ISmwCounter c in counters)
            {
                if (Settings.IsEnabled(c.Id)) { c.Reset(); }
            }
        }
    }

    private void Poll()
    {
        if (!emu.TryAttach()) { return; }
        foreach (ISmwCounter c in counters)
        {
            if (Settings.IsEnabled(c.Id)) { c.Poll(emu); }
        }
    }

    // Returns the label-override text the user typed, or null when the row
    // should draw the default icon instead.
    private string OverrideTextOrNull(ISmwCounter c)
    {
        string overrideText = Settings.GetLabelOverride(c.Id);
        return string.IsNullOrEmpty(overrideText) ? null : overrideText;
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        try { Settings.Hook?.Poll(); } catch { }

        cache.Restart();
        foreach (ISmwCounter c in counters)
        {
            if (!Settings.IsEnabled(c.Id)) { continue; }
            string overrideText = OverrideTextOrNull(c);
            string value = c.Value.ToString();
            labelCells[c.Id].Text = overrideText ?? "";
            valueCells[c.Id].Text = value;
            // Cache key is either the override text or "<icon>" so flipping
            // between override-text and default-icon invalidates correctly.
            cache[c.Id + ".label"] = overrideText ?? "<icon>";
            cache[c.Id + ".value"] = value;
        }

        if (invalidator != null && cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    private void DrawGeneral(Graphics g, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Font font = state.LayoutSettings.TextFont;
        Color textColor = state.LayoutSettings.TextColor;

        float textHeight = g.MeasureString("A", font).Height;
        VerticalHeight = 1.2f * textHeight;
        PaddingTop = Math.Max(0, (VerticalHeight - (0.75f * textHeight)) / 2f);
        PaddingBottom = PaddingTop;

        // Icons are scaled to fit the row height while preserving their native
        // aspect ratio, so a 16x24 sprite renders taller-than-wide.
        int iconHeight = (int)Math.Round(0.85f * textHeight);

        // Measure each enabled counter's cell width: label-slot + " " + value.
        // Label slot is icon-aspect-scaled when defaulting, override-text width otherwise.
        var enabled = counters.Where(c => Settings.IsEnabled(c.Id)).ToList();
        float totalWidth = 0f;
        var cellWidths = new Dictionary<string, (float labelW, float valueW)>();
        foreach (ISmwCounter c in enabled)
        {
            string overrideText = OverrideTextOrNull(c);
            float labelW;
            if (overrideText != null) { labelW = g.MeasureString(overrideText, font).Width; }
            else if (c.DefaultIcon != null) { labelW = IconWidthFor(c.DefaultIcon, iconHeight); }
            else { labelW = g.MeasureString(c.DefaultLabel, font).Width; }
            float valueW = g.MeasureString(c.Value.ToString("0"), font).Width;
            cellWidths[c.Id] = (labelW, valueW);
            if (totalWidth > 0) { totalWidth += CellGap; }
            totalWidth += labelW + 4 + valueW;
        }

        HorizontalWidth = totalWidth + 15;

        float x = 5f;
        foreach (ISmwCounter c in enabled)
        {
            (float labelW, float valueW) = cellWidths[c.Id];
            string overrideText = OverrideTextOrNull(c);

            if (overrideText == null && c.DefaultIcon != null)
            {
                DrawIcon(g, c.DefaultIcon, x, height, labelW, iconHeight);
            }
            else
            {
                // Either user-typed override or DefaultLabel fallback (when a
                // counter ships without an icon).
                labelCells[c.Id].Text = overrideText ?? c.DefaultLabel;
                ConfigureLabel(labelCells[c.Id], font, textColor, StringAlignment.Near, x, labelW, height);
                labelCells[c.Id].Draw(g);
            }
            x += labelW + 4;

            ConfigureLabel(valueCells[c.Id], font, textColor, StringAlignment.Near, x, valueW, height);
            valueCells[c.Id].Draw(g);
            x += valueW + CellGap;
        }
    }

    private static float IconWidthFor(Image icon, int iconHeight)
        => (float)Math.Round((double)iconHeight * icon.Width / icon.Height);

    private static void DrawIcon(Graphics g, Image icon, float x, float height, float drawWidth, int iconHeight)
    {
        InterpolationMode prevInterp = g.InterpolationMode;
        PixelOffsetMode prevOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        float y = (height - iconHeight) / 2f;
        g.DrawImage(icon, x, y, drawWidth, iconHeight);
        g.InterpolationMode = prevInterp;
        g.PixelOffsetMode = prevOffset;
    }

    private void ConfigureLabel(SimpleLabel label, Font font, Color color, StringAlignment hAlign, float x, float width, float height)
    {
        label.HorizontalAlignment = hAlign;
        label.VerticalAlignment = StringAlignment.Center;
        label.X = x;
        label.Y = 0;
        label.Width = width;
        label.Height = height;
        label.Font = font;
        label.Brush = new SolidBrush(color);
        label.HasShadow = state.LayoutSettings.DropShadows;
        label.ShadowColor = state.LayoutSettings.ShadowsColor;
        label.OutlineColor = state.LayoutSettings.TextOutlineColor;
    }

    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        => DrawGeneral(g, state, HorizontalWidth, height, LayoutMode.Horizontal);

    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        => DrawGeneral(g, state, width, VerticalHeight, LayoutMode.Vertical);

    public Control GetSettingsControl(LayoutMode mode) => Settings;

    public XmlNode GetSettings(XmlDocument document)
    {
        var node = (XmlElement)Settings.GetSettings(document);

        XmlElement stateNode = document.CreateElement("CounterState");
        foreach (ISmwCounter c in counters)
        {
            XmlElement el = document.CreateElement(c.Id);
            c.SaveState(document, el);
            stateNode.AppendChild(el);
        }
        node.AppendChild(stateNode);
        return node;
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);

        XmlElement stateNode = ((XmlElement)settings)["CounterState"];
        if (stateNode != null)
        {
            foreach (ISmwCounter c in counters)
            {
                XmlElement el = stateNode[c.Id];
                if (el != null) { c.LoadState(el); }
            }
        }
    }

    public int GetSettingsHashCode()
    {
        int hash = Settings.GetSettingsHashCode();
        foreach (ISmwCounter c in counters) { hash ^= c.Value.GetHashCode(); }
        return hash;
    }

    public void Dispose()
    {
        pollTimer?.Dispose();
        Settings.Hook.KeyOrButtonPressed -= Hook_KeyOrButtonPressed;
        Settings.Hook.UnregisterAllHotkeys();
    }
}
