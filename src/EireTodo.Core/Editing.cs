namespace EireTodo.Core;

public enum TextJustification { Left, Centre, Right }
public sealed class TextFormat
{
    public string Family { get; set; } = "Segoe UI";
    public double Size { get; set; } = 17;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public TextJustification Alignment { get; set; }
    public TextFormat Clone() => (TextFormat)MemberwiseClone();
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Family) || Family.Length > 100 || Family.IndexOfAny(['/', '\\', ':', '#']) >= 0 ||
            !double.IsFinite(Size) || Size < 12 || Size > 48 || !Enum.IsDefined(Alignment))
            throw new ArgumentException("Choose a font name, a size from 12 to 48, and a valid alignment.");
    }
}

public enum NodeDrop { Before, After, Child }
public enum DiagramKind { Hierarchy, Network }
public enum LeadSide { Auto, Top, Right, Bottom, Left }
public enum LeadRouting { Curve, SharpBends }
public sealed class DiagramLead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid From { get; set; }
    public Guid To { get; set; }
    public bool DoubleHeaded { get; set; }
    public LeadRouting Routing { get; set; }
    public string Description { get; set; } = "";
    public LeadSide FromSide { get; set; }
    public LeadSide ToSide { get; set; }
}

public static class NetworkCharts
{
    public const int MaxLeads = 5000;
    public static Diagram Create(string name)
    {
        var chart = new Diagram { Name = name, Kind = DiagramKind.Network, Layout = ChartLayout.Freeform };
        Add(chart, null, false, name); return chart;
    }
    public static ChartNode Add(Diagram chart, Guid? anchor, bool connected, string title = "New node", bool doubleHeaded = false, LeadRouting routing = LeadRouting.Curve, TextFormat? format = null, bool preferLeft = false)
    {
        if (chart.Kind != DiagramKind.Network || chart.Nodes.Count >= Charts.MaxNodes) throw new ArgumentException("Cannot add a network node to this diagram.");
        var node = new ChartNode { Number = chart.NextNumber++, Order = chart.Nodes.Count, Title = title, Format = format?.Clone() ?? new() };
        var around = chart.Nodes.FirstOrDefault(n => n.Id == anchor);
        var x = around?.X ?? 40; var y = around?.Y ?? 40;
        var levels = DiagramAppearance.Levels(chart);
        var size = ChartGeometry.Measure(node, around is null ? 1 : levels[around.Id] + 1);
        var occupied = chart.Nodes.Select(n => { var s = ChartGeometry.Measure(n, levels[n.Id]); return new NodeBox(n.Id, n.X ?? 40, n.Y ?? 40, s.Width, s.Height); }).ToList();
        bool Free(double px, double py) => occupied.All(b => px + size.Width + 28 <= b.X || b.X + b.Width + 28 <= px || py + size.Height + 28 <= b.Y || b.Y + b.Height + 28 <= py);
        // Search outward from the selected node; reserve a full cell plus a readable gap.
        bool found = false;
        for (var radius = 0; radius <= 64 && !found; radius++)
        {
            var directions = new List<(int X, int Y)> { (preferLeft ? -radius : radius, 0), (0, radius), (preferLeft ? radius : -radius, 0), (0, -radius) };
            for (var dy = -radius; dy <= radius; dy++) for (var dx = -radius; dx <= radius; dx++)
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == radius) directions.Add((dx, dy));
            foreach (var (dx, dy) in directions.Distinct())
            {
                var px = x + dx * (size.Width + 74); var py = y + dy * (size.Height + 74);
                if (Math.Abs(px) > 100000 || Math.Abs(py) > 100000 || !Free(px, py)) continue;
                node.X = px; node.Y = py; found = true; break;
            }
        }
        if (!found) throw new ArgumentException("No free position was found. Move existing nodes to make space.");
        chart.Nodes.Add(node);
        if (connected && anchor.HasValue) Connect(chart, anchor.Value, node.Id, doubleHeaded, routing);
        Charts.Validate(chart); return node;
    }
    public static DiagramLead Connect(Diagram chart, Guid from, Guid to, bool doubleHeaded = false, LeadRouting routing = LeadRouting.Curve, string description = "")
    {
        if (chart.Kind != DiagramKind.Network || from == to || !chart.Nodes.Any(n => n.Id == from) || !chart.Nodes.Any(n => n.Id == to))
            throw new ArgumentException("Select two different nodes in this connection diagram.");
        if (chart.Leads.Count >= MaxLeads) throw new ArgumentException("This diagram has reached its 5,000-lead limit.");
        var lead = new DiagramLead { From = from, To = to, DoubleHeaded = doubleHeaded, Routing = routing, Description = description.Trim() };
        chart.Leads.Add(lead); Charts.Validate(chart); return lead;
    }
    public static void Move(Diagram chart, Guid id, double x, double y)
    {
        var node = chart.Nodes.Single(n => n.Id == id); node.X = Math.Clamp(x, -100000, 100000); node.Y = Math.Clamp(y, -100000, 100000); Charts.Validate(chart);
    }
}

