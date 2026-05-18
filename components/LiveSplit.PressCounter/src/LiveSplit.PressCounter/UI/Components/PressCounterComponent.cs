using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Input;

namespace LiveSplit.UI.Components;

public class PressCounterComponent : IComponent
{
    private readonly LiveSplitState state;
    private readonly SimpleLabel nameLabel = new();
    private readonly SimpleLabel valueLabel = new();
    private readonly GraphicsCache cache = new();

    public PressCounterComponentSettings Settings { get; }
    public int Count { get; private set; }

    public string ComponentName => "Press Counter";

    public float VerticalHeight { get; private set; } = 10f;
    public float MinimumHeight { get; private set; }
    public float HorizontalWidth { get; private set; }
    public float MinimumWidth => 60f;

    public float PaddingTop { get; private set; }
    public float PaddingBottom { get; private set; }
    public float PaddingLeft => 7f;
    public float PaddingRight => 7f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public PressCounterComponent(LiveSplitState state)
    {
        this.state = state;

        bool allowGamepads = state.Settings.HotkeyProfiles.First().Value.AllowGamepadsAsHotkeys;
        Settings = new PressCounterComponentSettings(allowGamepads);
        Settings.Hook.KeyOrButtonPressed += Hook_KeyOrButtonPressed;
    }

    private void Hook_KeyOrButtonPressed(object sender, KeyOrButton e)
    {
        // Respect the same global-hotkey-vs-LiveSplit-focus rule the existing Counter uses.
        bool focused = Form.ActiveForm == state.Form;
        if (!focused && !Settings.GlobalHotkeysEnabled)
        {
            return;
        }

        if (e == Settings.IncrementKey)
        {
            Count++;
        }
        else if (e == Settings.ResetKey)
        {
            Count = 0;
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        try { Settings.Hook?.Poll(); } catch { }

        nameLabel.Text = string.IsNullOrEmpty(Settings.Label) ? "Presses:" : Settings.Label;
        valueLabel.Text = Count.ToString();

        cache.Restart();
        cache["name"] = nameLabel.Text;
        cache["value"] = valueLabel.Text;

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

        float valueWidth = g.MeasureString("00000", font).Width;
        HorizontalWidth = nameLabel.ActualWidth + valueWidth + 15;

        ConfigureLabel(nameLabel, font, textColor, StringAlignment.Near, 5, width - valueWidth - 5, height);
        ConfigureLabel(valueLabel, font, textColor, StringAlignment.Far, 5, width - 10, height);

        nameLabel.Draw(g);
        valueLabel.Draw(g);
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

    public XmlNode GetSettings(XmlDocument document) => Settings.GetSettings(document);

    public void SetSettings(XmlNode settings) => Settings.SetSettings(settings);

    public int GetSettingsHashCode() => Settings.GetSettingsHashCode();

    public void Dispose()
    {
        Settings.Hook.KeyOrButtonPressed -= Hook_KeyOrButtonPressed;
        Settings.Hook.UnregisterAllHotkeys();
    }
}
