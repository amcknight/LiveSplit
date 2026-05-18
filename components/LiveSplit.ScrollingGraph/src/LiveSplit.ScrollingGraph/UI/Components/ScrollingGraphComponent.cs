using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

public class ScrollingGraphComponent : IComponent
{
    // Property names to probe on sibling components (in order).
    private static readonly string[] CandidateProperties = { "Count", "Deaths", "Value" };

    private readonly LiveSplitState state;
    private readonly Timer pollTimer;
    private readonly GraphicsCache cache = new();

    // Time-series ring buffer of (timestamp, value) samples.
    private readonly LinkedList<(DateTime t, double v)> samples = new();

    // Bar events for completed splits, list of (timestamp, deltaMs, segmentName).
    private readonly List<(DateTime t, double deltaMs, string name)> bars = new();
    private DateTime runStart;

    // Cached reflection lookup for the configured source component.
    private IComponent cachedSource;
    private PropertyInfo cachedProp;

    public ScrollingGraphComponentSettings Settings { get; }

    public string ComponentName => "Scrolling Graph";

    public float VerticalHeight => Settings.GraphHeight;
    public float HorizontalWidth => Settings.GraphWidth;
    public float MinimumHeight => 30;
    public float MinimumWidth => 60;

    public float PaddingTop => 0;
    public float PaddingBottom => 0;
    public float PaddingLeft => 0;
    public float PaddingRight => 0;

    public IDictionary<string, Action> ContextMenuControls => null;

    public ScrollingGraphComponent(LiveSplitState state)
    {
        this.state = state;
        Settings = new ScrollingGraphComponentSettings();
        Settings.SourcesProvider = ListAvailableSources;

        state.OnSplit += State_OnSplit;
        state.OnUndoSplit += State_OnUndoSplit;
        state.OnReset += State_OnReset;

        pollTimer = new Timer { Interval = 100 };
        pollTimer.Tick += (_, __) => Sample();
        pollTimer.Enabled = true;

        runStart = DateTime.UtcNow;
    }

    // --------------------------------------------------------------------
    // Source discovery + sampling

    private List<string> ListAvailableSources()
    {
        var names = new List<string>();
        if (state.Layout?.LayoutComponents == null) { return names; }

        foreach (ILayoutComponent lc in state.Layout.LayoutComponents)
        {
            IComponent c = lc.Component;
            if (c == this) { continue; }
            if (FindCandidateProperty(c) != null) { names.Add(c.ComponentName); }
        }
        return names;
    }

