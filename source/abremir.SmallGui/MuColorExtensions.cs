using Avalonia.Media;
using abremir.MicroUISharp;

namespace abremir.SmallGui;

public static class MuColorExtensions
{
    public static Color ToColor(this MuColor muColor)
    {
        return Color.FromArgb(muColor.A, muColor.R, muColor.G, muColor.B);
    }
}
