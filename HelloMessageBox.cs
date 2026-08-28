using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class ThemePalette
{
    public bool IsLight;
    public Color Window;
    public Color Card;
    public Color Input;
    public Color Border;
    public Color Text;
    public Color MutedText;
    public Color Accent;
    public Color AccentHover;
    public Color AccentPressed;
    public Color AccentText;
    public Color Success;
    public Color Warning;
    public Color Error;
    public Color SwitchOff;

    public static ThemePalette Create(bool light)
    {
        ThemePalette palette = new ThemePalette();
        palette.IsLight = light;
        palette.Window = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
        palette.Card = light ? Color.FromArgb(255, 255, 255) : Color.FromArgb(45, 45, 45);
        palette.Input = light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(36, 36, 36);
        palette.Border = light ? Color.FromArgb(220, 220, 220) : Color.FromArgb(68, 68, 68);
        palette.Text = light ? Color.FromArgb(30, 30, 30) : Color.FromArgb(250, 250, 250);
        palette.MutedText = light ? Color.FromArgb(96, 96, 96) : Color.FromArgb(175, 175, 175);
        palette.Accent = light ? Color.FromArgb(0, 95, 184) : Color.FromArgb(96, 205, 255);
        palette.AccentHover = light ? Color.FromArgb(0, 82, 158) : Color.FromArgb(116, 213, 255);
        palette.AccentPressed = light ? Color.FromArgb(0, 72, 140) : Color.FromArgb(73, 183, 235);
        palette.AccentText = light ? Color.White : Color.FromArgb(0, 35, 53);
        palette.Success = light ? Color.FromArgb(15, 123, 15) : Color.FromArgb(108, 203, 95);
        palette.Warning = light ? Color.FromArgb(157, 93, 0) : Color.FromArgb(255, 185, 81);
        palette.Error = light ? Color.FromArgb(196, 43, 28) : Color.FromArgb(255, 153, 164);
        palette.SwitchOff = light ? Color.FromArgb(120, 120, 120) : Color.FromArgb(145, 145, 145);
        return palette;
    }
}

internal static class DrawingTools
{
    public static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        int diameter = Math.Max(1, radius * 2);
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class CardPanel : Panel
{
    private ThemePalette palette;

    public CardPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public void ApplyTheme(ThemePalette value)
    {
        palette = value;
        BackColor = value.Window;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = DrawingTools.RoundedRectangle(bounds, 8))
        using (SolidBrush brush = new SolidBrush(palette == null ? BackColor : palette.Card))
        using (Pen pen = new Pen(palette == null ? SystemColors.ControlDark : palette.Border))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

internal sealed class ThemedTextBox : Control
{
    private readonly TextBox editor;
    private ThemePalette palette;
    private bool focused;

    public override string Text
    {
        get { return editor == null ? base.Text : editor.Text; }
        set
        {
            base.Text = value;
            if (editor != null) editor.Text = value;
        }
    }

    public ThemedTextBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 34;
        TabStop = false;

        editor = new TextBox();
        editor.BorderStyle = BorderStyle.None;
        editor.Font = new Font("Segoe UI", 9F);
        editor.Location = new Point(10, 8);
        editor.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        editor.GotFocus += delegate { focused = true; Invalidate(); };
        editor.LostFocus += delegate { focused = false; Invalidate(); };
        editor.TextChanged += delegate { base.Text = editor.Text; OnTextChanged(EventArgs.Empty); };
        Controls.Add(editor);
    }

    public void ApplyTheme(ThemePalette value)
    {
        palette = value;
        BackColor = value.Card;
        editor.BackColor = value.Input;
        editor.ForeColor = value.Text;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (editor != null) editor.Width = Math.Max(1, Width - 20);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        Color fill = palette == null ? SystemColors.Window : palette.Input;
        Color border = palette == null ? SystemColors.ControlDark : (focused ? palette.Accent : palette.Border);
        using (GraphicsPath path = DrawingTools.RoundedRectangle(bounds, 5))
        using (SolidBrush brush = new SolidBrush(fill))
        using (Pen pen = new Pen(border, focused ? 2F : 1F))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        editor.Focus();
    }
}

internal sealed class ToggleSwitch : Control
{
    private bool isOn;
    private int animationPosition;
    private int animationTarget;
    private readonly Timer timer;
    private ThemePalette palette;