    private static PropertyInfo FindCandidateProperty(IComponent c)
    {
        Type t = c.GetType();
        foreach (string name in CandidateProperties)
        {
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanRead && (p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(long)))
            {
                return p;
            }
        }
        return null;
    }

    private bool TryReadSource(out double value)
    {
        value = 0;
        // Re-resolve if the source disappeared or settings changed.
        if (cachedSource == null
            || cachedProp == null
            || cachedSource.ComponentName != Settings.SourceComponentName
            || !state.Layout.LayoutComponents.Any(lc => lc.Component == cachedSource))
        {
            cachedSource = null;
            cachedProp = null;
            if (state.Layout?.LayoutComponents == null) { return false; }
            foreach (ILayoutComponent lc in state.Layout.LayoutComponents)
            {
                if (lc.Component == this) { continue; }
                if (lc.Component.ComponentName == Settings.SourceComponentName)
                {
                    PropertyInfo prop = FindCandidateProperty(lc.Component);
                    if (prop != null)
                    {
                        cachedSource = lc.Component;
                        cachedProp = prop;
                        break;
                    }
                }
            }
            if (cachedSource == null) { return false; }
        }

        try { value = Convert.ToDouble(cachedProp.GetValue(cachedSource)); return true; }
        catch { return false; }
    }

    private void Sample()
    {
        if (!TryReadSource(out double v)) { return; }
        DateTime now = DateTime.UtcNow;
        samples.AddLast((now, v));

        // Evict samples older than the window.
        DateTime cutoff = now - TimeSpan.FromSeconds(Settings.WindowSeconds);
        while (samples.First != null && samples.First.Value.t < cutoff)
        {
            samples.RemoveFirst();
        }
    }

    // --------------------------------------------------------------------
    // Split events

    private void State_OnSplit(object sender, EventArgs e)
    {
        // The split that JUST completed is at CurrentSplitIndex - 1.
        int finishedIdx = state.CurrentSplitIndex - 1;
        if (finishedIdx < 0 || finishedIdx >= state.Run.Count) { return; }

        string comparison = ResolveComparison();
        TimingMethod method = state.CurrentTimingMethod;

        ISegment seg = state.Run[finishedIdx];
        TimeSpan? splitTime = seg.SplitTime[method];
        TimeSpan? compTime = seg.Comparisons[comparison][method];
        if (splitTime == null || compTime == null) { return; }

        double deltaMs = (splitTime.Value - compTime.Value).TotalMilliseconds;
        bars.Add((DateTime.UtcNow, deltaMs, seg.Name));
    }

    private void State_OnUndoSplit(object sender, EventArgs e)
    {
        if (bars.Count > 0) { bars.RemoveAt(bars.Count - 1); }
    }

    private void State_OnReset(object sender, TimerPhase e)
    {
        bars.Clear();
        samples.Clear();
        runStart = DateTime.UtcNow;
    }

    private string ResolveComparison()
    {
        string c = Settings.Comparison;
        if (string.IsNullOrEmpty(c) || c == "Current Comparison") { return state.CurrentComparison; }
        return state.Run.Comparisons.Contains(c) ? c : state.CurrentComparison;
    }

    // --------------------------------------------------------------------
    // IComponent

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        cache.Restart();
        cache["samples"] = samples.Count;
        cache["bars"] = bars.Count;
        cache["latest"] = samples.Count > 0 ? samples.Last.Value.v : 0;
        cache["source"] = Settings.SourceComponentName ?? "";
        cache["mode"] = Settings.RateMode;
        cache["window"] = Settings.WindowSeconds;

        // Always invalidate at ~10Hz so the line scrolls smoothly even when nothing else changed.
        if (invalidator != null)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        => Render(g, HorizontalWidth, height);

    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        => Render(g, width, VerticalHeight);

    public Control GetSettingsControl(LayoutMode mode) => Settings;
    public XmlNode GetSettings(XmlDocument document) => Settings.GetSettings(document);
    public void SetSettings(XmlNode node) => Settings.SetSettings(node);
    public int GetSettingsHashCode() => Settings.GetSettingsHashCode();

    public void Dispose()
    {
        pollTimer?.Dispose();
        state.OnSplit -= State_OnSplit;
        state.OnUndoSplit -= State_OnUndoSplit;
        state.OnReset -= State_OnReset;
    }

    // --------------------------------------------------------------------
    // Rendering

    private void Render(Graphics g, float width, float height)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background.
        using (var bg = new SolidBrush(Settings.BackgroundColor))
        {
            g.FillRectangle(bg, 0, 0, width, height);
        }

        // Centerline.
        using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255)))
        {
            g.DrawLine(pen, 0, height / 2f, width, height / 2f);
        }

        DrawBars(g, width, height);
        DrawLine(g, width, height);
        DrawOverlayText(g, width, height);
    }

    private void DrawBars(Graphics g, float width, float height)
    {
        if (bars.Count == 0) { return; }

        // Bars span the whole RUN, not just the scrolling window — the user wants a per-segment history.
        // X = bar index across the available width, evenly spaced.
        double maxAbs = bars.Max(b => Math.Abs(b.deltaMs));
        if (maxAbs < 1) { maxAbs = 1; }

        float mid = height / 2f;
        float maxBarHeight = height * 0.45f;
        float barAreaWidth = width;
        float barWidth = Math.Max(2f, barAreaWidth / Math.Max(bars.Count, 8) * 0.6f);

        for (int i = 0; i < bars.Count; i++)
        {
            (DateTime _, double deltaMs, string _) = bars[i];
            float xCenter = (i + 0.5f) * (barAreaWidth / Math.Max(bars.Count, 1));
            float h = (float)(Math.Abs(deltaMs) / maxAbs) * maxBarHeight;

            Color color = deltaMs <= 0 ? Settings.AheadColor : Settings.BehindColor;
            using var brush = new SolidBrush(Color.FromArgb(200, color));
            if (deltaMs <= 0)
            {
                // Ahead: bar grows UP from centerline.
                g.FillRectangle(brush, xCenter - barWidth / 2f, mid - h, barWidth, h);
            }
            else
            {
                // Behind: bar grows DOWN.
                g.FillRectangle(brush, xCenter - barWidth / 2f, mid, barWidth, h);
            }
        }
    }

    private void DrawLine(Graphics g, float width, float height)
    {
        if (samples.Count < 2) { return; }

        // Build the visible series — raw values, or per-sample rate (derivative).
        var pts = new List<(DateTime t, double v)>(samples.Count);
        if (Settings.RateMode)
        {
            // Rate = change per second over a short sliding window.
            DateTime windowStart = samples.Last.Value.t - TimeSpan.FromSeconds(1);
            (DateTime t, double v) prev = (default, 0);
            bool havePrev = false;
            foreach ((DateTime t, double v) s in samples)
            {
                if (havePrev)
                {
                    double dt = (s.t - prev.t).TotalSeconds;
                    if (dt > 0)
                    {
                        pts.Add((s.t, (s.v - prev.v) / dt));
                    }
                }
                prev = s;
                havePrev = true;
            }
            if (pts.Count == 0) { return; }
        }
        else
        {
            foreach ((DateTime t, double v) s in samples) { pts.Add(s); }
        }

        double minV = pts.Min(p => p.v);
        double maxV = pts.Max(p => p.v);
        if (Math.Abs(maxV - minV) < 1e-6) { maxV = minV + 1; }

        DateTime tNow = samples.Last.Value.t;
        double windowSec = Settings.WindowSeconds;

        var points = new PointF[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            double agoSec = (tNow - pts[i].t).TotalSeconds;
            float x = (float)((1 - agoSec / windowSec) * width);
            float y = (float)(height - (pts[i].v - minV) / (maxV - minV) * height);
            points[i] = new PointF(x, y);
        }

        // Filled area below line.
        var fillPath = new GraphicsPath();
        fillPath.AddLines(points);
        fillPath.AddLine(points[points.Length - 1], new PointF(points[points.Length - 1].X, height));
        fillPath.AddLine(new PointF(points[points.Length - 1].X, height), new PointF(points[0].X, height));
        fillPath.CloseFigure();
        using (var fill = new SolidBrush(Color.FromArgb(60, Settings.LineColor)))
        {
            g.FillPath(fill, fillPath);
        }
        using (var pen = new Pen(Settings.LineColor, 2f))
        {
            g.DrawLines(pen, points);
        }
    }

    private void DrawOverlayText(Graphics g, float width, float height)
    {
        using var font = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));

        string srcText = string.IsNullOrEmpty(Settings.SourceComponentName) || cachedSource == null
            ? "(no source)"
            : Settings.SourceComponentName + (Settings.RateMode ? " /s" : "");
        g.DrawString(srcText, font, brush, 4, 2);

        if (samples.Count > 0)
        {
            double val = samples.Last.Value.v;
            string s = Settings.RateMode ? RecentRate().ToString("0.0") : val.ToString("0");
            SizeF sz = g.MeasureString(s, font);
            g.DrawString(s, font, brush, width - sz.Width - 4, 2);
        }
    }

    private double RecentRate()
    {
        if (samples.Count < 2) { return 0; }
        var last = samples.Last.Value;
        DateTime cutoff = last.t - TimeSpan.FromSeconds(1);
        (DateTime t, double v)? oldest = null;
        foreach ((DateTime t, double v) s in samples)
        {
            if (s.t >= cutoff) { oldest = s; break; }
        }
        if (oldest == null) { return 0; }
        double dt = (last.t - oldest.Value.t).TotalSeconds;
        return dt > 0 ? (last.v - oldest.Value.v) / dt : 0;
    }
}
