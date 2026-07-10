using System.Runtime.CompilerServices;

namespace abremir.MicroUISharp;

public class MuContext
{
    // ========================================================================
    // Stack Sizes and Sizing Configurations
    // ========================================================================

    public const int CommandListSize = 256 * 1024;
    public const int RootListSize = 32;
    public const int ContainerStackSize = 32;
    public const int ClipStackSize = 32;
    public const int IdStackSize = 32;
    public const int LayoutStackSize = 16;
    public const int ContainerPoolSize = 48;
    public const int TreeNodePoolSize = 48;
    public const int MaxWidths = 16;

    public const uint HashInitial = 2166136261;
    public static readonly MuRect UnclippedRect = new(0, 0, 0x1000000, 0x1000000);

    // Callbacks (Delegates)
    public Func<object, string, int, int> TextWidth;
    public Func<object, int> TextHeight;
    public Action<MuContext, MuRect, MuColorType> DrawFrame;

    private static int DefaultTextWidth(object font, string text, int start) => text.Length * 8;
    private static int DefaultTextHeight(object font) => 16;

    // Core UI Styles
    public MuStyle Style = new();

    // Core Tracking States
    public uint Hover;
    public uint Focus;
    public uint LastId;
    public MuRect LastRect;
    public int LastZIndex;
    public bool UpdatedFocus;
    public int Frame;

    // Pool index locations replacing C structures
    public int HoverRootIdx = -1;
    public int NextHoverRootIdx = -1;
    public int ScrollTargetIdx = -1;

    // Text Editing State Variables
    public string NumberEditBuf = "";
    public uint NumberEdit;

    // Stack Storage Containers
    public readonly MuStack<MuCommand> CommandList = new(CommandListSize);
    public readonly MuStack<int> RootList = new(RootListSize);
    public readonly MuStack<int> ContainerStack = new(ContainerStackSize);
    public readonly MuStack<MuRect> ClipStack = new(ClipStackSize);
    public readonly MuStack<uint> IdStack = new(IdStackSize);
    public readonly MuStack<MuLayout> LayoutStack = new(LayoutStackSize);

    // Retained State Pools
    public readonly MuPoolItem[] ContainerPool = new MuPoolItem[ContainerPoolSize];
    public readonly MuContainer[] Containers = new MuContainer[ContainerPoolSize];
    public readonly MuPoolItem[] TreeNodePool = new MuPoolItem[TreeNodePoolSize];

    // Core Input Tracking States
    public MuVec2 MousePos;
    public MuVec2 LastMousePos;
    public MuVec2 MouseDelta;
    public MuVec2 ScrollDelta;
    public MuMouse MouseDown;
    public MuMouse MousePressed;
    public MuKey KeyDown;
    public MuKey KeyPressed;
    public string InputText = "";

    // ========================================================================
    // Setup / Initializers
    // ========================================================================

    public MuContext()
    {
        TextWidth = DefaultTextWidth;
        TextHeight = DefaultTextHeight;
        DrawFrame = DefaultDrawFrame;
    }

    private static void DefaultDrawFrame(MuContext ctx, MuRect rect, MuColorType colorType)
    {
        ctx.DrawRect(rect, ctx.Style.Colors[colorType]);
        if (colorType == MuColorType.ScrollBase || colorType == MuColorType.ScrollThumb || colorType == MuColorType.TitleBg)
        {
            return;
        }

        if (ctx.Style.Colors[MuColorType.Border].A > 0)
        {
            ctx.DrawBox(ExpandRect(rect, 1), ctx.Style.Colors[MuColorType.Border]);
        }
    }

    // ========================================================================
    // Helpers (Math & Geometry)
    // ========================================================================

    public static MuRect ExpandRect(MuRect rect, int n) =>
        new MuRect(rect.X - n, rect.Y - n, rect.W + n * 2, rect.H + n * 2);

    public static MuRect IntersectRects(MuRect r1, MuRect r2)
    {
        int x1 = int.Max(r1.X, r2.X);
        int y1 = int.Max(r1.Y, r2.Y);
        int x2 = int.Min(r1.X + r1.W, r2.X + r2.W);
        int y2 = int.Min(r1.Y + r1.H, r2.Y + r2.H);
        if (x2 < x1) x2 = x1;
        if (y2 < y1) y2 = y1;

        return new MuRect(x1, y1, x2 - x1, y2 - y1);
    }

    public static bool RectOverlapsVec2(MuRect r, MuVec2 p) =>
        p.X >= r.X && p.X < r.X + r.W && p.Y >= r.Y && p.Y < r.Y + r.H;

    // ========================================================================
    // Core Control Frame Transitions (Begin / End)
    // ========================================================================

    public void Begin()
    {
        if (TextWidth == null || TextHeight == null)
        {
            throw new InvalidOperationException("MicroUISharp Callback delegates (TextWidth, TextHeight) are unassigned.");
        }

        CommandList.Clear();
        RootList.Clear();
        ScrollTargetIdx = -1;
        HoverRootIdx = NextHoverRootIdx;
        NextHoverRootIdx = -1;
        MouseDelta.X = MousePos.X - LastMousePos.X;
        MouseDelta.Y = MousePos.Y - LastMousePos.Y;
        Frame++;
    }