    public event EventHandler ToggleChanged;

    public bool IsOn
    {
        get { return isOn; }
        set { SetState(value, true); }
    }

    public ToggleSwitch()
    {
        Size = new Size(44, 24);
        MinimumSize = Size;
        MaximumSize = Size;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        timer = new Timer();
        timer.Interval = 15;
        timer.Tick += Animate;
    }

    public void ApplyTheme(ThemePalette value)
    {
        palette = value;
        Invalidate();
    }

    public void SetState(bool value, bool animate)
    {
        isOn = value;
        animationTarget = value ? 100 : 0;
        if (animate)
        {
            timer.Start();
        }
        else
        {
            animationPosition = animationTarget;
            timer.Stop();
            Invalidate();
        }
    }

    private void Animate(object sender, EventArgs e)
    {
        if (animationPosition < animationTarget) animationPosition = Math.Min(animationTarget, animationPosition + 16);
        if (animationPosition > animationTarget) animationPosition = Math.Max(animationTarget, animationPosition - 16);
        if (animationPosition == animationTarget) timer.Stop();
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (!Enabled) return;
        SetState(!isOn, true);
        if (ToggleChanged != null) ToggleChanged(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color off = palette == null ? Color.Gray : palette.SwitchOff;
        Color on = palette == null ? Color.DodgerBlue : palette.Accent;
        float progress = animationPosition / 100F;
        Color track = Blend(off, on, progress);
        Rectangle trackBounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath trackPath = DrawingTools.RoundedRectangle(trackBounds, 12))
        using (SolidBrush trackBrush = new SolidBrush(track))
            e.Graphics.FillPath(trackBrush, trackPath);

        int knobX = 3 + (int)Math.Round((Width - 22) * progress);
        Rectangle knob = new Rectangle(knobX, 3, 18, 18);
        using (SolidBrush knobBrush = new SolidBrush(Color.White))
            e.Graphics.FillEllipse(knobBrush, knob);
    }

    private static Color Blend(Color first, Color second, float amount)
    {
        return Color.FromArgb(
            (int)(first.R + (second.R - first.R) * amount),
            (int)(first.G + (second.G - first.G) * amount),
            (int)(first.B + (second.B - first.B) * amount));
    }
}

internal sealed class AccentButton : Button
{
    private ThemePalette palette;
    private bool hovered;
    private bool pressed;

    public AccentButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void ApplyTheme(ThemePalette value)
    {
        palette = value;
        BackColor = value.Card;
        ForeColor = value.AccentText;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill;
        Color text;
        if (palette == null)
        {
            fill = SystemColors.Highlight;
            text = SystemColors.HighlightText;
        }
        else if (!Enabled)
        {
            fill = palette.Border;
            text = palette.MutedText;
        }
        else
        {
            fill = pressed ? palette.AccentPressed : (hovered ? palette.AccentHover : palette.Accent);
            text = palette.AccentText;
        }

        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = DrawingTools.RoundedRectangle(bounds, 5))
        using (SolidBrush brush = new SolidBrush(fill))
            e.Graphics.FillPath(brush, path);

        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

internal sealed class MainForm : Form
{
    private const string ApplicationSettingsPath = @"Software\ProxyShare";
    private const string ThemePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Label titleLabel;
    private readonly Label subtitleLabel;
    private readonly Label statusDot;
    private readonly Label statusLabel;
    private readonly Label currentProxyCaptionLabel;
    private readonly Label currentProxyLabel;
    private readonly ToolTip currentProxyToolTip;
    private readonly CardPanel card;
    private readonly Label addressLabel;
    private readonly Label portLabel;
    private readonly ThemedTextBox addressBox;
    private readonly ThemedTextBox portBox;
    private readonly Label validationLabel;
    private readonly Label switchTitleLabel;
    private readonly Label switchHintLabel;
    private readonly ToggleSwitch proxySwitch;
    private readonly AccentButton testButton;
    private readonly Label resultLabel;
    private readonly BackgroundWorker testWorker;
    private readonly ProxyTakeoverLifecycle proxyLifecycle;
    private ThemePalette palette;
    private bool lightTheme;
    private bool suppressToggle;
    private bool activatedOnce;

