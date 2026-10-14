namespace EireTodo.Core;

// Layer directed components, use barycentre ordering to reduce crossings, then
// pack disconnected groups. SCCs handle cycles and double-headed relationships.
public static class GraphOrganisation
{
    public static ChartScene Arrange(Diagram chart,Func<ChartNode,int,(double Width,double Height)>? measure=null,double preferredWidth=1400)
    {
        Charts.Validate(chart);if(chart.Kind!=DiagramKind.Network)throw new ArgumentException("Select a graph to organise.");
        if(!double.IsFinite(preferredWidth)||preferredWidth<400||preferredWidth>20000)throw new ArgumentException("Choose an organisation width between 400 and 20,000.");
        if(chart.Nodes.Count==0)return new([],500,300);measure??=ChartGeometry.Measure;
        var nodes=chart.Nodes.OrderBy(n=>n.Order).ThenBy(n=>n.Number).ToList();var index=nodes.ToDictionary(n=>n.Id);
        var edges=chart.Leads.Select(l=>(l.From,l.To)).Concat(chart.Leads.Where(l=>l.DoubleHeaded).Select(l=>(From:l.To,To:l.From))).Distinct().ToList();
        var outgoing=nodes.ToDictionary(n=>n.Id,_=>new List<Guid>());var neighbours=nodes.ToDictionary(n=>n.Id,_=>new HashSet<Guid>());
        foreach(var (from,to) in edges){outgoing[from].Add(to);neighbours[from].Add(to);neighbours[to].Add(from);}
        var visited=new HashSet<Guid>();var blocks=new List<ChartScene>();
        foreach(var seed in nodes)
        {
            if(!visited.Add(seed.Id))continue;var component=new List<Guid>();var queue=new Queue<Guid>();queue.Enqueue(seed.Id);
            while(queue.TryDequeue(out var id)){component.Add(id);foreach(var next in neighbours[id])if(visited.Add(next))queue.Enqueue(next);}
            // Tarjan strongly connected components; <= 1,000 nodes bounds recursion.
            var discovery=new Dictionary<Guid,int>();var low=new Dictionary<Guid,int>();var stack=new Stack<Guid>();var onStack=new HashSet<Guid>();var clusters=new List<List<Guid>>();var sequence=0;
            void Visit(Guid id)
            {
                discovery[id]=low[id]=sequence++;stack.Push(id);onStack.Add(id);
                foreach(var next in outgoing[id]){if(!discovery.ContainsKey(next)){Visit(next);low[id]=Math.Min(low[id],low[next]);}else if(onStack.Contains(next))low[id]=Math.Min(low[id],discovery[next]);}
                if(low[id]!=discovery[id])return;var members=new List<Guid>();Guid item;do{item=stack.Pop();onStack.Remove(item);members.Add(item);}while(item!=id);clusters.Add(members);
            }
            foreach(var id in component)if(!discovery.ContainsKey(id))Visit(id);
            var clusterOf=clusters.SelectMany((list,i)=>list.Select(id=>(id,i))).ToDictionary(p=>p.id,p=>p.i);
            var links=edges.Where(e=>clusterOf.ContainsKey(e.From)&&clusterOf[e.From]!=clusterOf[e.To]).Select(e=>(From:clusterOf[e.From],To:clusterOf[e.To])).Distinct().ToList();
            var ranks=new int[clusters.Count];var indegree=Enumerable.Range(0,clusters.Count).ToDictionary(i=>i,i=>links.Count(e=>e.To==i));var ready=new Queue<int>(indegree.Where(p=>p.Value==0).Select(p=>p.Key));
            while(ready.TryDequeue(out var c))foreach(var edge in links.Where(e=>e.From==c)){ranks[edge.To]=Math.Max(ranks[edge.To],ranks[c]+1);if(--indegree[edge.To]==0)ready.Enqueue(edge.To);}
            var layers=component.GroupBy(id=>ranks[clusterOf[id]]).OrderBy(g=>g.Key).Select(g=>g.OrderBy(id=>index[id].Order).ThenBy(id=>index[id].Number).ToList()).ToList();
            var rankOf=layers.SelectMany((list,i)=>list.Select(id=>(id,i))).ToDictionary(p=>p.id,p=>p.i);
            for(var pass=0;pass<4;pass++)
            {
                var positions=layers.SelectMany(list=>list.Select((id,i)=>(id,i))).ToDictionary(p=>p.id,p=>(double)p.i);
                var order=pass%2==0?Enumerable.Range(0,layers.Count):Enumerable.Range(0,layers.Count).Reverse();
                foreach(var layer in order)
                {
                    double Centre(Guid id){var related=neighbours[id].Where(n=>rankOf[n]!=layer).ToList();return related.Count>0?related.Average(n=>positions[n]):positions[id];}
                    layers[layer]=layers[layer].OrderBy(Centre).ThenBy(id=>index[id].Order).ThenBy(id=>index[id].Number).ToList();for(var i=0;i<layers[layer].Count;i++)positions[layers[layer][i]]=i;
                }
            }
            var sizes=component.ToDictionary(id=>id,id=>NodeShapes.Expand(measure(index[id],2),NodeShapes.Resolve(chart,index[id])));
            if(sizes.Values.Any(s=>!double.IsFinite(s.Width)||!double.IsFinite(s.Height)||s.Width<=0||s.Height<=0))throw new ArgumentException("Node measurements are invalid.");
            var boxes=new List<NodeBox>();double bandY=0,blockWidth=0;var first=0;var band=0;
            while(first<layers.Count)
            {
                var last=first;double span=0;
                while(last<layers.Count){var width=layers[last].Max(id=>sizes[id].Width);if(last>first&&span+width>preferredWidth)break;span+=width+96;last++;}
                span-=96;var height=Enumerable.Range(first,last-first).Max(i=>layers[i].Sum(id=>sizes[id].Height)+36*(layers[i].Count-1));double x=0;
                for(var l=first;l<last;l++)
                {
                    var width=layers[l].Max(id=>sizes[id].Width);var columnHeight=layers[l].Sum(id=>sizes[id].Height)+36*(layers[l].Count-1);var y=bandY+(height-columnHeight)/2;
                    foreach(var id in layers[l]){var size=sizes[id];boxes.Add(new(id,band%2==0?x:span-x-width,y,size.Width,size.Height) {Shape=NodeShapes.Resolve(chart,index[id])});y+=size.Height+36;}x+=width+96;
                }
                blockWidth=Math.Max(blockWidth,span);bandY+=height+110;first=last;band++;
            }
            blocks.Add(new(boxes,blockWidth,bandY-110));
        }
        var packed=new List<NodeBox>();double px=40,py=40,shelfHeight=0;var target=Math.Max(blocks.Max(b=>b.Width),Math.Min(preferredWidth,Math.Sqrt(blocks.Sum(b=>b.Width*b.Height)*1.5)));
        foreach(var block in blocks)
        {
            if(px>40&&px+block.Width>target+40){px=40;py+=shelfHeight+80;shelfHeight=0;}
            packed.AddRange(block.Boxes.Select(b=>b with{X=b.X+px,Y=b.Y+py}));px+=block.Width+80;shelfHeight=Math.Max(shelfHeight,block.Height);
        }
        var maxX=packed.Max(b=>b.X+b.Width)+40;var maxY=packed.Max(b=>b.Y+b.Height)+40;
        var shiftX=packed.Max(b=>b.X)>100000?packed.Max(b=>b.X)/2:0;var shiftY=packed.Max(b=>b.Y)>100000?packed.Max(b=>b.Y)/2:0;
        if(shiftX>0||shiftY>0)packed=packed.Select(b=>b with {X=b.X-shiftX,Y=b.Y-shiftY}).ToList();
        if(packed.Any(b=>Math.Abs(b.X)>100000||Math.Abs(b.Y)>100000))throw new ArgumentException("The graph exceeds the placement area. Reduce large fonts or full-note displays before organising.");
        return new(packed,Math.Max(500,maxX),Math.Max(300,maxY));
    }
    public static void Organise(Diagram chart,Func<ChartNode,int,(double Width,double Height)>? measure=null,double preferredWidth=1400)
    {
        var scene=Arrange(chart,measure,preferredWidth);var positions=scene.Boxes.ToDictionary(b=>b.Id);foreach(var node in chart.Nodes){var b=positions[node.Id];node.X=b.X;node.Y=b.Y;}Charts.Validate(chart);
    }
}
