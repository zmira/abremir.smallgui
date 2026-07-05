using System.Text;
using abremir.MicroUISharp;
using abremir.SmallGui;

StringBuilder logBuffer = new();
bool logBufferUpdated = false;
float[] background = [90f, 95f, 100f]; // must be declared outside of the SmallGui.Run() method to persist between frames
string textboxBuffer = string.Empty; // must be declared outside of the SmallGui.Run() method to persist between frames
bool[] checkboxes = [true, false, true];
ColorTypeLabel[] colorTypes = [.. Enum.GetValues<MuColorType>().Select(colorType => new ColorTypeLabel(colorType, colorType.ToString().ToLower() + ":"))];

SmallGui.Run(title: "Small GUI Demo", width: 1024, height: 768, style: null, fullScreen: false, context =>
{
    StyleWindow(context);
    LogWindow(context);
    TestWindow(context);

    SmallGui.Clear(new(255, (byte)background[0], (byte)background[1], (byte)background[2]));
});

void TestWindow(MuContext context)
{
    if (context.BeginWindow("Demo Window", new(40, 40, 300, 450)) is not MuResult.None)
    {
        ref MuContainer window = ref context.GetCurrentContainer();
        window.Rect.W = int.Max(window.Rect.W, 240);
        window.Rect.H = int.Max(window.Rect.H, 300);

        /* window info */
        if (context.Header("Window Info") is not MuResult.None)
        {
            window = ref context.GetCurrentContainer();
            context.LayoutRow(2, [54, -1], 0);
            context.Label("Position");
            context.Label($"({window.Rect.X}, {window.Rect.Y})");
            context.Label("Size");
            context.Label($"({window.Rect.W}, {window.Rect.H})");
        }

        /* labels + buttons */
        if (context.Header("Test Buttons", MuOption.Expanded) is not MuResult.None)
        {
            context.LayoutRow(3, [86, -110, -1], 0);
            context.Label("Test Buttons 1:");
            if (context.Button("Button 1") is not MuResult.None)
            {
                WriteLog("Pressed button 1");
            }
            if (context.Button("Button 2") is not MuResult.None)
            {
                WriteLog("Pressed button 2");
            }
            context.Label("Test Buttons 2:");
            if (context.Button("Button 3") is not MuResult.None)
            {
                WriteLog("Pressed button 3");
            }
            if (context.Button("Popup") is not MuResult.None)
            {
                context.OpenPopup("Test Popup");
            }
            if (context.BeginPopup("Test Popup") is not MuResult.None)
            {
                context.Button("Hello");
                context.Button("World");
                context.EndPopup();
            }
        }

        /* tree */
        if (context.Header("Tree and Text", MuOption.Expanded) is not MuResult.None)
        {
            context.LayoutRow(2, [140, -1], 0);
            context.LayoutBeginColumn();
            if (context.BeginTreeNode("Test 1") is not MuResult.None)
            {
                if (context.BeginTreeNode("Test 1a") is not MuResult.None)
                {
                    context.Label("Hello");
                    context.Label("World");
                    context.EndTreeNode();
                }
                if (context.BeginTreeNode("Test 1b") is not MuResult.None)
                {
                    if (context.Button("Button 1") is not MuResult.None)
                    {
                        WriteLog("Pressed button 1");
                    }
                    if (context.Button("Button 2") is not MuResult.None)
                    {
                        WriteLog("Pressed button 2");
                    }
                    context.EndTreeNode();
                }
                context.EndTreeNode();
            }
            if (context.BeginTreeNode("Test 2") is not MuResult.None)
            {
                context.LayoutRow(2, [54, 54], 0);
                if (context.Button("Button 3") is not MuResult.None)
                {
                    WriteLog("Pressed button 3");
                }
                if (context.Button("Button 4") is not MuResult.None)
                {
                    WriteLog("Pressed button 4");
                }
                if (context.Button("Button 5") is not MuResult.None)
                {
                    WriteLog("Pressed button 5");
                }
                if (context.Button("Button 6") is not MuResult.None)
                {
                    WriteLog("Pressed button 6");
                }
                context.EndTreeNode();
            }
            if (context.BeginTreeNode("Test 3") is not MuResult.None)
            {
                context.Checkbox("Checkbox 1", ref checkboxes[0]);
                context.Checkbox("Checkbox 2", ref checkboxes[1]);
                context.Checkbox("Checkbox 3", ref checkboxes[2]);
                context.EndTreeNode();
            }
            context.LayoutEndColumn();
            context.LayoutBeginColumn();
            context.LayoutRow(1, [-1], 0);
            context.Text("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Maecenas lacinia, sem eu lacinia molestie, mi risus faucibus ipsum, eu varius magna felis a nulla.");
            context.LayoutEndColumn();
        }

        /* background color sliders */
        if (context.Header("Background Color", MuOption.Expanded) is not MuResult.None)
        {
            context.LayoutRow(2, [-78, -1], 74);

            /* sliders */
            context.LayoutBeginColumn();
            context.LayoutRow(2, [46, -1], 0);
            context.Label("Red:");
            context.Slider(ref background[0], 0f, 255f);
            context.Label("Green:");
            context.Slider(ref background[1], 0f, 255f);
            context.Label("Blue:");
            context.Slider(ref background[2], 0f, 255f);
            context.LayoutEndColumn();

            /* color preview */
            MuRect r = context.LayoutNext();
            context.DrawRect(r, new((byte)background[0], (byte)background[1], (byte)background[2], 255));
            context.DrawControlText($"#{(int)background[0]:X2}{(int)background[1]:X2}{(int)background[2]:X2}", r, MuColorType.Text, MuOption.AlignCenter);
        }

        context.EndWindow();
    }
}