    public MainForm()
    {
        Text = "局域网代理共享";
        ClientSize = new Size(380, 410);
        MinimumSize = new Size(356, 419);
        MaximumSize = new Size(536, 529);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        DoubleBuffered = true;
        Padding = new Padding(20);

        try { Icon = new Icon("logo.ico"); } catch { }

        titleLabel = MakeLabel("代理共享", 18F, FontStyle.Bold);
        subtitleLabel = MakeLabel("快速设置 Windows 系统代理", 9F, FontStyle.Regular);
        statusDot = MakeLabel("●", 9F, FontStyle.Regular);
        statusDot.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel = MakeLabel("已关闭", 9F, FontStyle.Bold);
        currentProxyCaptionLabel = MakeLabel("当前系统代理", 8.5F, FontStyle.Regular);
        currentProxyLabel = MakeLabel("正在读取…", 9F, FontStyle.Bold);
        currentProxyLabel.AutoEllipsis = true;
        currentProxyToolTip = new ToolTip();

        card = new CardPanel();
        addressLabel = MakeLabel("代理地址", 9F, FontStyle.Regular);
        portLabel = MakeLabel("端口", 9F, FontStyle.Regular);
        addressBox = new ThemedTextBox();
        addressBox.Text = "192.168.1.72";
        portBox = new ThemedTextBox();
        portBox.Text = "7897";
        validationLabel = MakeLabel("", 8.5F, FontStyle.Regular);

        switchTitleLabel = MakeLabel("系统代理", 10F, FontStyle.Bold);
        switchHintLabel = MakeLabel("拨动开关立即应用当前配置", 8.5F, FontStyle.Regular);
        proxySwitch = new ToggleSwitch();
        proxySwitch.ToggleChanged += ProxySwitchChanged;

        testButton = new AccentButton();
        testButton.Text = "测试连接";
        testButton.Height = 38;
        testButton.Click += TestButtonClick;
        resultLabel = MakeLabel("", 9F, FontStyle.Regular);
        resultLabel.TextAlign = ContentAlignment.MiddleCenter;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(statusDot);
        Controls.Add(statusLabel);
        Controls.Add(currentProxyCaptionLabel);
        Controls.Add(currentProxyLabel);
        Controls.Add(card);
        card.Controls.Add(addressLabel);
        card.Controls.Add(portLabel);
        card.Controls.Add(addressBox);
        card.Controls.Add(portBox);
        card.Controls.Add(validationLabel);
        Controls.Add(switchTitleLabel);
        Controls.Add(switchHintLabel);
        Controls.Add(proxySwitch);
        Controls.Add(testButton);
        Controls.Add(resultLabel);

        testWorker = new BackgroundWorker();
        testWorker.DoWork += TestWorkerDoWork;
        testWorker.RunWorkerCompleted += TestWorkerCompleted;
        proxyLifecycle = new ProxyTakeoverLifecycle();

        lightTheme = IsSystemLightTheme();
        ApplyTheme();
        LayoutControls();
        Shown += delegate { InitializeProxyState(); };
        Activated += delegate
        {
            bool currentTheme = IsSystemLightTheme();
            if (currentTheme != lightTheme)
            {
                lightTheme = currentTheme;
                ApplyTheme();
            }
            if (activatedOnce) ApplyTakeoverResult(proxyLifecycle.ObserveExternalChange());
            activatedOnce = true;
        };
        FormClosing += delegate
        {
            ProxyTakeoverResult result = proxyLifecycle.MarkCleanExit();
            if (result.NoticeKind == ProxyTakeoverNoticeKind.Error)
                MessageBox.Show(this, result.Message, "保存代理接管状态", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
    }

    private static Label MakeLabel(string text, float size, FontStyle style)
    {
        Label label = new Label();
        label.Text = text;
        label.Font = new Font("Segoe UI", size, style);
        label.AutoSize = false;
        label.BackColor = Color.Transparent;
        return label;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        LayoutControls();
    }

    private void LayoutControls()
    {
        if (titleLabel == null) return;
        int contentWidth = ClientSize.Width - 40;
        int left = 20;

        titleLabel.SetBounds(left, 18, contentWidth - 100, 30);
        subtitleLabel.SetBounds(left, 49, contentWidth, 20);
        statusDot.SetBounds(ClientSize.Width - 106, 24, 16, 20);
        statusLabel.SetBounds(ClientSize.Width - 88, 24, 68, 20);
        currentProxyCaptionLabel.SetBounds(left, 72, 92, 19);
        currentProxyLabel.SetBounds(left + 96, 72, contentWidth - 96, 19);

        card.SetBounds(left, 104, contentWidth, 120);
        int innerWidth = card.Width - 32;
        int portWidth = Math.Min(94, Math.Max(76, innerWidth / 3));
        int gap = 12;
        int addressWidth = innerWidth - portWidth - gap;
        addressLabel.SetBounds(16, 13, addressWidth, 19);
        portLabel.SetBounds(16 + addressWidth + gap, 13, portWidth, 19);
        addressBox.SetBounds(16, 35, addressWidth, 34);
        portBox.SetBounds(16 + addressWidth + gap, 35, portWidth, 34);
        validationLabel.SetBounds(16, 78, innerWidth, 27);

        switchTitleLabel.SetBounds(left, 244, contentWidth - 70, 21);
        switchHintLabel.SetBounds(left, 266, contentWidth - 60, 38);
        proxySwitch.Location = new Point(ClientSize.Width - 64, 249);

        int buttonY = ClientSize.Height - 78;
        testButton.SetBounds(left, buttonY, contentWidth, 38);
        resultLabel.SetBounds(left, buttonY + 42, contentWidth, 23);
    }

    private void ApplyTheme()
    {
        palette = ThemePalette.Create(lightTheme);
        BackColor = palette.Window;
        ForeColor = palette.Text;
        titleLabel.ForeColor = palette.Text;
        subtitleLabel.ForeColor = palette.MutedText;
        statusLabel.ForeColor = palette.Text;
        currentProxyCaptionLabel.ForeColor = palette.MutedText;
        currentProxyLabel.ForeColor = palette.Text;
        addressLabel.ForeColor = palette.MutedText;
        portLabel.ForeColor = palette.MutedText;
        validationLabel.ForeColor = palette.Error;
        switchTitleLabel.ForeColor = palette.Text;
        switchHintLabel.ForeColor = palette.MutedText;
        resultLabel.ForeColor = palette.MutedText;
        card.ApplyTheme(palette);
        addressBox.ApplyTheme(palette);
        portBox.ApplyTheme(palette);
        proxySwitch.ApplyTheme(palette);
        testButton.ApplyTheme(palette);
        RefreshProxyStatus();
        Invalidate(true);
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ThemePath))
            {
                object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                return value == null || Convert.ToInt32(value) != 0;
            }
        }
        catch { return true; }
    }

