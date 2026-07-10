namespace abremir.MicroUISharp;

public class MuStyle
{
    public object? Font;
    public MuVec2 Size;
    public int Padding;
    public int Spacing;
    public int Indent;
    public int TitleHeight;
    public int ScrollbarSize;
    public int ThumbSize;
    public MuColors Colors;

    public MuStyle()
    {
        Size = new MuVec2(68, 10);
        Padding = 5;
        Spacing = 4;
        Indent = 24;
        TitleHeight = 24;
        ScrollbarSize = 12;
        ThumbSize = 8;
        Colors = new MuColors();

        Colors[MuColorType.Text] = new MuColor(230, 230, 230, 255);
        Colors[MuColorType.Border] = new MuColor(25, 25, 25, 255);
        Colors[MuColorType.WindowBg] = new MuColor(50, 50, 50, 255);
        Colors[MuColorType.TitleBg] = new MuColor(25, 25, 25, 255);
        Colors[MuColorType.TitleText] = new MuColor(240, 240, 240, 255);
        Colors[MuColorType.PanelBg] = new MuColor(0, 0, 0, 0);
        Colors[MuColorType.Button] = new MuColor(75, 75, 75, 255);
        Colors[MuColorType.ButtonHover] = new MuColor(95, 95, 95, 255);
        Colors[MuColorType.ButtonFocus] = new MuColor(115, 115, 115, 255);
        Colors[MuColorType.Base] = new MuColor(30, 30, 30, 255);
        Colors[MuColorType.BaseHover] = new MuColor(35, 35, 35, 255);
        Colors[MuColorType.BaseFocus] = new MuColor(40, 40, 40, 255);
        Colors[MuColorType.ScrollBase] = new MuColor(43, 43, 43, 255);
        Colors[MuColorType.ScrollThumb] = new MuColor(30, 30, 30, 255);
    }
}
