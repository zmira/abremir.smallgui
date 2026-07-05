namespace abremir.MicroUISharp;

public struct MuContainer
{
    public int HeadIdx; // Index replacement of 'head' pointer
    public int TailIdx; // Index replacement of 'tail' pointer
    public MuRect Rect;
    public MuRect Body;
    public MuVec2 ContentSize;
    public MuVec2 Scroll;
    public int ZIndex;
    public bool Open;
}