    private void InitializeProxyState()
    {
        LoadLastEndpoint();
        ProxyTakeoverResult result = proxyLifecycle.Initialize();
        ApplyTakeoverResult(result);
        if (result.State == ProxyTakeoverState.RecoveryAvailable)
        {
            DialogResult choice = MessageBox.Show(this,
                "检测到上次运行未正常结束，代理设置仍由本软件管理。\r\n\r\n选择“是”恢复原配置；选择“否”继续使用当前代理。",
                "恢复系统代理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (choice == DialogResult.Yes)
            {
                ApplyTakeoverResult(proxyLifecycle.DisableOrRestore());
            }
        }
    }

    private void RefreshProxyStatus()
    {
        ApplyTakeoverResult(proxyLifecycle.ObserveExternalChange());
    }

    private void ApplyTakeoverResult(ProxyTakeoverResult result)
    {
        ProxyConfiguration current = result.Configuration;
        if (current != null)
        {
            suppressToggle = true;
            proxySwitch.SetState(current.ProxyEnabled, false);
            suppressToggle = false;
            statusDot.ForeColor = current.ProxyEnabled ? palette.Success : palette.MutedText;
            statusLabel.Text = current.ProxyEnabled ? "已开启" : "已关闭";
            statusLabel.ForeColor = current.ProxyEnabled ? palette.Success : palette.MutedText;
            currentProxyLabel.Text = current.GetSummary();
            currentProxyToolTip.SetToolTip(currentProxyLabel, current.GetDetails());
        }
        if (result.Message != null)
        {
            validationLabel.Text = result.Message;
            validationLabel.ForeColor = result.NoticeKind == ProxyTakeoverNoticeKind.Error ? palette.Error :
                (result.NoticeKind == ProxyTakeoverNoticeKind.Warning ? palette.Warning : palette.Success);
        }
    }

    private bool TryGetEndpoint(out string address, out int port, out string error)
    {
        address = addressBox.Text.Trim();
        port = 0;
        error = null;
        if (address.Length == 0)
            error = "请输入代理地址。";
        else if (!IsValidProxyHost(address))
            error = "请输入合法的 IP 地址或主机名（不要包含协议或路径）。";
        else if (!int.TryParse(portBox.Text.Trim(), out port))
            error = "端口必须是数字。";
        else if (port < 1 || port > 65535)
            error = "端口范围应为 1–65535。";
        return error == null;
    }

    private static bool IsValidProxyHost(string address)
    {
        if (address.IndexOfAny(new char[] { '/', '\\', ' ', '\t', '\r', '\n', '@' }) >= 0) return false;
        if (address.IndexOf("://", StringComparison.Ordinal) >= 0) return false;
        IPAddress parsed;
        if (IPAddress.TryParse(address, out parsed)) return true;
        if (address.Length > 253 || address.StartsWith(".") || address.EndsWith(".")) return false;
        string[] labels = address.Split('.');
        foreach (string label in labels)
        {
            if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-') return false;
            foreach (char character in label)
                if (!(char.IsLetterOrDigit(character) || character == '-')) return false;
        }
        return true;
    }

    private void ProxySwitchChanged(object sender, EventArgs e)
    {
        if (suppressToggle) return;
        if (proxySwitch.IsOn)
        {
            string address;
            int port;
            string error;
            if (!TryGetEndpoint(out address, out port, out error))
            {
                validationLabel.Text = error;
                validationLabel.ForeColor = palette.Error;
                suppressToggle = true;
                proxySwitch.SetState(false, true);
                suppressToggle = false;
                return;
            }
            validationLabel.Text = "";
            ProxyTakeoverResult result = proxyLifecycle.Enable(new ProxyEndpoint(address, port));
            ApplyTakeoverResult(result);
            if (result.NoticeKind == ProxyTakeoverNoticeKind.None)
            {
                string saveError;
                if (!TrySaveLastEndpoint(address, port, out saveError))
                {
                    validationLabel.Text = "代理已应用，但保存最近端点失败：" + saveError;
                    validationLabel.ForeColor = palette.Warning;
                }
            }
        }
        else
        {
            ApplyTakeoverResult(proxyLifecycle.DisableOrRestore());
        }
    }

    private static bool TrySaveLastEndpoint(string address, int port, out string error)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ApplicationSettingsPath))
            {
                key.SetValue("LastAddress", address, RegistryValueKind.String);
                key.SetValue("LastPort", port, RegistryValueKind.DWord);
            }
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void LoadLastEndpoint()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationSettingsPath))
            {
                if (key == null) return;
                string address = Convert.ToString(key.GetValue("LastAddress", ""));
                int port = Convert.ToInt32(key.GetValue("LastPort", 0));
                if (address.Length > 0 && port >= 1 && port <= 65535)
                {
                    addressBox.Text = address;
                    portBox.Text = port.ToString();
                }
            }
        }
        catch { }
    }


    private void TestButtonClick(object sender, EventArgs e)
    {
        if (testWorker.IsBusy) return;
        string address;
        int port;
        string error;
        if (!TryGetEndpoint(out address, out port, out error))
        {
            validationLabel.Text = error;
            validationLabel.ForeColor = palette.Error;
            resultLabel.Text = "";
            return;
        }

        validationLabel.Text = "";
        resultLabel.Text = "正在检测代理连接…";
        resultLabel.ForeColor = palette.MutedText;
        testButton.Text = "正在测试…";
        testButton.Enabled = false;
        testWorker.RunWorkerAsync(new ProxyEndpoint(address, port));
    }

    private static void TestWorkerDoWork(object sender, DoWorkEventArgs e)
    {
        ProxyEndpoint endpoint = (ProxyEndpoint)e.Argument;
        try
        {
            using (Ping ping = new Ping())
            {
                PingReply reply = ping.Send(endpoint.Address, 3000);
                if (reply == null || reply.Status != IPStatus.Success)
                {
                    e.Result = new TestResult(TestResultKind.Error, "代理主机不可达");
                    return;
                }
            }
        }
        catch
        {
            e.Result = new TestResult(TestResultKind.Error, "代理主机不可达");
            return;
        }

        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://www.google.com/generate_204");
            request.Proxy = new WebProxy(endpoint.Address, endpoint.Port);
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.AllowAutoRedirect = false;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                int code = (int)response.StatusCode;
                e.Result = code >= 200 && code < 400
                    ? new TestResult(TestResultKind.Success, "连接成功，代理可以使用")
                    : new TestResult(TestResultKind.Warning, "主机可达，但代理响应异常") ;
            }
        }
        catch (WebException)
        {
            e.Result = new TestResult(TestResultKind.Warning, "主机可达，但无法通过代理访问网络");
        }
        catch
        {
            e.Result = new TestResult(TestResultKind.Error, "测试失败，请检查代理配置");
        }
    }

    private void TestWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        testButton.Text = "测试连接";
        testButton.Enabled = true;
        TestResult result = e.Error == null ? e.Result as TestResult : null;
        if (result == null)
        {
            resultLabel.Text = "测试失败，请稍后重试";
            resultLabel.ForeColor = palette.Error;
            return;
        }
        resultLabel.Text = result.Message;
        resultLabel.ForeColor = result.Kind == TestResultKind.Success
            ? palette.Success
            : (result.Kind == TestResultKind.Warning ? palette.Warning : palette.Error);
    }

}

