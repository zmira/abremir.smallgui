using System.Collections.Frozen;
using abremir.MicroUISharp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace abremir.SmallGui;

internal class MicroUiControl : Control
{
    private readonly MuContext _mu = new MuContext();
    private readonly Action<MuContext> _layoutBuilder;
    private readonly DispatcherTimer _timer;
    private readonly Stack<DrawingContext.PushedState> _clipStates = new Stack<DrawingContext.PushedState>();

    // A cache for FormattedText objects to improve performance by avoiding re-creation.
    private readonly Dictionary<(string, Typeface), FormattedText> _textCache = [];

    // A cache for parsed Geometry objects to prevent garbage collection pressure
    private readonly Dictionary<MuIcon, Geometry> _geometryCache = [];

    private static readonly FrozenDictionary<MuIcon, string> SvgIconPaths = new Dictionary<MuIcon, string>()
    {
        // https://github.com/microsoft/fluentui-system-icons/blob/main/assets/Dismiss/SVG/ic_fluent_dismiss_12_regular.svg
        { MuIcon.Close, "M2.08859 2.21569L2.14645 2.14645C2.32001 1.97288 2.58944 1.9536 2.78431 2.08859L2.85355 2.14645L6 5.293L9.14645 2.14645C9.34171 1.95118 9.65829 1.95118 9.85355 2.14645C10.0488 2.34171 10.0488 2.65829 9.85355 2.85355L6.707 6L9.85355 9.14645C10.0271 9.32001 10.0464 9.58944 9.91141 9.78431L9.85355 9.85355C9.67999 10.0271 9.41056 10.0464 9.21569 9.91141L9.14645 9.85355L6 6.707L2.85355 9.85355C2.65829 10.0488 2.34171 10.0488 2.14645 9.85355C1.95118 9.65829 1.95118 9.34171 2.14645 9.14645L5.293 6L2.14645 2.85355C1.97288 2.67999 1.9536 2.41056 2.08859 2.21569L2.14645 2.14645L2.08859 2.21569Z" },
        // https://github.com/microsoft/fluentui-system-icons/blob/main/assets/Checkmark/SVG/ic_fluent_checkmark_12_regular.svg
        { MuIcon.Check, "M9.85355 3.14645C10.0488 3.34171 10.0488 3.65829 9.85355 3.85355L5.35355 8.35355C5.15829 8.54882 4.84171 8.54882 4.64645 8.35355L2.64645 6.35355C2.45118 6.15829 2.45118 5.84171 2.64645 5.64645C2.84171 5.45118 3.15829 5.45118 3.35355 5.64645L5 7.29289L9.14645 3.14645C9.34171 2.95118 9.65829 2.95118 9.85355 3.14645Z" },
        // https://github.com/microsoft/fluentui-system-icons/blob/main/assets/Chevron%20Right/SVG/ic_fluent_chevron_right_12_regular.svg
        { MuIcon.Collapsed, "M4.64645 2.14645C4.45118 2.34171 4.45118 2.65829 4.64645 2.85355L7.79289 6L4.64645 9.14645C4.45118 9.34171 4.45118 9.65829 4.64645 9.85355C4.84171 10.0488 5.15829 10.0488 5.35355 9.85355L8.85355 6.35355C9.04882 6.15829 9.04882 5.84171 8.85355 5.64645L5.35355 2.14645C5.15829 1.95118 4.84171 1.95118 4.64645 2.14645Z" },
        // https://github.com/microsoft/fluentui-system-icons/blob/main/assets/Chevron%20Down/SVG/ic_fluent_chevron_down_12_regular.svg
        { MuIcon.Expanded, "M2.14645 4.64645C2.34171 4.45118 2.65829 4.45118 2.85355 4.64645L6 7.79289L9.14645 4.64645C9.34171 4.45118 9.65829 4.45118 9.85355 4.64645C10.0488 4.84171 10.0488 5.15829 9.85355 5.35355L6.35355 8.85355C6.15829 9.04882 5.84171 9.04882 5.64645 8.85355L2.14645 5.35355C1.95118 5.15829 1.95118 4.84171 2.14645 4.64645Z" }
    }.ToFrozenDictionary();

    public MicroUiControl(Action<MuContext> layoutBuilder, MuStyle? style = null)
    {
        _layoutBuilder = layoutBuilder ?? throw new ArgumentNullException(nameof(layoutBuilder));
        _mu.Style = style ?? new();
        _mu.Style.Font ??= new Typeface(FontFamily.Default.Name);

        ClipToBounds = true;
        Focusable = true; // Required to receive keyboard events.
        IsTabStop = true; // Allow tab navigation to this control.

        // 1. Set up Text Measurement callbacks using Avalonia's FormattedText.
        _mu.TextWidth = (font, str, len) => (int)GetFormattedText((Typeface)font, str, new SolidColorBrush(Colors.White)).Width;
        _mu.TextHeight = (font) => (int)GetFormattedText((Typeface)font, " ", new SolidColorBrush(Colors.White)).Height; // Height of a space character.

        // 2. Set up a "Game Loop" for continuous UI updates.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1 / 60.0) };
        _timer.Tick += (sender, e) =>
        {
            // Build the command list for the current frame.
            _mu.Begin();
            _layoutBuilder(_mu);
            _mu.End();

            // Schedule a repaint of the control.
            InvalidateVisual();
        };
        _timer.Start();
    }

    // 3. Override Render to draw the MicroUI command list.
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        ClearClips(); // Ensure a clean clip slate on frame start

