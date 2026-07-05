namespace abremir.MicroUISharp;

public struct MuCommand
{
    public MuCommandType Type;
    public int JumpDstIdx; // Replaces 'dst' pointer mapping
    public MuRect Rect;
    public MuColor Color;
    public object Font;
    public MuVec2 Pos;
    public string Str;     // Reference assignment (no heap copying allocation)
    public MuIcon Icon;
}

