using System.Collections.Generic;
using Godot;
using Sentinel.Config;

namespace Sentinel.UI;

/// <summary>
/// The Research branch drawn as PDTD's hex tech tree rather than a scrolling list:
/// nodes as hexagons connected by a spine that zigzags left/right as it climbs, lit
/// where you've bought in and dark where you haven't.
///
/// A branch is a straight chain by tier (data/research.json has 1-6 per branch, with
/// several nodes sharing a tier), so the layout is: one row per tier, bottom to top —
/// PDTD reads bottom-up, with the node you can afford next glowing at the bottom — and
/// the row alternates which side of the centre line it leans toward, which is what gives
/// the reference its zigzag. The connecting line is drawn behind the hexes so it reads as
/// a spine rather than as borders touching.
///
/// Self-drawn rather than assembled from Controls: a hexagon with a stroke, a fill, a
/// glow and a line into its parent is far less code as one _Draw than as five nested
/// nodes per cell, and it keeps hit-testing to a single point-in-hex check.
/// </summary>
public sealed partial class ResearchTree : Control
{
    public sealed class Cell
    {
        public string Id = "";
        public string Name = "";
        public int Tier;
        public int Ranks;          // bought
        public int RankCount;      // max
        public bool Affordable;
        public bool Locked;        // gated (commander level / prerequisite)
        public bool Capstone;
        public Vector2 Pos;        // centre, filled in by Layout()
    }

    public readonly List<Cell> Cells = new();
    public Color Accent = new(0.35f, 0.72f, 1f);
    public System.Action<string>? OnPick;