public sealed record LeadPath(double X1, double Y1, double C1X, double C1Y, double C2X, double C2Y, double X2, double Y2, double LabelX, double LabelY, double LabelAngle)
{
    public IReadOnlyList<LeadPoint>? Bends { get; init; }
    public double LabelWidth => Math.Clamp(Math.Sqrt(Math.Pow(C2X - C1X, 2) + Math.Pow(C2Y - C1Y, 2)) > 60 ? Math.Sqrt(Math.Pow(C2X - C1X, 2) + Math.Pow(C2Y - C1Y, 2)) - 20 : Math.Sqrt(Math.Pow(X2 - X1, 2) + Math.Pow(Y2 - Y1, 2)) - 20, 45, 280);
}
public sealed record LeadPoint(double X, double Y);
public static class LeadGeometry
{
    public static LeadPoint Port(NodeBox box, LeadSide side) => side switch
    {
        LeadSide.Top => new(box.X + box.Width / 2, box.Y), LeadSide.Right => new(box.X + box.Width, box.Y + box.Height / 2),
        LeadSide.Bottom => new(box.X + box.Width / 2, box.Y + box.Height), LeadSide.Left => new(box.X, box.Y + box.Height / 2),
        _ => throw new ArgumentException("Choose a node side.")
    };
    public static IReadOnlyList<LeadPoint> Vertices(LeadPath path) => path.Bends ?? [new(path.X1, path.Y1), new(path.C1X, path.C1Y), new(path.C2X, path.C2Y), new(path.X2, path.Y2)];
    private static LeadPath PortRoute(NodeBox from, NodeBox to, LeadRouting routing, int lane, LeadSide first, LeadSide last)
    {
        var dx = to.X + to.Width / 2 - from.X - from.Width / 2; var dy = to.Y + to.Height / 2 - from.Y - from.Height / 2;
        if (first == LeadSide.Auto) first = Math.Abs(dx) >= Math.Abs(dy) ? dx >= 0 ? LeadSide.Right : LeadSide.Left : dy >= 0 ? LeadSide.Bottom : LeadSide.Top;
        if (last == LeadSide.Auto) last = Math.Abs(dx) >= Math.Abs(dy) ? dx >= 0 ? LeadSide.Left : LeadSide.Right : dy >= 0 ? LeadSide.Top : LeadSide.Bottom;
        (double X, double Y) Normal(LeadSide side) => side switch { LeadSide.Top => (0,-1), LeadSide.Right => (1,0), LeadSide.Bottom => (0,1), _ => (-1,0) };
        var a = Port(from, first); var z = Port(to, last); var n = Normal(first); var m = Normal(last);
        var clearance = Math.Clamp(Math.Sqrt(dx * dx + dy * dy) * .35, 50, 220) + Math.Abs(lane) * 14;
        if (n.X != 0 && m.X == -n.X && (z.X-a.X)*n.X > 0) clearance = Math.Min(clearance,Math.Max(12,Math.Abs(z.X-a.X)*.45));
        if (n.Y != 0 && m.Y == -n.Y && (z.Y-a.Y)*n.Y > 0) clearance = Math.Min(clearance,Math.Max(12,Math.Abs(z.Y-a.Y)*.45));
        var b = new LeadPoint(a.X + n.X * clearance, a.Y + n.Y * clearance); var c = new LeadPoint(z.X + m.X * clearance, z.Y + m.Y * clearance);
        if (routing == LeadRouting.Curve)
        {
            var angle = Math.Atan2(-a.Y-b.Y+c.Y+z.Y, -a.X-b.X+c.X+z.X) * 180 / Math.PI; if (angle > 90) angle -= 180; if (angle < -90) angle += 180;
            return new(a.X,a.Y,b.X,b.Y,c.X,c.Y,z.X,z.Y,(a.X+3*b.X+3*c.X+z.X)/8,(a.Y+3*b.Y+3*c.Y+z.Y)/8,angle);
        }
        var points = new List<LeadPoint> { a, b };
        if (n.X != 0 && m.X != 0) { var x = n.X == m.X ? n.X > 0 ? Math.Max(b.X,c.X) : Math.Min(b.X,c.X) : (b.X+c.X)/2; points.Add(new(x,b.Y)); points.Add(new(x,c.Y)); }
        else if (n.Y != 0 && m.Y != 0) { var y = n.Y == m.Y ? n.Y > 0 ? Math.Max(b.Y,c.Y) : Math.Min(b.Y,c.Y) : (b.Y+c.Y)/2; points.Add(new(b.X,y)); points.Add(new(c.X,y)); }
        else points.Add(n.X != 0 ? new(c.X,b.Y) : new(b.X,c.Y));
        points.Add(c); points.Add(z);
        var segments = points.Zip(points.Skip(1)).OrderByDescending(p => Math.Abs(p.First.X-p.Second.X)+Math.Abs(p.First.Y-p.Second.Y)).ToList(); var longest = segments[0];
        var labelAngle = Math.Atan2(longest.Second.Y-longest.First.Y,longest.Second.X-longest.First.X)*180/Math.PI; if (labelAngle > 90) labelAngle -= 180; if (labelAngle < -90) labelAngle += 180;
        return new(a.X,a.Y,b.X,b.Y,c.X,c.Y,z.X,z.Y,(longest.First.X+longest.Second.X)/2,(longest.First.Y+longest.Second.Y)/2,labelAngle) { Bends = points };
    }
    public static LeadPath Route(NodeBox from, NodeBox to, LeadRouting routing, int lane = 0, IReadOnlyCollection<NodeBox>? obstacles = null, LeadSide fromSide = LeadSide.Auto, LeadSide toSide = LeadSide.Auto)
    {
        if (fromSide != LeadSide.Auto || toSide != LeadSide.Auto) return PortRoute(from, to, routing, lane, fromSide, toSide);
        var horizontal = Math.Abs(to.X + to.Width / 2 - from.X - from.Width / 2) >= Math.Abs(to.Y + to.Height / 2 - from.Y - from.Height / 2);
        double x1, y1, x2, y2, a, b, c, d;
        var shift = Math.Clamp(lane * 14, -36, 36);
        if (horizontal)
        {
            var right = to.X + to.Width / 2 >= from.X + from.Width / 2;
            x1 = from.X + (right ? from.Width : 0); y1 = from.Y + from.Height / 2 + shift;
            x2 = to.X + (right ? 0 : to.Width); y2 = to.Y + to.Height / 2 + shift;
            a = c = (x1 + x2) / 2; b = y1; d = y2;
        }
        else
        {
            var down = to.Y + to.Height / 2 >= from.Y + from.Height / 2;
            x1 = from.X + from.Width / 2 + shift; y1 = from.Y + (down ? from.Height : 0);
            x2 = to.X + to.Width / 2 + shift; y2 = to.Y + (down ? 0 : to.Height);
            a = x1; c = x2; b = d = (y1 + y2) / 2;
        }
        // Route a long lead around intervening cells rather than through their text.
        var blocked = (obstacles ?? []).Where(o => o.Id != from.Id && o.Id != to.Id &&
            o.X < Math.Max(x1, x2) + 6 && o.X + o.Width > Math.Min(x1, x2) - 6 &&
            o.Y < Math.Max(y1, y2) + 6 && o.Y + o.Height > Math.Min(y1, y2) - 6).ToList();
        if (blocked.Count > 0)
        {
            if (!horizontal)
            {
                var left = Math.Min(Math.Min(from.X, to.X), blocked.Min(o => o.X)) - 60 - Math.Abs(shift);
                var right = Math.Max(Math.Max(from.X + from.Width, to.X + to.Width), blocked.Max(o => o.X + o.Width)) + 60 + Math.Abs(shift);
                var useLeft = Math.Abs(from.X-left)+Math.Abs(to.X-left) <= Math.Abs(from.X+from.Width-right)+Math.Abs(to.X+to.Width-right);
                x1 = from.X + (useLeft ? 0 : from.Width); x2 = to.X + (useLeft ? 0 : to.Width);
                y1 = from.Y + from.Height / 2 + shift; y2 = to.Y + to.Height / 2 + shift;
                a = c = useLeft ? left : right; b = y1; d = y2;
                if (routing == LeadRouting.Curve)
                {
                    a = c = ((useLeft ? left : right) - .125 * (x1 + x2)) / .75;
                    return new(x1, y1, a, b, c, d, x2, y2, .125 * (x1 + x2) + .75 * a, (y1 + y2) / 2, 90);
                }
            }
            else
            {
            var above = Math.Min(Math.Min(from.Y, to.Y), blocked.Min(o => o.Y)) - 60 - Math.Abs(shift);
            var below = Math.Max(Math.Max(from.Y + from.Height, to.Y + to.Height), blocked.Max(o => o.Y + o.Height)) + 60 + Math.Abs(shift);
            var useAbove = Math.Abs(from.Y-above)+Math.Abs(to.Y-above) <= Math.Abs(from.Y+from.Height-below)+Math.Abs(to.Y+to.Height-below);
            x1 = from.X + from.Width / 2 + shift; x2 = to.X + to.Width / 2 + shift;
            y1 = from.Y + (useAbove ? 0 : from.Height); y2 = to.Y + (useAbove ? 0 : to.Height);
            a = x1; c = x2; b = d = useAbove ? above : below;
            if (routing == LeadRouting.Curve)
            {
                // The Bézier midpoint is only 75% toward its controls. Reserve clearance at the curve itself.
                b = d = ((useAbove ? above : below) - .125 * (y1 + y2)) / .75;
                return new(x1, y1, a, b, c, d, x2, y2, (x1 + x2) / 2, .125 * (y1 + y2) + .75 * b, 0);
            }
            }
        }
        double lx, ly, angle;
        if (routing == LeadRouting.Curve)
        {
            lx = (x1 + 3 * a + 3 * c + x2) / 8; ly = (y1 + 3 * b + 3 * d + y2) / 8;
            angle = Math.Atan2(-y1 - b + d + y2, -x1 - a + c + x2) * 180 / Math.PI;
        }
        else
        {
            var segments = new[] { (x1, y1, a, b), (a, b, c, d), (c, d, x2, y2) };
            var longest = segments.OrderByDescending(s => Math.Abs(s.Item3 - s.Item1) + Math.Abs(s.Item4 - s.Item2)).First();
            lx = (longest.Item1 + longest.Item3) / 2; ly = (longest.Item2 + longest.Item4) / 2;
            angle = Math.Atan2(longest.Item4 - longest.Item2, longest.Item3 - longest.Item1) * 180 / Math.PI;
        }
        if (angle > 90) angle -= 180; if (angle < -90) angle += 180;
        return new(x1, y1, a, b, c, d, x2, y2, lx, ly, angle);
    }
}