    public void End()
    {
        if (ContainerStack.Idx != 0)
        {
            throw new InvalidOperationException("Mismatched window push/pop states.");
        }
        if (ClipStack.Idx != 0)
        {
            throw new InvalidOperationException("Mismatched clipping push/pop states.");
        }
        if (IdStack.Idx != 0)
        {
            throw new InvalidOperationException("Mismatched ID stack push/pop states.");
        }
        if (LayoutStack.Idx != 0)
        {
            throw new InvalidOperationException("Mismatched Layout stack push/pop states.");
        }

        if (ScrollTargetIdx != -1)
        {
            Containers[ScrollTargetIdx].Scroll.X += ScrollDelta.X;
            Containers[ScrollTargetIdx].Scroll.Y += ScrollDelta.Y;
        }

        if (!UpdatedFocus)
        {
            Focus = 0;
        }
        UpdatedFocus = false;

        if (MousePressed != MuMouse.None && NextHoverRootIdx != -1 &&
            Containers[NextHoverRootIdx].ZIndex < LastZIndex &&
            Containers[NextHoverRootIdx].ZIndex >= 0)
        {
            BringToFront(NextHoverRootIdx);
        }

        // Input Cycle State Resets
        KeyPressed = MuKey.None;
        InputText = string.Empty;
        MousePressed = MuMouse.None;
        ScrollDelta = new MuVec2(0, 0);
        LastMousePos = MousePos;

        int n = RootList.Idx;
        // Sorting root list elements using precalculated container Z-Indices
        Array.Sort(RootList.Items, 0, n, Comparer<int>.Create((a, b) => Containers[a].ZIndex.CompareTo(Containers[b].ZIndex)));

        // Linking dynamic Jump Commands via indices
        for (int i = 0; i < n; i++)
        {
            int cntIdx = RootList.Items[i];
            if (i == 0)
            {
                MuCommand firstCmd = CommandList.Items[0];
                firstCmd.JumpDstIdx = Containers[cntIdx].HeadIdx + 1;
                CommandList.Items[0] = firstCmd;
            }
            else
            {
                int prevIdx = RootList.Items[i - 1];
                int prevTailIdx = Containers[prevIdx].TailIdx;
                MuCommand prevTailCmd = CommandList.Items[prevTailIdx];
                prevTailCmd.JumpDstIdx = Containers[cntIdx].HeadIdx + 1;
                CommandList.Items[prevTailIdx] = prevTailCmd;
            }

            if (i == n - 1)
            {
                int tailIdx = Containers[cntIdx].TailIdx;
                MuCommand tailCmd = CommandList.Items[tailIdx];
                tailCmd.JumpDstIdx = CommandList.Idx;
                CommandList.Items[tailIdx] = tailCmd;
            }
        }
    }

    // ========================================================================
    // Hashing & Unique ID Generation
    // ========================================================================

    private static uint HashFnv1A(uint hashVal, string str)
    {
        foreach (char c in str)
        {
            hashVal = (hashVal ^ (byte)(c & 0xFF)) * 16777619;
            hashVal = (hashVal ^ (byte)((c >> 8) & 0xFF)) * 16777619;
        }

        return hashVal;
    }

    private static uint HashFnv1A(uint hashVal, int val)
    {
        hashVal = (hashVal ^ (byte)(val & 0xFF)) * 16777619;
        hashVal = (hashVal ^ (byte)((val >> 8) & 0xFF)) * 16777619;
        hashVal = (hashVal ^ (byte)((val >> 16) & 0xFF)) * 16777619;
        hashVal = (hashVal ^ (byte)((val >> 24) & 0xFF)) * 16777619;

        return hashVal;
    }

    public uint GetId(string str)
    {
        int idx = IdStack.Idx;
        uint res = (idx > 0) ? IdStack.Items[idx - 1] : HashInitial;
        res = HashFnv1A(res, str);
        LastId = res;

        return res;
    }

    public uint GetId(int val)
    {
        int idx = IdStack.Idx;
        uint res = (idx > 0) ? IdStack.Items[idx - 1] : HashInitial;
        res = HashFnv1A(res, val);
        LastId = res;

        return res;
    }

    public uint GetId(object obj)
    {
        int idx = IdStack.Idx;
        uint res = (idx > 0) ? IdStack.Items[idx - 1] : HashInitial;
        res = HashFnv1A(res, obj.GetHashCode());
        LastId = res;

        return res;
    }

    public void PushId(string str) =>
        IdStack.Push(GetId(str));

    public void PushId(int val) =>
        IdStack.Push(GetId(val));

    public void PushId(object obj) =>
        IdStack.Push(GetId(obj));

    public void PopId() =>
        IdStack.Pop();

    // ========================================================================
    // Clipping State Controls
    // ========================================================================

    public void PushClipRect(MuRect rect)
    {
        MuRect last = GetClipRect();
        ClipStack.Push(IntersectRects(rect, last));
    }

    public void PopClipRect() =>
        ClipStack.Pop();

    public MuRect GetClipRect()
    {
        if (ClipStack.Idx <= 0)
        {
            throw new InvalidOperationException("Clip stack is empty.");
        }

        return ClipStack.Items[ClipStack.Idx - 1];
    }

    public MuClip CheckClip(MuRect r)
    {
        MuRect cr = GetClipRect();
        if (r.X > cr.X + cr.W || r.X + r.W < cr.X || r.Y > cr.Y + cr.H || r.Y + r.H < cr.Y)
        {
            return MuClip.All;
        }
        if (r.X >= cr.X && r.X + r.W <= cr.X + cr.W && r.Y >= cr.Y && r.Y + r.H <= cr.Y + cr.H)
        {
            return 0;
        }

        return MuClip.Part;
    }

