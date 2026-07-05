namespace abremir.MicroUISharp;

[Flags]
public enum MuMouse
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
}