internal struct OptionalRegistryValue<T>
{
    public readonly bool Exists;
    public readonly T Value;

    public OptionalRegistryValue(bool exists, T value)
    {
        Exists = exists;
        Value = value;
    }

    public override bool Equals(object other)
    {
        if (!(other is OptionalRegistryValue<T>)) return false;
        OptionalRegistryValue<T> value = (OptionalRegistryValue<T>)other;
        return Exists == value.Exists && EqualityComparer<T>.Default.Equals(Value, value.Value);
    }

    public override int GetHashCode()
    {
        return (Exists ? 397 : 0) ^ (object.Equals(Value, null) ? 0 : Value.GetHashCode());
    }
}

internal sealed class ProxyConfiguration
{
    private const string InternetSettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    private OptionalRegistryValue<int> proxyEnable;
    private OptionalRegistryValue<string> proxyServer;
    private OptionalRegistryValue<string> proxyOverride;
    private OptionalRegistryValue<string> autoConfigUrl;
    private OptionalRegistryValue<int> autoDetect;

    public bool ProxyEnabled { get { return proxyEnable.Exists && proxyEnable.Value == 1; } }
    public string ProxyServer { get { return proxyServer.Value; } }

    public string GetProxyOverrideOrNull()
    {
        return proxyOverride.Exists ? proxyOverride.Value : null;
    }