void LogWindow(MuContext context)
{
    if (context.BeginWindow("Log Window", new(350, 40, 300, 200)) is not MuResult.None)
    {
        /* output text panel */
        context.LayoutRow(1, [-1], -25);
        context.BeginPanel("Log Output");
        ref MuContainer panel = ref context.GetCurrentContainer();
        context.LayoutRow(1, [-1], -1);
        context.Text(logBuffer.ToString());
        context.EndPanel();

        if (logBufferUpdated)
        {
            panel.Scroll.Y = panel.ContentSize.Y;
            logBufferUpdated = false;
        }

        /* input textbox + submit button */
        context.LayoutRow(2, [-70, -1], 0);
        bool submitted = false;
        if ((context.Textbox(ref textboxBuffer) & MuResult.Submit) != 0)
        {
            context.SetFocus(context.LastId);
            submitted = true;
        }
        if (context.Button("Submit") is not MuResult.None)
        {
            submitted = true;
        }
        if (submitted)
        {
            WriteLog(textboxBuffer);
            textboxBuffer = string.Empty;
        }

        context.EndWindow();
    }
}

void StyleWindow(MuContext context)
{
    if (context.BeginWindow("Style Editor", new(350, 250, 300, 240)) is not MuResult.None)
    {
        int sw = (int)(context.GetCurrentContainer().Body.W * 0.14);
        context.LayoutRow(6, [80, sw, sw, sw, sw, -1], 0);
        foreach (var colorType in colorTypes)
        {
            context.Label(colorType.Label);
            var r = ColorSlider(context, context.Style.Colors[colorType.ColorType].R, 0, 255, $"{colorType.Label}_R");
            var g = ColorSlider(context, context.Style.Colors[colorType.ColorType].G, 0, 255, $"{colorType.Label}_G");
            var b = ColorSlider(context, context.Style.Colors[colorType.ColorType].B, 0, 255, $"{colorType.Label}_B");
            var a = ColorSlider(context, context.Style.Colors[colorType.ColorType].A, 0, 255, $"{colorType.Label}_A");
            context.Style.Colors[colorType.ColorType] = new(r, g, b, a);
            context.DrawRect(context.LayoutNext(), context.Style.Colors[colorType.ColorType]);
        }
        context.EndWindow();
    }
}

void WriteLog(string message)
{
    logBuffer.AppendLine(message);
    logBufferUpdated = true;
}

byte ColorSlider(MuContext context, byte value, byte low, byte high, string expression)
{
    float tmp = (float)value;
    MuResult result = context.Slider(ref tmp, low, high, expression: expression);
    value = (byte)tmp;

    return value;
}

record ColorTypeLabel(MuColorType ColorType, string Label);