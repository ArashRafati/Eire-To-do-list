using System.Buffers.Binary;
namespace EireTodo.Core;

public static class NodeColours
{
    public static bool IsValid(string value) => value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    public static string ReadableText(string fill) => DiagramAppearance.Contrast(BrandTheme.Ink,fill) >= 4.5 ? BrandTheme.Ink : DiagramAppearance.Contrast(BrandTheme.White,fill) >= 4.5 ? BrandTheme.White : BrandTheme.Black;
    public static NodeSurface Apply(NodeSurface surface,string colour)
    {
        if (!IsValid(colour)) throw new ArgumentException("Choose a six-digit node colour.");
        if (surface.Decoration is NodeDecoration.Flat or NodeDecoration.Underline)
            return surface with { Text = DiagramAppearance.Contrast(colour,BrandTheme.Surface) >= 4.5 ? colour : BrandTheme.Ink, Stroke = surface.Decoration == NodeDecoration.Flat ? "" : colour };
        return surface with { Fill=colour, Text=ReadableText(colour), Stroke=colour };
    }
    public static bool IsMulticolour(DiagramPalette palette) => palette is DiagramPalette.MixTealOrange or DiagramPalette.MixRedGrey or DiagramPalette.MixBlueRed or DiagramPalette.MixTealViolet or DiagramPalette.MixForestCopper or DiagramPalette.MixOceanCoral;
    public static void ApplyPackage(Diagram diagram,DiagramPalette palette)
    {
        if(!Enum.IsDefined(palette))throw new ArgumentException("Choose a valid palette.");
        diagram.Palette=palette;
        foreach(var node in diagram.Nodes.Where(n=>n.ColourFromPreset)){node.FillColour=null;node.ColourFromPreset=false;}
        if(diagram.Kind!=DiagramKind.Network||!IsMulticolour(palette))return;
        var pack=DiagramAppearance.Colours(palette);var colours=new[]{pack.Root,pack.Branch,pack.Leaf};var i=0;
        foreach(var node in diagram.Nodes.OrderBy(n=>n.Order).ThenBy(n=>n.Number)){node.FillColour=colours[i++%colours.Length];node.ColourFromPreset=true;}
    }
    public static void Set(Diagram diagram,IEnumerable<Guid> ids,string? colour)
    {
        if(colour is not null && !IsValid(colour)) throw new ArgumentException("Choose a valid node colour.");
        var selected=ids.ToHashSet(); if(selected.Count==0 || selected.Any(id=>!diagram.Nodes.Any(n=>n.Id==id))) throw new ArgumentException("Select nodes to colour.");
        foreach(var node in diagram.Nodes.Where(n=>selected.Contains(n.Id))) {node.FillColour=colour;node.ColourFromPreset=false;}
    }
}

public static class OrganisationBranding
{
    public static void Validate(Diagram diagram)
    {
        LogoAsset.Validate(diagram.OrganisationLogoPng);
        if(!double.IsFinite(diagram.OrganisationLogoWidth) || diagram.OrganisationLogoWidth<40 || diagram.OrganisationLogoWidth>300 || !double.IsFinite(diagram.ExportLogoWidth) || diagram.ExportLogoWidth<30 || diagram.ExportLogoWidth>180) throw new ArgumentException("Logo width must be 40–300 screen pixels or 30–180 PDF points.");
    }
    public static (double Width,double Height) Fit(byte[] png,double width,double maxHeight)
    {
        LogoAsset.Validate(png); if(png.Length==0) return (0,0);
        if(!double.IsFinite(width) || width<=0 || !double.IsFinite(maxHeight) || maxHeight<=0) throw new ArgumentException("Choose a valid logo size.");
        var w=BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16,4)); var h=BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20,4));
        var scale=Math.Min(width/w,maxHeight/h); return (w*scale,h*scale);
    }
}