    public void UseManualProxy(string server, string bypass)
    {
        proxyEnable = new OptionalRegistryValue<int>(true, 1);
        proxyServer = new OptionalRegistryValue<string>(true, server);
        proxyOverride = new OptionalRegistryValue<string>(true, bypass);
        autoConfigUrl = new OptionalRegistryValue<string>(false, null);
        autoDetect = new OptionalRegistryValue<int>(false, 0);
    }

    public void DisableManualProxy()
    {
        proxyEnable = new OptionalRegistryValue<int>(true, 0);
    }

    internal void SetProxyServer(string server)
    {
        proxyServer = new OptionalRegistryValue<string>(true, server);
    }

    internal void SetAutoDetect(bool exists, int value)
    {
        autoDetect = new OptionalRegistryValue<int>(exists, value);
    }

    public static ProxyConfiguration Read()
    {
        ProxyConfiguration value = new ProxyConfiguration();
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InternetSettingsPath))
        {
            if (key == null) return value;
            Dictionary<string, bool> names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in key.GetValueNames()) names[name] = true;
            value.proxyEnable = ReadDword(key, names, "ProxyEnable");
            value.proxyServer = ReadString(key, names, "ProxyServer");
            value.proxyOverride = ReadString(key, names, "ProxyOverride");
            value.autoConfigUrl = ReadString(key, names, "AutoConfigURL");
            value.autoDetect = ReadDword(key, names, "AutoDetect");
        }
        return value;
    }

    public void Write()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(InternetSettingsPath))
        {
            WriteDword(key, "ProxyEnable", proxyEnable);
            WriteString(key, "ProxyServer", proxyServer);
            WriteString(key, "ProxyOverride", proxyOverride);
            WriteString(key, "AutoConfigURL", autoConfigUrl);
            WriteDword(key, "AutoDetect", autoDetect);
        }
    }

    private static OptionalRegistryValue<string> ReadString(RegistryKey key,
        Dictionary<string, bool> names, string name)
    {
        bool exists = names.ContainsKey(name);
        return new OptionalRegistryValue<string>(exists,
            exists ? Convert.ToString(key.GetValue(name, "")) : null);
    }

    private static OptionalRegistryValue<int> ReadDword(RegistryKey key,
        Dictionary<string, bool> names, string name)
    {
        bool exists = names.ContainsKey(name);
        return new OptionalRegistryValue<int>(exists,
            exists ? Convert.ToInt32(key.GetValue(name, 0)) : 0);
    }

    private static void WriteString(RegistryKey key, string name,
        OptionalRegistryValue<string> value)
    {
        if (value.Exists) key.SetValue(name, value.Value ?? "", RegistryValueKind.String);
        else key.DeleteValue(name, false);
    }

    private static void WriteDword(RegistryKey key, string name,
        OptionalRegistryValue<int> value)
    {
        if (value.Exists) key.SetValue(name, value.Value, RegistryValueKind.DWord);
        else key.DeleteValue(name, false);
    }

    public ProxyConfiguration Clone()
    {
        return (ProxyConfiguration)MemberwiseClone();
    }

    public override bool Equals(object other)
    {
        ProxyConfiguration value = other as ProxyConfiguration;
        return value != null &&
            proxyEnable.Equals(value.proxyEnable) &&
            proxyServer.Equals(value.proxyServer) &&
            proxyOverride.Equals(value.proxyOverride) &&
            autoConfigUrl.Equals(value.autoConfigUrl) &&
            autoDetect.Equals(value.autoDetect);
    }

    public override int GetHashCode()
    {
        return proxyEnable.GetHashCode() ^ proxyServer.GetHashCode() ^
            proxyOverride.GetHashCode() ^ autoConfigUrl.GetHashCode() ^ autoDetect.GetHashCode();
    }

    public string DescribeDifferences(ProxyConfiguration actual)
    {
        List<string> differences = new List<string>();
        if (!proxyEnable.Equals(actual.proxyEnable))
            differences.Add("ProxyEnable 期望 " + Describe(proxyEnable) + "，实际 " + Describe(actual.proxyEnable));
        if (!proxyServer.Equals(actual.proxyServer))
            differences.Add("ProxyServer 期望 [" + Describe(proxyServer) + "]，实际 [" + Describe(actual.proxyServer) + "]");
        if (!proxyOverride.Equals(actual.proxyOverride))
            differences.Add("ProxyOverride 不一致");
        if (!autoConfigUrl.Equals(actual.autoConfigUrl))
            differences.Add("AutoConfigURL 不一致");
        if (!autoDetect.Equals(actual.autoDetect))
            differences.Add("AutoDetect 期望 " + Describe(autoDetect) + "，实际 " + Describe(actual.autoDetect));
        return differences.Count == 0 ? "未知差异" : string.Join("；", differences.ToArray());
    }

    private static string Describe<T>(OptionalRegistryValue<T> value)
    {
        return value.Exists ? Convert.ToString(value.Value) : "<不存在>";
    }

    public string GetSummary()
    {
        if (ProxyEnabled && proxyServer.Exists && !string.IsNullOrEmpty(proxyServer.Value))
        {
            if (proxyServer.Value.IndexOf('=') >= 0)
                return proxyServer.Value.Replace(";", "  ·  ").Replace("=", ": ");
            return proxyServer.Value;
        }
        if (autoConfigUrl.Exists && !string.IsNullOrEmpty(autoConfigUrl.Value)) return "PAC：" + autoConfigUrl.Value;
        if (autoDetect.Exists && autoDetect.Value != 0) return "自动检测设置";
        return "未启用";
    }

    public string GetDetails()
    {
        List<string> lines = new List<string>();
        lines.Add("手动代理：" + (ProxyEnabled ? "已开启" : "已关闭"));
        if (proxyServer.Exists && !string.IsNullOrEmpty(proxyServer.Value)) lines.Add("服务器：" + proxyServer.Value);
        if (autoConfigUrl.Exists && !string.IsNullOrEmpty(autoConfigUrl.Value)) lines.Add("PAC：" + autoConfigUrl.Value);
        if (autoDetect.Exists && autoDetect.Value != 0) lines.Add("自动检测：已开启");
        if (proxyOverride.Exists && !string.IsNullOrEmpty(proxyOverride.Value)) lines.Add("绕过：" + proxyOverride.Value);
        return string.Join(Environment.NewLine, lines.ToArray());
    }

    public void Save(RegistryKey key, string prefix)
    {
        SaveOptionalDword(key, prefix, "ProxyEnable", proxyEnable);
        SaveOptionalString(key, prefix, "ProxyServer", proxyServer);
        SaveOptionalString(key, prefix, "ProxyOverride", proxyOverride);
        SaveOptionalString(key, prefix, "AutoConfigUrl", autoConfigUrl);
        SaveOptionalDword(key, prefix, "AutoDetect", autoDetect);
    }

    private static void SaveOptionalString(RegistryKey key, string prefix, string name,
        OptionalRegistryValue<string> value)
    {
        key.SetValue(prefix + "Has" + name, value.Exists ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(prefix + name, value.Value ?? "", RegistryValueKind.String);
    }

    private static void SaveOptionalDword(RegistryKey key, string prefix, string name,
        OptionalRegistryValue<int> value)
    {
        key.SetValue(prefix + "Has" + name, value.Exists ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(prefix + name, value.Value, RegistryValueKind.DWord);
    }

    public static ProxyConfiguration Load(RegistryKey key, string prefix)
    {
        ProxyConfiguration value = new ProxyConfiguration();
        value.proxyEnable = LoadOptionalDword(key, prefix, "ProxyEnable");
        value.proxyServer = LoadOptionalString(key, prefix, "ProxyServer");
        value.proxyOverride = LoadOptionalString(key, prefix, "ProxyOverride");
        value.autoConfigUrl = LoadOptionalString(key, prefix, "AutoConfigUrl");
        value.autoDetect = LoadOptionalDword(key, prefix, "AutoDetect");
        return value;
    }

    private static OptionalRegistryValue<string> LoadOptionalString(RegistryKey key,
        string prefix, string name)
    {
        bool exists = Convert.ToInt32(key.GetValue(prefix + "Has" + name, 0)) == 1;
        return new OptionalRegistryValue<string>(exists,
            Convert.ToString(key.GetValue(prefix + name, "")));
    }

    private static OptionalRegistryValue<int> LoadOptionalDword(RegistryKey key,
        string prefix, string name)
    {
        bool exists = Convert.ToInt32(key.GetValue(prefix + "Has" + name, 0)) == 1;
        return new OptionalRegistryValue<int>(exists,
            Convert.ToInt32(key.GetValue(prefix + name, 0)));
    }
}

internal sealed class ProxyEndpoint
{
    public readonly string Address;
    public readonly int Port;
    public ProxyEndpoint(string address, int port) { Address = address; Port = port; }
}

internal enum TestResultKind { Success, Warning, Error }

internal sealed class TestResult
{
    public readonly TestResultKind Kind;
    public readonly string Message;
    public TestResult(TestResultKind kind, string message) { Kind = kind; Message = message; }
}
