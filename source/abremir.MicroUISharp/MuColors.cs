namespace abremir.MicroUISharp;

// ========================================================================
// Style Colors Map Struct
// ========================================================================

public struct MuColors
{
    private MuColor _text, _border, _windowBg, _titleBg, _titleText, _panelBg, _button, _buttonHover, _buttonFocus, _base, _baseHover, _baseFocus, _scrollBase, _scrollThumb;

    public MuColor this[MuColorType index]
    {
        get => index switch
        {
            MuColorType.Text => _text,
            MuColorType.Border => _border,
            MuColorType.WindowBg => _windowBg,
            MuColorType.TitleBg => _titleBg,
            MuColorType.TitleText => _titleText,
            MuColorType.PanelBg => _panelBg,
            MuColorType.Button => _button,
            MuColorType.ButtonHover => _buttonHover,
            MuColorType.ButtonFocus => _buttonFocus,
            MuColorType.Base => _base,
            MuColorType.BaseHover => _baseHover,
            MuColorType.BaseFocus => _baseFocus,
            MuColorType.ScrollBase => _scrollBase,
            MuColorType.ScrollThumb => _scrollThumb,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case MuColorType.Text: _text = value; break;
                case MuColorType.Border: _border = value; break;
                case MuColorType.WindowBg: _windowBg = value; break;
                case MuColorType.TitleBg: _titleBg = value; break;
                case MuColorType.TitleText: _titleText = value; break;
                case MuColorType.PanelBg: _panelBg = value; break;
                case MuColorType.Button: _button = value; break;
                case MuColorType.ButtonHover: _buttonHover = value; break;
                case MuColorType.ButtonFocus: _buttonFocus = value; break;
                case MuColorType.Base: _base = value; break;
                case MuColorType.BaseHover: _baseHover = value; break;
                case MuColorType.BaseFocus: _baseFocus = value; break;
                case MuColorType.ScrollBase: _scrollBase = value; break;
                case MuColorType.ScrollThumb: _scrollThumb = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }
}