    // ========================================================================
    // Pool Management Helpers
    // ========================================================================

    public int PoolInit(MuPoolItem[] items, int len, uint id)
    {
        int n = -1;
        int f = Frame;
        for (int i = 0; i < len; i++)
        {
            if (items[i].LastUpdate < f)
            {
                f = items[i].LastUpdate;
                n = i;
            }
        }
        if (n == -1)
        {
            throw new InvalidOperationException("State cache pool exceeded.");
        }
        items[n].Id = id;
        PoolUpdate(items, n);

        return n;
    }

    public int PoolGet(MuPoolItem[] items, int len, uint id)
    {
        for (int i = 0; i < len; i++)
        {
            if (items[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public void PoolUpdate(MuPoolItem[] items, int idx) =>
        items[idx].LastUpdate = Frame;

    // ========================================================================
    // Layout Calculations
    // ========================================================================

    private void PushLayout(MuRect body, MuVec2 scroll)
    {
        MuLayout layout = new();
        layout.Widths = new MuWidths();
        layout.Body = new MuRect(body.X - scroll.X, body.Y - scroll.Y, body.W, body.H);
        layout.Max = new MuVec2(-0x1000000, -0x1000000);
        LayoutStack.Push(layout);
        LayoutRow(1, null, 0);
    }

    private ref MuLayout GetLayout()
    {
        if (LayoutStack.Idx <= 0)
        {
            throw new InvalidOperationException("Layout stack underflow.");
        }

        return ref LayoutStack.Items[LayoutStack.Idx - 1];
    }

    private void PopContainer()
    {
        int cntIdx = GetCurrentContainerIdx();
        ref MuLayout layout = ref GetLayout();
        Containers[cntIdx].ContentSize.X = layout.Max.X - layout.Body.X;
        Containers[cntIdx].ContentSize.Y = layout.Max.Y - layout.Body.Y;

        ContainerStack.Pop();
        LayoutStack.Pop();
        PopId();
    }

    public int GetCurrentContainerIdx()
    {
        if (ContainerStack.Idx <= 0)
        {
            throw new InvalidOperationException("Container context stack is empty.");
        }

        return ContainerStack.Items[ContainerStack.Idx - 1];
    }

    public ref MuContainer GetCurrentContainer() =>
        ref Containers[GetCurrentContainerIdx()];

    private int GetContainerIdx(uint id, MuOption opt)
    {
        int idx = PoolGet(ContainerPool, ContainerPoolSize, id);
        if (idx >= 0)
        {
            if (Containers[idx].Open || (opt & MuOption.Closed) == MuOption.None)
            {
                PoolUpdate(ContainerPool, idx);
            }

            return idx;
        }
        if ((opt & MuOption.Closed) != MuOption.None)
        {
            return -1;
        }

        idx = PoolInit(ContainerPool, ContainerPoolSize, id);
        Containers[idx] = new MuContainer
        {
            Open = true,
            HeadIdx = -1,
            TailIdx = -1
        };
        BringToFront(idx);

        return idx;
    }

    public int GetContainerIdx(string name) =>
        GetContainerIdx(GetId(name), MuOption.None);

    public void BringToFront(int cntIdx) =>
        Containers[cntIdx].ZIndex = ++LastZIndex;

    // ========================================================================
    // Layout Execution Pipeline
    // ========================================================================

    public void LayoutRow(int items, int[]? widths, int height)
    {
        ref MuLayout layout = ref GetLayout();
        if (widths != null)
        {
            if (items > MaxWidths)
            {
                throw new ArgumentOutOfRangeException(nameof(items), "Row layout items limit exceeded.");
            }
            layout.Widths.CopyFrom(widths, items);
        }
        layout.Items = items;
        layout.Position = new MuVec2(layout.Indent, layout.NextRow);
        layout.Size.Y = height;
        layout.ItemIndex = 0;
    }

    public void LayoutWidth(int width) =>
        GetLayout().Size.X = width;

    public void LayoutHeight(int height) =>
        GetLayout().Size.Y = height;

    public void LayoutSetNext(MuRect r, bool relative)
    {
        ref MuLayout layout = ref GetLayout();
        layout.Next = r;
        layout.NextType = relative ? 1 : 2;
    }

    public MuRect LayoutNext()
    {
        ref MuLayout layout = ref GetLayout();
        MuStyle style = Style;
        MuRect res = new();

        if (layout.NextType != 0)
        {
            int type = layout.NextType;
            layout.NextType = 0;
            res = layout.Next;
            if (type == 2) // ABSOLUTE
            {
                LastRect = res;
                return res;
            }
        }
        else
        {
            if (layout.ItemIndex == layout.Items)
            {
                LayoutRow(layout.Items, null, layout.Size.Y);
            }
            res.X = layout.Position.X;
            res.Y = layout.Position.Y;
            res.W = layout.Items > 0 ? layout.Widths[layout.ItemIndex] : layout.Size.X;
            res.H = layout.Size.Y;

            if (res.W == 0)
            {
                res.W = style.Size.X + style.Padding * 2;
            }
            if (res.H == 0)
            {
                res.H = style.Size.Y + style.Padding * 2;
            }
            if (res.W < 0)
            {
                res.W += layout.Body.W - res.X + 1;
            }
            if (res.H < 0)
            {
                res.H += layout.Body.H - res.Y + 1;
            }

            layout.ItemIndex++;
        }

        layout.Position.X += res.W + style.Spacing;
        layout.NextRow = int.Max(layout.NextRow, res.Y + res.H + style.Spacing);

        res.X += layout.Body.X;
        res.Y += layout.Body.Y;

        layout.Max.X = int.Max(layout.Max.X, res.X + res.W);
        layout.Max.Y = int.Max(layout.Max.Y, res.Y + res.H);

        LastRect = res;

        return res;
    }

    public void LayoutBeginColumn() =>
        PushLayout(LayoutNext(), new MuVec2(0, 0));

    public void LayoutEndColumn()
    {
        MuLayout b = LayoutStack.Pop();
        ref MuLayout a = ref GetLayout();

        a.Position.X = int.Max(a.Position.X, b.Position.X + b.Body.X - a.Body.X);
        a.NextRow = int.Max(a.NextRow, b.NextRow + b.Body.Y - a.Body.Y);
        a.Max.X = int.Max(a.Max.X, b.Max.X);
        a.Max.Y = int.Max(a.Max.Y, b.Max.Y);
    }

    // ========================================================================
    // Input Receivers
    // ========================================================================

    public void InputMouseMove(int x, int y) =>
        MousePos = new MuVec2(x, y);

    public void InputMouseDown(int x, int y, MuMouse btn)
    {
        InputMouseMove(x, y);
        MouseDown |= btn;
        MousePressed |= btn;
    }

    public void InputMouseUp(int x, int y, MuMouse btn)
    {
        InputMouseMove(x, y);
        MouseDown &= ~btn;
    }

    public void InputScroll(int x, int y)
    {
        ScrollDelta.X += x;
        ScrollDelta.Y += y;
    }

    public void InputKeyDown(MuKey key)
    {
        KeyPressed |= key;
        KeyDown |= key;
    }

    public void InputKeyUp(MuKey key) =>
        KeyDown &= ~key;

    public void InputTextString(string text) =>
        InputText += text;

    // ========================================================================
    // Command Queue Pushing
    // ========================================================================

    public int PushCommand(MuCommandType type)
    {
        int idx = CommandList.Idx;
        MuCommand cmd = new() { Type = type };
        CommandList.Push(cmd);

        return idx;
    }

    public void SetClip(MuRect rect)
    {
        int idx = PushCommand(MuCommandType.Clip);
        CommandList.Items[idx].Rect = rect;
    }

    public void DrawRect(MuRect rect, MuColor color)
    {
        rect = IntersectRects(rect, GetClipRect());
        if (rect.W > 0 && rect.H > 0)
        {
            int idx = PushCommand(MuCommandType.Rect);
            CommandList.Items[idx].Rect = rect;
            CommandList.Items[idx].Color = color;
        }
    }

    public void DrawBox(MuRect rect, MuColor color)
    {
        DrawRect(new MuRect(rect.X + 1, rect.Y, rect.W - 2, 1), color);
        DrawRect(new MuRect(rect.X + 1, rect.Y + rect.H - 1, rect.W - 2, 1), color);
        DrawRect(new MuRect(rect.X, rect.Y, 1, rect.H), color);
        DrawRect(new MuRect(rect.X + rect.W - 1, rect.Y, 1, rect.H), color);
    }

    public void DrawText(object font, string str, int len, MuVec2 pos, MuColor color)
    {
        if (len < 0)
        {
            len = str.Length;
        }
        string subStr = str.Substring(0, len);
        MuRect rect = new(pos.X, pos.Y, TextWidth(font, subStr, len), TextHeight(font));
        MuClip clipped = CheckClip(rect);
        if (clipped == MuClip.All)
        {
            return;
        }

        if (clipped == MuClip.Part)
        {
            SetClip(GetClipRect());
        }

        int idx = PushCommand(MuCommandType.Text);
        ref MuCommand cmd = ref CommandList.Items[idx];
        cmd.Font = font;
        cmd.Pos = pos;
        cmd.Color = color;
        cmd.Str = subStr;

        if (clipped != MuClip.None)
        {
            SetClip(UnclippedRect);
        }
    }

    public void DrawIcon(MuIcon iconType, MuRect rect, MuColor color)
    {
        MuClip clipped = CheckClip(rect);
        if (clipped == MuClip.All)
        {
            return;
        }

        if (clipped == MuClip.Part)
        {
            SetClip(GetClipRect());
        }

        int idx = PushCommand(MuCommandType.Icon);
        ref MuCommand cmd = ref CommandList.Items[idx];
        cmd.Icon = iconType;
        cmd.Rect = rect;
        cmd.Color = color;

        if (clipped != MuClip.None)
        {
            SetClip(UnclippedRect);
        }
    }

    private int PushJump(int dstIdx)
    {
        int idx = PushCommand(MuCommandType.Jump);
        CommandList.Items[idx].JumpDstIdx = dstIdx;

        return idx;
    }

    // ========================================================================
    // UI Controls & Interaction State
    // ========================================================================

    private bool InHoverRoot()
    {
        int i = ContainerStack.Idx;
        while (i-- > 0)
        {
            int cntIdx = ContainerStack.Items[i];
            if (cntIdx == HoverRootIdx)
            {
                return true;
            }
            if (Containers[cntIdx].HeadIdx != -1)
            {
                break;
            }
        }

        return false;
    }

    public void DrawControlFrame(uint id, MuRect rect, MuColorType colorType, MuOption opt)
    {
        if ((opt & MuOption.NoFrame) != MuOption.None)
        {
            return;
        }
        colorType += (Focus == id) ? 2 : (Hover == id) ? 1 : 0;
        DrawFrame(this, rect, colorType);
    }

    public void DrawControlText(string str, MuRect rect, MuColorType colorType, MuOption opt)
    {
        MuVec2 pos = new();
        object font = Style.Font!;
        int tw = TextWidth(font, str, -1);
        PushClipRect(rect);
        pos.Y = rect.Y + (rect.H - TextHeight(font)) / 2;
        if ((opt & MuOption.AlignCenter) != MuOption.None)
        {
            pos.X = rect.X + (rect.W - tw) / 2;
        }
        else if ((opt & MuOption.AlignRight) != MuOption.None)
        {
            pos.X = rect.X + rect.W - tw - Style.Padding;
        }
        else
        {
            pos.X = rect.X + Style.Padding;
        }
        DrawText(font, str, -1, pos, Style.Colors[colorType]);
        PopClipRect();
    }

    public bool MouseOver(MuRect rect) =>
        RectOverlapsVec2(rect, MousePos) && RectOverlapsVec2(GetClipRect(), MousePos) && InHoverRoot();

    public void UpdateControl(uint id, MuRect rect, MuOption opt)
    {
        bool mouseOver = MouseOver(rect);
        if (Focus == id)
        {
            UpdatedFocus = true;
        }
        if ((opt & MuOption.NoInteract) != MuOption.None)
        {
            return;
        }

        if (mouseOver && MouseDown == MuMouse.None)
        {
            Hover = id;
        }

        if (Focus == id)
        {
            if (MousePressed != MuMouse.None && !mouseOver)
            {
                SetFocus(0);
            }
            if (MouseDown == MuMouse.None && (opt & MuOption.HoldFocus) == MuOption.None)
            {
                SetFocus(0);
            }
        }

        if (Hover == id)
        {
            if (MousePressed != MuMouse.None)
            {
                SetFocus(id);
            }
            else if (!mouseOver)
            {
                Hover = 0;
            }
        }
    }

    public void SetFocus(uint id)
    {
        Focus = id;
        UpdatedFocus = true;
    }

    // ========================================================================
    // Primitive Elements (Label, Buttons, Checkboxes, Textboxes)
    // ========================================================================

    public void Label(string text) =>
        DrawControlText(text, LayoutNext(), MuColorType.Text, 0);

    public void Text(string text)
    {
        object font = Style.Font!;
        MuColor color = Style.Colors[MuColorType.Text];
        LayoutBeginColumn();
        LayoutRow(1, [-1], TextHeight(font));

        int p = 0;
        while (p < text.Length)
        {
            MuRect r = LayoutNext();
            int w = 0;
            int start = p;
            int end = p;

            while (end < text.Length && text[end] != '\n')
            {
                int word = p;
                while (p < text.Length && text[p] != ' ' && text[p] != '\n')
                {
                    p++;
                }

                int wordLen = p - word;
                w += TextWidth(font, text.Substring(word, wordLen), wordLen);
                if (w > r.W && end != start)
                {
                    break;
                }

                if (p < text.Length)
                {
                    w += TextWidth(font, text[p].ToString(), 1);
                }

                end = p;
                if (p < text.Length && text[p] != '\n')
                {
                    p++;
                }
            }

            int len = end - start;
            DrawText(font, text.Substring(start), len, new MuVec2(r.X, r.Y), color);
            p = end + 1;
        }
        LayoutEndColumn();
    }

    public MuResult ButtonEx(string label, MuIcon icon = MuIcon.None, MuOption opt = MuOption.AlignCenter)
    {
        MuResult res = MuResult.None;
        uint id = label != null
            ? GetId("button_" + label)
            : GetId(icon);
        MuRect r = LayoutNext();
        UpdateControl(id, r, opt);

        if (MousePressed == MuMouse.Left && Focus == id)
        {
            res |= MuResult.Submit;
        }

        DrawControlFrame(id, r, MuColorType.Button, opt);
        if (label != null)
        {
            DrawControlText(label, r, MuColorType.Text, opt);
        }
        if (icon != MuIcon.None)
        {
            DrawIcon(icon, r, Style.Colors[MuColorType.Text]);
        }

        return res;
    }

    public MuResult Button(string label) =>
        ButtonEx(label, MuIcon.None, MuOption.AlignCenter);

    public MuResult Checkbox(string label, ref bool state)
    {
        MuResult res = MuResult.None;
        uint id = GetId("checkbox_" + label);
        MuRect r = LayoutNext();
        MuRect box = new(r.X, r.Y, r.H, r.H);
        UpdateControl(id, r, 0);

        if (MousePressed == MuMouse.Left && Focus == id)
        {
            res |= MuResult.Change;
            state = !state;
        }

        DrawControlFrame(id, box, MuColorType.Base, 0);
        if (state)
        {
            DrawIcon(MuIcon.Check, box, Style.Colors[MuColorType.Text]);
        }

        MuRect rText = new(r.X + box.W, r.Y, r.W - box.W, r.H);
        DrawControlText(label, rText, MuColorType.Text, 0);

        return res;
    }

    public MuResult TextboxRaw(ref string buf, uint id, MuRect r, MuOption opt)
    {
        buf ??= string.Empty;
        MuResult res = MuResult.None;

        UpdateControl(id, r, opt | MuOption.HoldFocus);

        if (Focus == id)
        {
            if (!string.IsNullOrEmpty(InputText))
            {
                buf += InputText;
                res |= MuResult.Change;
            }
            if ((KeyPressed & MuKey.Backspace) != MuKey.None && buf.Length > 0)
            {
                buf = buf.Substring(0, buf.Length - 1);
                res |= MuResult.Change;
            }
            if ((KeyPressed & MuKey.Return) != MuKey.None)
            {
                SetFocus(0);
                res |= MuResult.Submit;
            }
        }

        DrawControlFrame(id, r, MuColorType.Base, opt);

        if (Focus == id)
        {
            MuColor color = Style.Colors[MuColorType.Text];
            object font = Style.Font!;
            int textW = TextWidth(font, buf, -1);
            int textH = TextHeight(font);
            int ofX = r.W - Style.Padding - textW - 1;
            int textX = r.X + int.Min(ofX, Style.Padding);
            int textY = r.Y + (r.H - textH) / 2;

            PushClipRect(r);
            DrawText(font, buf, -1, new(textX, textY), color);
            DrawRect(new(textX + textW, textY, 1, textH), color);
            PopClipRect();
        }
        else
        {
            DrawControlText(buf, r, MuColorType.Text, opt);
        }

        return res;
    }

    public MuResult Textbox(ref string buf, MuOption opt = MuOption.None, [CallerArgumentExpression(nameof(buf))] string expression = "")
    {
        uint id = GetId("textbox_" + expression);

        return TextboxRaw(ref buf, id, LayoutNext(), opt);
    }

    // ========================================================================
    // Numeric Controls (Sliders / Direct Inputs)
    // ========================================================================

    private bool NumberTextbox(ref float value, MuRect r, uint id)
    {
        if (MousePressed == MuMouse.Left && (KeyDown & MuKey.Shift) != MuKey.None && Hover == id)
        {
            NumberEdit = id;
            NumberEditBuf = value.ToString("G3");
        }
        if (NumberEdit == id)
        {
            MuResult res = TextboxRaw(ref NumberEditBuf, id, r, 0);
            if ((res & MuResult.Submit) != MuResult.None || Focus != id)
            {
                if (float.TryParse(NumberEditBuf, out float parsedVal))
                {
                    value = parsedVal;
                }
                NumberEdit = 0;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    public MuResult Slider(ref float value, float low, float high, float step = 0, string fmt = "F2", MuOption opt = MuOption.AlignCenter, [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        MuResult res = MuResult.None;
        float last = value;
        float v = last;
        uint id = GetId("slider_" + expression);
        MuRect r = LayoutNext();

        if (NumberTextbox(ref v, r, id))
        {
            value = v;
            return res;
        }

        UpdateControl(id, r, opt);
        if (Focus == id && (MouseDown | MousePressed) == MuMouse.Left)
        {
            v = low + (MousePos.X - r.X) * (high - low) / r.W;
            if (step != 0)
            {
                v = float.Round(v / step) * step;
            }
        }

        value = float.Clamp(v, low, high);
        if (last != value)
        {
            res |= MuResult.Change;
        }

        DrawControlFrame(id, r, MuColorType.Base, opt);
        int w = Style.ThumbSize;
        int x = (int)((value - low) * (r.W - w) / (high - low));
        MuRect thumb = new(r.X + x, r.Y, w, r.H);
        DrawControlFrame(id, thumb, MuColorType.Button, opt);

        DrawControlText(value.ToString(fmt), r, MuColorType.Text, opt);

        return res;
    }

    public MuResult Number(ref float value, float step, string fmt = "F2", MuOption opt = MuOption.AlignCenter, [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        MuResult res = MuResult.None;
        uint id = GetId("number_" + expression);
        MuRect baseRect = LayoutNext();
        float last = value;

        if (NumberTextbox(ref value, baseRect, id))
        {
            return res;
        }

        UpdateControl(id, baseRect, opt);
        if (Focus == id && MouseDown == MuMouse.Left)
        {
            value += MouseDelta.X * step;
        }

        if (value != last)
        {
            res |= MuResult.Change;
        }

        DrawControlFrame(id, baseRect, MuColorType.Base, opt);
        DrawControlText(value.ToString(fmt), baseRect, MuColorType.Text, opt);

        return res;
    }

    // ========================================================================
    // TreeNodes & Headers
    // ========================================================================

    private MuResult Header(string label, bool isTreeNode, MuOption opt)
    {
        uint id = GetId(label);
        int idx = PoolGet(TreeNodePool, TreeNodePoolSize, id);
        LayoutRow(1, [-1], 0);
        bool active = (idx >= 0);
        bool expanded = ((opt & MuOption.Expanded) != MuOption.None)
            ? !active
            : active;
        MuRect r = LayoutNext();
        UpdateControl(id, r, 0);

        if (MousePressed == MuMouse.Left && Focus == id)
        {
            active = !active;
        }

        if (idx >= 0)
        {
            if (active)
            {
                PoolUpdate(TreeNodePool, idx);
            }
            else
            {
                TreeNodePool[idx] = new MuPoolItem();
            }
        }
        else if (active)
        {
            PoolInit(TreeNodePool, TreeNodePoolSize, id);
        }

        if (isTreeNode)
        {
            if (Hover == id)
            {
                DrawFrame(this, r, MuColorType.ButtonHover);
            }
        }
        else
        {
            DrawControlFrame(id, r, MuColorType.Button, 0);
        }

        MuIcon iconType = expanded
            ? MuIcon.Expanded
            : MuIcon.Collapsed;
        DrawIcon(iconType, new MuRect(r.X, r.Y, r.H, r.H), Style.Colors[MuColorType.Text]);

        MuRect rText = new(r.X + r.H - Style.Padding, r.Y, r.W - r.H + Style.Padding, r.H);
        DrawControlText(label, rText, MuColorType.Text, 0);

        return expanded
            ? MuResult.Active
            : MuResult.None;
    }

    public MuResult Header(string label, MuOption opt = MuOption.None) =>
        Header(label, false, opt);

    public MuResult BeginTreeNode(string label, MuOption opt = MuOption.None)
    {
        MuResult res = Header(label, true, opt);
        if ((res & MuResult.Active) != MuResult.None)
        {
            GetLayout().Indent += Style.Indent;
            IdStack.Push(LastId);
        }

        return res;
    }

    public void EndTreeNode()
    {
        GetLayout().Indent -= Style.Indent;
        PopId();
    }

    // ========================================================================
    // Scrollbar Implementations
    // ========================================================================

    private void Scrollbar(int cntIdx, ref MuRect body, MuVec2 cs, bool isVertical)
    {
        ref int scroll = ref isVertical
            ? ref Containers[cntIdx].Scroll.Y
            : ref Containers[cntIdx].Scroll.X;
        int bodySize = isVertical
            ? body.H
            : body.W;
        int contentSize = isVertical
            ? cs.Y
            : cs.X;
        int maxScroll = contentSize - bodySize;

        if (maxScroll > 0 && bodySize > 0)
        {
            uint id = GetId(isVertical ? "!scrollbar_y" : "!scrollbar_x");
            MuRect baseRect = body;

            if (isVertical)
            {
                baseRect.X = body.X + body.W;
                baseRect.W = Style.ScrollbarSize;
            }
            else
            {
                baseRect.Y = body.Y + body.H;
                baseRect.H = Style.ScrollbarSize;
            }

            UpdateControl(id, baseRect, 0);

            if (Focus == id && MouseDown == MuMouse.Left)
            {
                scroll += (isVertical ? MouseDelta.Y : MouseDelta.X) * contentSize / (isVertical ? baseRect.H : baseRect.W);
            }
            scroll = int.Clamp(scroll, 0, maxScroll);

            DrawFrame(this, baseRect, MuColorType.ScrollBase);
            MuRect thumb = baseRect;

            if (isVertical)
            {
                thumb.H = int.Max(Style.ThumbSize, baseRect.H * bodySize / contentSize);
                thumb.Y += scroll * (baseRect.H - thumb.H) / maxScroll;
            }
            else
            {
                thumb.W = int.Max(Style.ThumbSize, baseRect.W * bodySize / contentSize);
                thumb.X += scroll * (baseRect.W - thumb.W) / maxScroll;
            }
            DrawFrame(this, thumb, MuColorType.ScrollThumb);

            if (MouseOver(body))
            {
                ScrollTargetIdx = cntIdx;
            }
        }
        else
        {
            scroll = 0;
        }
    }

    private void Scrollbars(int cntIdx, ref MuRect body)
    {
        int sz = Style.ScrollbarSize;
        MuVec2 cs = Containers[cntIdx].ContentSize;
        cs.X += Style.Padding * 2;
        cs.Y += Style.Padding * 2;

        PushClipRect(body);
        if (cs.Y > Containers[cntIdx].Body.H)
        {
            body.W -= sz;
        }
        if (cs.X > Containers[cntIdx].Body.W)
        {
            body.H -= sz;
        }

        Scrollbar(cntIdx, ref body, cs, true);  // Vertical
        Scrollbar(cntIdx, ref body, cs, false); // Horizontal
        PopClipRect();
    }

    private void PushContainerBody(int cntIdx, MuRect body, MuOption opt)
    {
        if ((opt & MuOption.NoScroll) == MuOption.None)
        {
            Scrollbars(cntIdx, ref body);
        }
        PushLayout(ExpandRect(body, -Style.Padding), Containers[cntIdx].Scroll);
        Containers[cntIdx].Body = body;
    }

    private void BeginRootContainer(int cntIdx)
    {
        ContainerStack.Push(cntIdx);
        RootList.Push(cntIdx);

        Containers[cntIdx].HeadIdx = PushJump(-1);

        if (RectOverlapsVec2(Containers[cntIdx].Rect, MousePos) &&
            (NextHoverRootIdx == -1 || Containers[cntIdx].ZIndex > Containers[NextHoverRootIdx].ZIndex))
        {
            NextHoverRootIdx = cntIdx;
        }

        ClipStack.Push(UnclippedRect);
    }

    private void EndRootContainer()
    {
        int cntIdx = GetCurrentContainerIdx();
        Containers[cntIdx].TailIdx = PushJump(-1);

        int headIdx = Containers[cntIdx].HeadIdx;
        CommandList.Items[headIdx].JumpDstIdx = CommandList.Idx;

        PopClipRect();
        PopContainer();
    }

    // ========================================================================
    // Window Containers (Windows, Popups, Panels)
    // ========================================================================

    public MuResult BeginWindowEx(string title, MuRect rect, MuOption opt = MuOption.None)
    {
        uint id = GetId(title);
        int cntIdx = GetContainerIdx(id, opt);
        if (cntIdx == -1 || !Containers[cntIdx].Open)
        {
            return MuResult.None;
        }

        IdStack.Push(id);
        if (Containers[cntIdx].Rect.W == 0)
        {
            Containers[cntIdx].Rect = rect;
        }

        BeginRootContainer(cntIdx);
        rect = Containers[cntIdx].Rect;
        MuRect body = rect;

        if ((opt & MuOption.NoFrame) == MuOption.None)
        {
            DrawFrame(this, rect, MuColorType.WindowBg);
        }

        if ((opt & MuOption.NotTitle) == MuOption.None)
        {
            MuRect tr = rect;
            tr.H = Style.TitleHeight;
            DrawFrame(this, tr, MuColorType.TitleBg);

            uint titleId = GetId("!title");
            UpdateControl(titleId, tr, opt);
            DrawControlText(title, tr, MuColorType.TitleText, opt);

            if (titleId == Focus && MouseDown == MuMouse.Left)
            {
                Containers[cntIdx].Rect.X += MouseDelta.X;
                Containers[cntIdx].Rect.Y += MouseDelta.Y;
            }
            body.Y += tr.H;
            body.H -= tr.H;

            if ((opt & MuOption.NoClose) == MuOption.None)
            {
                uint closeId = GetId("!close");
                MuRect rClose = new(tr.X + tr.W - tr.H, tr.Y, tr.H, tr.H);
                tr.W -= rClose.W;
                DrawIcon(MuIcon.Close, rClose, Style.Colors[MuColorType.TitleText]);
                UpdateControl(closeId, rClose, opt);
                if (MousePressed == MuMouse.Left && closeId == Focus)
                {
                    Containers[cntIdx].Open = false;
                }
            }
        }

        PushContainerBody(cntIdx, body, opt);

        if ((opt & MuOption.NoResize) == MuOption.None)
        {
            int sz = Style.TitleHeight;
            uint resizeId = GetId("!resize");
            MuRect rResize = new(rect.X + rect.W - sz, rect.Y + rect.H - sz, sz, sz);
            UpdateControl(resizeId, rResize, opt);
            if (resizeId == Focus && MouseDown == MuMouse.Left)
            {
                Containers[cntIdx].Rect.W = int.Max(96, Containers[cntIdx].Rect.W + MouseDelta.X);
                Containers[cntIdx].Rect.H = int.Max(64, Containers[cntIdx].Rect.H + MouseDelta.Y);
            }
        }

        if ((opt & MuOption.AutoSize) != MuOption.None)
        {
            MuRect rBody = GetLayout().Body;
            Containers[cntIdx].Rect.W = Containers[cntIdx].ContentSize.X + (Containers[cntIdx].Rect.W - rBody.W);
            Containers[cntIdx].Rect.H = Containers[cntIdx].ContentSize.Y + (Containers[cntIdx].Rect.H - rBody.H);
        }

        if ((opt & MuOption.Popup) != MuOption.None && MousePressed != MuMouse.None && HoverRootIdx != cntIdx)
        {
            Containers[cntIdx].Open = false;
        }

        PushClipRect(Containers[cntIdx].Body);

        return MuResult.Active;
    }

    public void EndWindow()
    {
        PopClipRect();
        EndRootContainer();
    }

    public void OpenPopup(string name)
    {
        int cntIdx = GetContainerIdx(name);
        HoverRootIdx = cntIdx;
        NextHoverRootIdx = cntIdx;
        Containers[cntIdx].Rect = new MuRect(MousePos.X, MousePos.Y, 1, 1);
        Containers[cntIdx].Open = true;
        BringToFront(cntIdx);
    }

    public MuResult BeginPopup(string name)
    {
        MuOption opt = MuOption.Popup | MuOption.AutoSize | MuOption.NoResize | MuOption.NoScroll | MuOption.NotTitle | MuOption.Closed;
        return BeginWindowEx(name, new MuRect(0, 0, 0, 0), opt);
    }

    public void EndPopup() =>
        EndWindow();

    public void BeginPanelEx(string name, MuOption opt = MuOption.None)
    {
        PushId(name);
        int cntIdx = GetContainerIdx(LastId, opt);
        Containers[cntIdx].Rect = LayoutNext();
        if ((opt & MuOption.NoFrame) == MuOption.None)
        {
            DrawFrame(this, Containers[cntIdx].Rect, MuColorType.PanelBg);
        }
        ContainerStack.Push(cntIdx);
        PushContainerBody(cntIdx, Containers[cntIdx].Rect, opt);
        PushClipRect(Containers[cntIdx].Body);
    }

    public void EndPanel()
    {
        PopClipRect();
        PopContainer();
    }

    public MuResult BeginWindow(string title, MuRect rect) =>
        BeginWindowEx(title, rect, MuOption.None);

    public void BeginPanel(string name) =>
        BeginPanelEx(name, MuOption.None);

    // ========================================================================
    // Traversal Logic (Command List Parser Pipeline)
    // ========================================================================

    public bool NextCommand(ref int cmdIdx, out MuCommand cmd)
    {
        cmdIdx = (cmdIdx < 0) ? 0 : cmdIdx + 1;

        while (cmdIdx < CommandList.Idx)
        {
            cmd = CommandList.Items[cmdIdx];
            if (cmd.Type != MuCommandType.Jump)
            {
                return true;
            }
            cmdIdx = cmd.JumpDstIdx;
        }

        cmd = default;

        return false;
    }

    public IEnumerable<MuCommand> GetCommands()
    {
        int cmdIdx = -1;
        while (NextCommand(ref cmdIdx, out MuCommand cmd))
        {
            yield return cmd;
        }
    }
}
