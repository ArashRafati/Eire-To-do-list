namespace EireTodo.Core;
public enum RegisterPalette { Sumapp, Blueprint, Sage, Charcoal, Amber, Plum, Coral, Slate }
public enum RegisterLayout { Comfortable, Compact, Bordered, Minimal }
public enum RibbonDisplayMode { Pinned, Compact, Automatic }
public sealed record RegisterColours(string Name,string Background,string Row,string Alternate,string Text,string Header,string HeaderText,string Selected,string Line,string Accent,string Error);
public static class RegisterAppearance
{
    public static RegisterColours Colours(RegisterPalette choice)=>choice switch
    {
        RegisterPalette.Blueprint=>new("Blueprint","#F8FAFC","#FFFFFF","#EFF6FF","#1E293B","#DBEAFE","#1E3A8A","#BFDBFE","#CBD5E1","#2563EB","#B42318"),
        RegisterPalette.Sage=>new("Sage","#F7FAF8","#FFFFFF","#F0FDF4","#1F2937","#DCFCE7","#14532D","#BBF7D0","#D1D5DB","#15803D","#B42318"),
        RegisterPalette.Charcoal=>new("Charcoal","#111827","#1F2937","#273449","#F8FAFC","#374151","#FFFFFF","#475569","#64748B","#67E8F9","#FFE4E6"),
        RegisterPalette.Amber=>new("Amber","#FFFBEB","#FFFFFF","#FFF7ED","#292524","#FEF3C7","#78350F","#FDE68A","#D6D3D1","#B45309","#B42318"),
        RegisterPalette.Plum=>new("Plum","#FAF5FF","#FFFFFF","#F5F3FF","#292134","#EDE9FE","#581C87","#DDD6FE","#D8D3DF","#7C3AED","#B42318"),
        RegisterPalette.Coral=>new("Coral","#FFF7F7","#FFFFFF","#FFF1F2","#30232A","#FFE4E6","#9F1239","#FECDD3","#E7D5D9","#BE123C","#B42318"),
        RegisterPalette.Slate=>new("Slate","#F8FAFC","#FFFFFF","#F1F5F9","#1E293B","#E2E8F0","#334155","#CBD5E1","#CBD5E1","#475569","#991B1B"),
        _=>new("SUMAPP","#F6F8F9","#FFFFFF","#E6F4F3","#222B34","#E6F4F3","#095F5E","#C5E8E6","#D6E4E3","#039690","#B42318")
    };
    public static double RowMinimum(RegisterLayout layout)=>layout==RegisterLayout.Compact?46:58;
}