    private const float HexR = 44f;        // circumradius
    private const float RowGap = 116f;
    private float _t;
    private int _hover = -1;

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    /// <summary>Place every cell. Rows are tiers, bottom (tier 1) to top; cells within a
    /// tier spread horizontally, and each row leans to alternating sides so the spine
    /// zigzags the way PDTD's does.</summary>
    public void Layout(float width)
    {
        var byTier = new Dictionary<int, List<Cell>>();
        int maxTier = 1;
        foreach (var c in Cells)
        {
            if (!byTier.TryGetValue(c.Tier, out var l)) byTier[c.Tier] = l = new List<Cell>();
            l.Add(c);
            if (c.Tier > maxTier) maxTier = c.Tier;
        }

        float cx = width * 0.5f;
        float lean = width * 0.16f;
        for (int tier = 1; tier <= maxTier; tier++)
        {
            if (!byTier.TryGetValue(tier, out var row)) continue;
            // tier 1 at the bottom
            float y = (maxTier - tier) * RowGap + HexR + 14f;
            float side = (tier % 2 == 0) ? 1f : -1f;
            float spread = HexR * 2.35f;
            float x0 = cx + side * lean - (row.Count - 1) * spread * 0.5f;
            for (int i = 0; i < row.Count; i++)
                row[i].Pos = new Vector2(x0 + i * spread, y);
        }

        CustomMinimumSize = new Vector2(width, maxTier * RowGap + HexR * 2f + 20f);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            int h = HitTest(mm.Position);
            if (h != _hover) { _hover = h; QueueRedraw(); }
            return;
        }
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            int h = HitTest(mb.Position);
            if (h >= 0) { OnPick?.Invoke(Cells[h].Id); AcceptEvent(); }
        }
        else if (e is InputEventScreenTouch { Pressed: true } st)
        {
            int h = HitTest(st.Position);
            if (h >= 0) { OnPick?.Invoke(Cells[h].Id); AcceptEvent(); }
        }
    }

    private int HitTest(Vector2 p)
    {
        for (int i = 0; i < Cells.Count; i++)
            if (Cells[i].Pos.DistanceTo(p) <= HexR) return i;
        return -1;
    }

    private static Vector2[] Hex(Vector2 c, float r)
    {
        var pts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            // flat-top hexagon, matching the reference
            float a = Mathf.Pi / 3f * i - Mathf.Pi / 6f;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }

    public override void _Draw()
    {
        if (Cells.Count == 0) return;

        // --- spine, behind the hexes: link each tier's cells to the tier below ---
        var byTier = new Dictionary<int, List<Cell>>();
        foreach (var c in Cells)
        {
            if (!byTier.TryGetValue(c.Tier, out var l)) byTier[c.Tier] = l = new List<Cell>();
            l.Add(c);
        }
        foreach (var (tier, row) in byTier)
        {
            if (!byTier.TryGetValue(tier - 1, out var below)) continue;
            foreach (var c in row)
            {
                // nearest cell on the tier below — keeps the chain readable when a tier
                // holds several nodes
                Cell? best = null;
                float bd = float.MaxValue;
                foreach (var b in below)
                {
                    float d = b.Pos.DistanceSquaredTo(c.Pos);
                    if (d < bd) { bd = d; best = b; }
                }
                if (best == null) continue;
                bool live = best.Ranks > 0;
                DrawLine(best.Pos, c.Pos, new Color(Accent, live ? 0.55f : 0.16f), live ? 5f : 3f);
                if (live) DrawLine(best.Pos, c.Pos, new Color(1, 1, 1, 0.22f), 1.6f);
            }
        }

        // --- hexes ---
        for (int i = 0; i < Cells.Count; i++)
        {
            var c = Cells[i];
            bool done = c.Ranks >= c.RankCount && c.RankCount > 0;
            bool started = c.Ranks > 0;
            bool open = !c.Locked && c.Affordable && !done;

            var poly = Hex(c.Pos, HexR);

            // ready-to-buy pulse
            if (open)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 3.2f);
                DrawPolylineColors(Hex(c.Pos, HexR + 6f + pulse * 2f), new[] { new Color(Accent, 0.16f + 0.14f * pulse) }, 4f);
            }

            Color fill = done ? new Color(Accent, 0.42f)
                       : started ? new Color(Accent, 0.24f)
                       : c.Locked ? new Color(0.10f, 0.13f, 0.18f, 0.9f)
                       : new Color(Accent, 0.09f);
            DrawColoredPolygon(poly, fill);

            var edge = done ? new Color(1f, 0.92f, 0.6f, 0.95f)
                     : c.Locked ? new Color(0.4f, 0.45f, 0.55f, 0.5f)
                     : new Color(Accent, open ? 0.95f : 0.55f);
            var loop = new Vector2[7];
            poly.CopyTo(loop, 0); loop[6] = poly[0];
            DrawPolyline(loop, edge, c.Capstone ? 4.5f : 2.6f, true);

            if (_hover == i)
                DrawPolyline(loop, new Color(1, 1, 1, 0.4f), 2f, true);

            // rank pips around the bottom edge
            if (c.RankCount > 1)
            {
                float gap = 11f;
                float total = (c.RankCount - 1) * gap;
                for (int r = 0; r < c.RankCount; r++)
                {
                    var pp = c.Pos + new Vector2(-total * 0.5f + r * gap, HexR * 0.58f);
                    DrawCircle(pp, 3.4f, r < c.Ranks ? new Color(1f, 0.92f, 0.6f) : new Color(1, 1, 1, 0.22f));
                }
            }

            // name, wrapped onto two short lines inside the hex
            var font = ThemeDB.FallbackFont;
            string label = c.Name.Length > 22 ? c.Name[..21] + "…" : c.Name;
            var words = label.Split(' ');
            string l1 = words[0], l2 = "";
            for (int wI = 1; wI < words.Length; wI++)
            {
                if (l1.Length + 1 + words[wI].Length <= 11 && l2.Length == 0) l1 += " " + words[wI];
                else l2 += (l2.Length > 0 ? " " : "") + words[wI];
            }
            var tcol = c.Locked ? new Color(1, 1, 1, 0.45f) : new Color(1, 1, 1, 0.92f);
            float ty = c.Pos.Y - (l2.Length > 0 ? 6f : 0f);
            DrawString(font, new Vector2(c.Pos.X - HexR, ty), l1, HorizontalAlignment.Center, HexR * 2f, 13, tcol);
            if (l2.Length > 0)
                DrawString(font, new Vector2(c.Pos.X - HexR, ty + 14f), l2, HorizontalAlignment.Center, HexR * 2f, 13, tcol);
        }
    }

    private void DrawPolylineColors(Vector2[] pts, Color[] cols, float w)
    {
        var loop = new Vector2[pts.Length + 1];
        pts.CopyTo(loop, 0); loop[^1] = pts[0];
        DrawPolyline(loop, cols[0], w, true);
    }
}
