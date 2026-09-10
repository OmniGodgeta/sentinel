using System.Collections.Generic;
using Godot;
using Sentinel.Game;
using Sentinel.Render;

namespace Sentinel.UI;

/// <summary>
/// Level select as a star map — glowing star nodes on a winding route, the way
/// you'd chart a campaign. Node 1 is bottom-left; the route climbs to the boss
/// and then the Endless anomaly. Tap an unlocked star to launch it.
/// </summary>
public sealed partial class StarMapScreen : CanvasLayer
{
    public AppRoot App = null!;

    private readonly List<(string id, string file, string name, Vector2 f)> _nodes = new();
    private MapCanvas _canvas = null!;
    private HBoxContainer _ascRow = null!;

    // winding route in fractional coords (0..1 of the map area)
    private static readonly Vector2[] Route =
    {
        new(0.17f, 0.86f), new(0.36f, 0.75f), new(0.22f, 0.61f), new(0.47f, 0.53f),
        new(0.63f, 0.62f), new(0.52f, 0.40f), new(0.74f, 0.30f), new(0.58f, 0.15f),
    };

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = -0.5f, NebulaAlpha = 0.35f });

        int i = 0;
        foreach (var m in App.Cfg.Arc.Missions)
        {
            var f = i < Route.Length ? Route[i] : new Vector2(0.5f, 0.1f);
            _nodes.Add((m.Id, m.File, m.Name, f));
            i++;
        }
        _nodes.Add(("endless", "res://data/missions/endless.json", "◈ ENDLESS", new Vector2(0.85f, 0.08f)));

        var top = new Control { AnchorLeft = 0f, AnchorRight = 1f, OffsetTop = 14, OffsetBottom = 78 };
        top.Theme = UiTheme.Instance;
        AddChild(top);
        var back = new Button { Text = "‹ Back", Position = new Vector2(16, 0), CustomMinimumSize = new Vector2(150, 60) };
        back.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Back(); App.ShowMenu(); };
        top.AddChild(back);
        var hdr = new Label { Text = App.Cfg.Arc.Name, Position = new Vector2(186, 16) };
        hdr.AddThemeFontSizeOverride("font_size", 20);
        top.AddChild(hdr);

        _ascRow = new HBoxContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetTop = 90, OffsetLeft = -220, OffsetRight = 220, Alignment = BoxContainer.AlignmentMode.Center };
        _ascRow.Theme = UiTheme.Instance;
        _ascRow.AddThemeConstantOverride("separation", 10);
        AddChild(_ascRow);

        _canvas = new MapCanvas { Screen = this };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.OffsetTop = 150;
        AddChild(_canvas);

        RebuildAsc();
    }

    private void RebuildAsc()
    {
        App.RefreshProgression();
        var s = App.Save; var p = App.Prog;
        foreach (Node c in _ascRow.GetChildren()) c.QueueFree();
        int max = p.AscensionMax;
        if (max <= 0) return;
        if (s.AscensionTier > max) s.AscensionTier = max;
        var minus = new Button { Text = "−", CustomMinimumSize = new Vector2(64, 56) };
        minus.AddThemeFontSizeOverride("font_size", 24);
        minus.Pressed += () => { s.AscensionTier = Mathf.Max(0, s.AscensionTier - 1); s.Save(); RebuildAsc(); };
        _ascRow.AddChild(minus);
        string nm = s.AscensionTier == 0 ? "off" : App.Cfg.Ascension.Find(a => a.Tier == s.AscensionTier)?.Name ?? "";
        var lbl = new Label { Text = $"  Ascension {s.AscensionTier}/{max} · {nm}  " };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        _ascRow.AddChild(lbl);
        var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(64, 56) };
        plus.AddThemeFontSizeOverride("font_size", 24);
        plus.Pressed += () => { s.AscensionTier = Mathf.Min(max, s.AscensionTier + 1); s.Save(); RebuildAsc(); };
        _ascRow.AddChild(plus);
    }

    private List<string> ArcIds()
    {
        var l = new List<string>();
        foreach (var m in App.Cfg.Arc.Missions) l.Add(m.Id);
        return l;
    }

    private sealed partial class MapCanvas : Control
    {
        public StarMapScreen Screen = null!;
        private float _t;

        public override void _Ready() { SetProcess(true); }
        public override void _Process(double d) { _t += (float)d; QueueRedraw(); }

        private Vector2 NodePos(Vector2 f) => new(f.X * Size.X, f.Y * Size.Y);

        public override void _GuiInput(InputEvent e)
        {
            if (e is not (InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })) return;
            Vector2 p = e is InputEventMouseButton mb ? mb.Position : ((InputEventScreenTouch)e).Position;
            var s = Screen; var save = s.App.Save; var prog = s.App.Prog;
            var ids = s.ArcIds();
            foreach (var (id, file, _, f) in s._nodes)
            {
                if (NodePos(f).DistanceTo(p) > 58f) continue;
                bool open = id == "endless" || save.IsUnlocked(id, ids);
                if (!open) { Sentinel.Audio.AudioManager.Instance?.Play("ui_error", -6f); return; }
                Sentinel.Audio.AudioManager.Instance?.Confirm();
                s.App.StartMission(file, id);
                return;
            }
        }

        public override void _Draw()
        {
            var s = Screen; var save = s.App.Save; var ids = s.ArcIds();

            // topographic contours
            var c0 = Size * new Vector2(0.35f, 0.55f);
            for (int r = 40; r < 900; r += 46)
            {
                int seg = 40;
                var pts = new Vector2[seg + 1];
                for (int k = 0; k <= seg; k++)
                {
                    float a = k * Mathf.Tau / seg;
                    float wob = Mathf.Sin(a * 3f + r * 0.05f) * 10f + Mathf.Sin(a * 7f) * 5f;
                    pts[k] = c0 + Vector2.FromAngle(a) * (r + wob);
                }
                DrawPolyline(pts, new Color(0.4f, 0.55f, 0.85f, 0.07f), 1f);
            }

            // route lines
            for (int i = 0; i < s._nodes.Count - 1; i++)
            {
                var a = NodePos(s._nodes[i].f);
                var b = NodePos(s._nodes[i + 1].f);
                bool doneA = s._nodes[i].id == "endless" || save.Record(s._nodes[i].id).Cleared;
                var col = doneA ? new Color(0.5f, 0.85f, 1f, 0.5f) : new Color(1, 1, 1, 0.12f);
                DrawLine(a, b, col, 2f);
                // travelling spark on completed legs
                if (doneA)
                {
                    float ph = (_t * 0.4f + i * 0.2f) % 1f;
                    DrawCircle(a.Lerp(b, ph), 2.5f, new Color(0.7f, 0.95f, 1f, 0.8f));
                }
            }

            // nodes
            int idx = 0;
            int nextIdx = NextToPlay(save, ids);
            foreach (var (id, _, name, f) in s._nodes)
            {
                var pos = NodePos(f);
                bool endless = id == "endless";
                bool open = endless || save.IsUnlocked(id, ids);
                var rec = endless ? null : save.Record(id);
                bool cleared = rec?.Cleared ?? (save.EndlessBest > 0);
                bool isNext = idx == nextIdx;

                float baseR = endless ? 24f : idx == s._nodes.Count - 2 ? 23f : 18f; // boss node bigger
                var col = !open ? new Color(0.4f, 0.45f, 0.55f)
                        : cleared ? new Color(0.4f, 0.86f, 0.92f)      // icon teal
                        : new Color(0.95f, 0.55f, 0.78f);             // icon magenta = "next / unplayed"

                if (open)
                {
                    DrawCircle(pos, baseR * 2.8f, new Color(col, 0.10f));
                    DrawCircle(pos, baseR * 1.7f, new Color(col, 0.16f));
                }
                // 4-point star
                DrawStar(pos, baseR, col, open ? 1f : 0.5f);

                if (isNext && open)
                {
                    float pr = baseR + 12f + 4f * Mathf.Sin(_t * 4f);
                    DrawArc(pos, pr, 0, Mathf.Tau, 32, new Color(0.95f, 0.35f, 0.62f), 3f);
                }
                if (!open)
                    DrawString(ThemeDB.FallbackFont, pos + new Vector2(-8, 7), "🔒", HorizontalAlignment.Center, 18, 18);

                // label
                string lbl = open ? name.ToUpperInvariant() : $"LEVEL {idx + 1}";
                DrawString(ThemeDB.FallbackFont, pos + new Vector2(-120, baseR + 24f), lbl,
                           HorizontalAlignment.Center, 240, 16, new Color(1, 1, 1, open ? 0.9f : 0.4f));
                if (cleared && rec != null && rec.Stars > 0)
                {
                    string st = new string('★', rec.Stars) + new string('☆', 3 - rec.Stars);
                    DrawString(ThemeDB.FallbackFont, pos + new Vector2(-50, baseR + 44f), st,
                               HorizontalAlignment.Center, 100, 16, new Color(1f, 0.85f, 0.4f));
                }
                idx++;
            }
        }

        private int NextToPlay(Meta.SaveGame save, List<string> ids)
        {
            for (int i = 0; i < Screen._nodes.Count; i++)
            {
                var id = Screen._nodes[i].id;
                if (id == "endless") continue;
                if (!save.Record(id).Cleared && save.IsUnlocked(id, ids)) return i;
            }
            return -1;
        }

        private void DrawStar(Vector2 c, float r, Color col, float a)
        {
            var pts = new Vector2[8];
            for (int i = 0; i < 8; i++)
                pts[i] = c + Vector2.FromAngle(i * Mathf.Pi / 4f + Mathf.Pi / 8f) * (i % 2 == 0 ? r : r * 0.38f);
            DrawColoredPolygon(pts, new Color(col, a));
            DrawCircle(c, r * 0.3f, new Color(1, 1, 1, a));
        }
    }
}
