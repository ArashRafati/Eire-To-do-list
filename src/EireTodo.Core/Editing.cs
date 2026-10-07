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
public enum LeadRouting { Curve, SharpBends }
public sealed class DiagramLead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid From { get; set; }
    public Guid To { get; set; }
    public bool DoubleHeaded { get; set; }
    public LeadRouting Routing { get; set; }
    public string Description { get; set; } = "";
}

public static class NetworkCharts
{
    public const int MaxLeads = 5000;
    public static Diagram Create(string name)
    {
        var chart = new Diagram { Name = name, Kind = DiagramKind.Network, Layout = ChartLayout.Freeform };
        Add(chart, null, false, name); return chart;
    }
    public static ChartNode Add(Diagram chart, Guid? anchor, bool connected, string title = "New node", bool doubleHeaded = false, LeadRouting routing = LeadRouting.Curve, TextFormat? format = null)
    {
        if (chart.Kind != DiagramKind.Network || chart.Nodes.Count >= Charts.MaxNodes) throw new ArgumentException("Cannot add a network node to this diagram.");
        var node = new ChartNode { Number = chart.NextNumber++, Order = chart.Nodes.Count, Title = title, Format = format?.Clone() ?? new() };
        var around = chart.Nodes.FirstOrDefault(n => n.Id == anchor);
        var x = around?.X ?? 40; var y = around?.Y ?? 40;
        var size = ChartGeometry.Measure(node);
        var occupied = chart.Nodes.Select(n => { var s = ChartGeometry.Measure(n); return new NodeBox(n.Id, n.X ?? 40, n.Y ?? 40, s.Width, s.Height); }).ToList();
        bool Free(double px, double py) => occupied.All(b => px + size.Width + 28 <= b.X || b.X + b.Width + 28 <= px || py + size.Height + 28 <= b.Y || b.Y + b.Height + 28 <= py);
        // Search outward from the selected node; reserve a full cell plus a readable gap.
        bool found = false;
        for (var radius = 0; radius <= 64 && !found; radius++)
            for (var dy = -radius; dy <= radius && !found; dy++)
                for (var dx = -radius; dx <= radius && !found; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                    var px = x + dx * (size.Width + 74); var py = y + dy * (size.Height + 74);
                    if (px < 40 || py < 40 || !Free(px, py)) continue;
                    node.X = px; node.Y = py; found = true;
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
        var node = chart.Nodes.Single(n => n.Id == id); node.X = Math.Clamp(x, 40, 100000); node.Y = Math.Clamp(y, 40, 100000); Charts.Validate(chart);
    }
}

public sealed record LeadPath(double X1, double Y1, double C1X, double C1Y, double C2X, double C2Y, double X2, double Y2, double LabelX, double LabelY, double LabelAngle)
{
    public double LabelWidth => Math.Clamp(Math.Sqrt(Math.Pow(C2X - C1X, 2) + Math.Pow(C2Y - C1Y, 2)) > 60 ? Math.Sqrt(Math.Pow(C2X - C1X, 2) + Math.Pow(C2Y - C1Y, 2)) - 20 : Math.Sqrt(Math.Pow(X2 - X1, 2) + Math.Pow(Y2 - Y1, 2)) - 20, 45, 280);
}
public static class LeadGeometry
{
    public static LeadPath Route(NodeBox from, NodeBox to, LeadRouting routing, int lane = 0, IReadOnlyCollection<NodeBox>? obstacles = null)
    {
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
                var useLeft = left >= 20 && (routing != LeadRouting.Curve || (left - .125 * (from.X + to.X)) / .75 >= 20);
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
            var useAbove = above >= 20 && (routing != LeadRouting.Curve || (above - .125 * (from.Y + to.Y)) / .75 >= 20);
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