        int cmdIdx = -1;
        while (_mu.NextCommand(ref cmdIdx, out MuCommand cmd))
        {
            switch (cmd.Type)
            {
                case MuCommandType.Rect:
                    var rectBrush = new SolidColorBrush(cmd.Color.ToColor());
                    context.DrawRectangle(rectBrush, null, new Rect(cmd.Rect.X, cmd.Rect.Y, cmd.Rect.W, cmd.Rect.H));
                    break;

                case MuCommandType.Text:
                    var textBrush = new SolidColorBrush(cmd.Color.ToColor());
                    var formattedText = GetFormattedText((Typeface)_mu.Style.Font!, cmd.Str, textBrush);
                    context.DrawText(formattedText, new Point(cmd.Pos.X, cmd.Pos.Y));
                    break;

                case MuCommandType.Icon:
                    var iconBrush = new SolidColorBrush(cmd.Color.ToColor());
                    RenderIcon(context, cmd.Icon, new Rect(cmd.Rect.X, cmd.Rect.Y, cmd.Rect.W, cmd.Rect.H), iconBrush);
                    break;

                case MuCommandType.Clip:
                    if (_clipStates.Count > 0)
                    {
                        _clipStates.Pop().Dispose();
                    }
                    var clipState = context.PushClip(new Rect(cmd.Rect.X, cmd.Rect.Y, cmd.Rect.W, cmd.Rect.H));
                    _clipStates.Push(clipState);
                    break;
            }
        }

        ClearClips(); // Always clean up lingering graphics context transforms
    }

    private void ClearClips()
    {
        while (_clipStates.Count > 0)
        {
            _clipStates.Pop().Dispose();
        }
    }

    // 4. Wire up Avalonia's input system to MicroUI.
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _mu.InputMouseMove((int)p.X, (int)p.Y);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(NavigationMethod.Pointer); // Ensure the control has focus to receive keyboard input.
        var p = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        MuMouse btn = MuMouse.None;
        if (properties.IsLeftButtonPressed)
        {
            btn |= MuMouse.Left;
        }
        if (properties.IsRightButtonPressed)
        {
            btn |= MuMouse.Right;
        }
        if (properties.IsMiddleButtonPressed)
        {
            btn |= MuMouse.Middle;
        }
        _mu.InputMouseDown((int)p.X, (int)p.Y, btn);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);
        MuMouse btn = MuMouse.None;
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            btn |= MuMouse.Left;
        }
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            btn |= MuMouse.Right;
        }
        if (e.InitialPressMouseButton == MouseButton.Middle)
        {
            btn |= MuMouse.Middle;
        }
        _mu.InputMouseUp((int)p.X, (int)p.Y, btn);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // Avalonia's Y-scroll is often inverted compared to other systems.
        _mu.InputScroll((int)e.Delta.X, (int)(-e.Delta.Y * _mu.Style.ScrollbarSize));
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _mu.InputTextString(e.Text ?? string.Empty);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _mu.InputKeyDown(MapKey(e.Key, e.KeyModifiers));
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _mu.InputKeyUp(MapKey(e.Key, e.KeyModifiers));
        e.Handled = true;
    }

    // 5. Helper methods for text, icons, and input mapping.
    private FormattedText GetFormattedText(Typeface font, string text, IBrush colour)
    {
        if (string.IsNullOrEmpty(text))
        {
            text = " ";
        }
        if (_textCache.TryGetValue((text, font), out var formattedText))
        {
            return formattedText;
        }

        formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            font,
            11,
            colour
        );
        _textCache[(text, font)] = formattedText;

        return formattedText;
    }

    private void RenderIcon(DrawingContext context, MuIcon icon, Rect rect, IBrush brush)
    {
        if (!SvgIconPaths.TryGetValue(icon, out var pathData))
        {
            return; // Icon ID not mapped
        }

        // 2. Parse and cache the geometry on demand
        if (!_geometryCache.TryGetValue(icon, out var geometry))
        {
            geometry = StreamGeometry.Parse(pathData);
            _geometryCache[icon] = geometry;
        }

        // Deflate the bounding box to give icons a nice default internal padding
        var targetBounds = rect.Deflate(4);
        var sourceBounds = geometry.Bounds;

        if (sourceBounds.Width > 0 && sourceBounds.Height > 0)
        {
            // 3. Compute scale and translation to fit the icon inside the MicroUI widget boundaries
            double scaleX = targetBounds.Width / sourceBounds.Width;
            double scaleY = targetBounds.Height / sourceBounds.Height;
            double scale = Math.Min(scaleX, scaleY); // Keep aspect ratio

            // Centering math
            double destX = targetBounds.X + (targetBounds.Width - (sourceBounds.Width * scale)) / 2;
            double destY = targetBounds.Y + (targetBounds.Height - (sourceBounds.Height * scale)) / 2;

            // Apply a safe matrix transform to context, draw, then pop state
            var transformMatrix = Matrix.CreateTranslation(-sourceBounds.X, -sourceBounds.Y)
                                 * Matrix.CreateScale(scale, scale)
                                 * Matrix.CreateTranslation(destX, destY);

            using (context.PushTransform(transformMatrix))
            {
                // Use brush to fill the SVG geometry shape
                context.DrawGeometry(brush, null, geometry);
            }
        }
    }

    private static MuKey MapKey(Key key, KeyModifiers modifiers)
    {
        MuKey res = MuKey.None;
        if ((modifiers & KeyModifiers.Shift) != KeyModifiers.None) res |= MuKey.Shift;
        if ((modifiers & KeyModifiers.Control) != KeyModifiers.None) res |= MuKey.Ctrl;
        if ((modifiers & KeyModifiers.Alt) != KeyModifiers.None) res |= MuKey.Alt;

        switch (key)
        {
            case Key.Back: res |= MuKey.Backspace; break;
            case Key.Enter: res |= MuKey.Return; break;
        }

        return res;
    }
}
