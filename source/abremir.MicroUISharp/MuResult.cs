namespace abremir.MicroUISharp;

[Flags]
public enum MuResult
{
    None = 0,
    Active = 1 << 0,
    Submit = 1 << 1,
    Change = 1 << 2,
}
