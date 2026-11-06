using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Threading.Tasks;
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
    public Color WarningBackground;
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
        palette.WarningBackground = light ? Color.FromArgb(255, 239, 207) : Color.FromArgb(79, 54, 24);
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
    private readonly Label addressPreview;
    private readonly Label portPreview;
    private readonly ComboBox hostCombo;
    private readonly AccentButton editHostsButton;
    private readonly LinkLabel latencyButton;
    private readonly Label conflictLabel;
    private readonly BackgroundWorker latencyWorker;
    private readonly BackgroundWorker processWorker;
    private readonly Timer statusTimer;
    private readonly System.Collections.Generic.Dictionary<string, string> latencies = new System.Collections.Generic.Dictionary<string, string>();
    private ProxyHostCatalog hostCatalog;
    private ProxyEndpoint testedEndpoint;
    private bool suppressSelection;
    private readonly Label validationLabel;
    private readonly Label switchTitleLabel;
    private readonly Label switchHintLabel;
    private readonly ToggleSwitch proxySwitch;
    private readonly Panel mismatchPanel;
    private readonly Label mismatchTitleLabel;
    private readonly Label mismatchDetailLabel;
    private readonly Label mismatchConfiguredLabel;
    private readonly AccentButton testButton;
    private readonly Label resultLabel;
    private readonly BackgroundWorker testWorker;
    private readonly ProxyTakeoverLifecycle proxyLifecycle;
    private ThemePalette palette;
    private bool lightTheme;
    private bool suppressToggle;
    private bool activatedOnce;
    private ProxyConfiguration currentConfiguration;

    public MainForm() : this(new ProxyTakeoverLifecycle(), null) { }

    internal MainForm(ProxyTakeoverLifecycle lifecycle, ProxyHostCatalog catalog)
    {
        Text = "局域网代理共享";
        ClientSize = new Size(420, 470);
        MinimumSize = new Size(396, 509);
        MaximumSize = new Size(636, 649);
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
        addressPreview = MakeLabel("", 10F, FontStyle.Bold);
        portPreview = MakeLabel("", 10F, FontStyle.Bold);
        addressPreview.AutoEllipsis = true;
        hostCombo = new ComboBox();
        hostCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        hostCombo.DrawMode = DrawMode.OwnerDrawFixed;
        hostCombo.ItemHeight = 28;
        hostCombo.DropDownWidth = 460;
        hostCombo.AccessibleName = "代理主机";
        hostCombo.DrawItem += DrawHostItem;
        hostCombo.SelectedIndexChanged += HostSelectionChanged;
        editHostsButton = new AccentButton();
        editHostsButton.Text = "编辑";
        editHostsButton.Click += EditHostsClick;
        latencyButton = new LinkLabel();
        latencyButton.Text = "测试所有延迟";
        latencyButton.BackColor = Color.Transparent;
        latencyButton.AutoSize = false;
        latencyButton.TextAlign = ContentAlignment.MiddleRight;
        latencyButton.LinkClicked += delegate { StartLatencyTest(); };
        conflictLabel = MakeLabel("检测到您当前正在运行其他代理软件，请关闭其他代理软件后再使用。", 9F, FontStyle.Regular);
        conflictLabel.Visible = false;
        validationLabel = MakeLabel("", 8.5F, FontStyle.Regular);

        switchTitleLabel = MakeLabel("系统代理", 10F, FontStyle.Bold);
        switchHintLabel = MakeLabel("拨动开关立即应用当前配置", 8.5F, FontStyle.Regular);
        proxySwitch = new ToggleSwitch();
        proxySwitch.AccessibleName = "启用所选主机的系统代理";
        proxySwitch.ToggleChanged += ProxySwitchChanged;
        mismatchPanel = new Panel();
        mismatchPanel.Visible = false;
        mismatchTitleLabel = MakeLabel("所选主机尚未生效", 9F, FontStyle.Bold);
        mismatchDetailLabel = MakeLabel("", 8.5F, FontStyle.Regular);
        mismatchConfiguredLabel = MakeLabel("", 8.5F, FontStyle.Regular);
        mismatchDetailLabel.AutoEllipsis = true;
        mismatchConfiguredLabel.AutoEllipsis = true;
        mismatchPanel.Controls.Add(mismatchTitleLabel);
        mismatchPanel.Controls.Add(mismatchDetailLabel);
        mismatchPanel.Controls.Add(mismatchConfiguredLabel);

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
        card.Controls.Add(addressPreview);
        card.Controls.Add(portPreview);
        card.Controls.Add(hostCombo);
        card.Controls.Add(editHostsButton);
        card.Controls.Add(latencyButton);
        card.Controls.Add(validationLabel);
        Controls.Add(switchTitleLabel);
        Controls.Add(switchHintLabel);
        Controls.Add(proxySwitch);
        Controls.Add(mismatchPanel);
        Controls.Add(testButton);
        Controls.Add(resultLabel);
        Controls.Add(conflictLabel);

        testWorker = new BackgroundWorker();
        testWorker.DoWork += TestWorkerDoWork;
        testWorker.RunWorkerCompleted += TestWorkerCompleted;
        proxyLifecycle = lifecycle;
        hostCatalog = catalog;
        if (hostCatalog != null) PopulateHosts();
        latencyWorker = new BackgroundWorker();
        latencyWorker.DoWork += LatencyWorkerDoWork;
        latencyWorker.RunWorkerCompleted += LatencyWorkerCompleted;
        processWorker = new BackgroundWorker();
        processWorker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = OtherProxyDetector.FindRunning(); };
        processWorker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed || e.Error != null) return;
            string[] names = e.Result as string[];
            conflictLabel.Visible = names != null && names.Length > 0;
            if (conflictLabel.Visible) currentProxyToolTip.SetToolTip(conflictLabel, string.Join("、", names));
            LayoutControls();
        };
        statusTimer = new Timer();
        statusTimer.Interval = 1000;
        statusTimer.Tick += delegate { if (Visible && !suppressSelection) RefreshProxyStatus(); };

        lightTheme = IsSystemLightTheme();
        ApplyTheme();
        LayoutControls();
        Shown += delegate { InitializeProxyState(); processWorker.RunWorkerAsync(); statusTimer.Start(); };
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
        if (resultLabel == null || conflictLabel == null) return;
        int contentWidth = ClientSize.Width - 40;
        int left = 20;
        bool showMismatch = mismatchPanel != null && mismatchPanel.Visible;
        int conflictHeight = conflictLabel.Visible ? 52 : 0;
        int cardY = 108 + conflictHeight;
        int switchY = cardY + 178 + (showMismatch ? 62 : 0);

        titleLabel.SetBounds(left, 18, contentWidth - 100, 30);
        subtitleLabel.SetBounds(left, 52, contentWidth, 20);
        statusDot.SetBounds(ClientSize.Width - 106, 24, 16, 20);
        statusLabel.SetBounds(ClientSize.Width - 88, 24, 68, 20);
        currentProxyCaptionLabel.SetBounds(left, 77, 92, 19);
        currentProxyLabel.SetBounds(left + 96, 77, contentWidth - 96, 19);

        conflictLabel.SetBounds(left, 103, contentWidth, 46);
        mismatchPanel.SetBounds(left, cardY + 174, contentWidth, 58);
        mismatchTitleLabel.SetBounds(12, 2, contentWidth - 24, 18);
        mismatchDetailLabel.SetBounds(12, 20, contentWidth - 24, 18);
        mismatchConfiguredLabel.SetBounds(12, 39, contentWidth - 24, 18);

        card.SetBounds(left, cardY, contentWidth, 166);
        int innerWidth = card.Width - 32;
        int portWidth = Math.Min(94, Math.Max(76, innerWidth / 3));
        int gap = 12;
        int addressWidth = innerWidth - portWidth - gap;
        hostCombo.SetBounds(16, 14, innerWidth - 70, 34);
        editHostsButton.SetBounds(card.Width - 76, 14, 60, 32);
        addressLabel.SetBounds(16, 57, addressWidth, 19);
        portLabel.SetBounds(16 + addressWidth + gap, 57, portWidth, 19);
        addressPreview.SetBounds(16, 79, addressWidth, 24);
        portPreview.SetBounds(16 + addressWidth + gap, 79, portWidth, 24);
        latencyButton.SetBounds(card.Width - 140, 110, 124, 26);
        validationLabel.SetBounds(16, 137, innerWidth, 27);

        switchTitleLabel.SetBounds(left, switchY, contentWidth - 70, 21);
        switchHintLabel.SetBounds(left, switchY + 23, contentWidth - 60, 36);
        proxySwitch.Location = new Point(ClientSize.Width - 64, switchY + 5);

        int requiredHeight = switchHintLabel.Bottom + 94;
        if (ClientSize.Height < requiredHeight) ClientSize = new Size(ClientSize.Width, requiredHeight);
        int buttonY = Math.Max(ClientSize.Height - 76,
            switchHintLabel.Bottom + 17);
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
        mismatchPanel.BackColor = palette.WarningBackground;
        mismatchTitleLabel.ForeColor = palette.Warning;
        mismatchDetailLabel.ForeColor = palette.Text;
        mismatchConfiguredLabel.ForeColor = palette.Text;
        resultLabel.ForeColor = palette.MutedText;
        card.ApplyTheme(palette);
        addressPreview.ForeColor = palette.Text;
        portPreview.ForeColor = palette.Text;
        hostCombo.BackColor = palette.Input;
        hostCombo.ForeColor = palette.Text;
        editHostsButton.ApplyTheme(palette);
        latencyButton.LinkColor = palette.Accent;
        latencyButton.ActiveLinkColor = palette.AccentHover;
        latencyButton.VisitedLinkColor = palette.Accent;
        conflictLabel.ForeColor = palette.Warning;
        proxySwitch.ApplyTheme(palette);
        testButton.ApplyTheme(palette);
        if (hostCatalog != null) RefreshProxyStatus();
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
        string warning = null;
        if (hostCatalog == null) hostCatalog = ProxyHostCatalog.Load(out warning);
        PopulateHosts();
        ProxyTakeoverResult result = proxyLifecycle.Initialize();
        ApplyTakeoverResult(result);
        if (warning != null) { validationLabel.Text = warning; validationLabel.ForeColor = palette.Warning; }
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
        currentConfiguration = current;
        if (current != null)
        {
            suppressToggle = true;
            proxySwitch.SetState(hostCatalog != null && current.UsesEndpoint(hostCatalog.Selected.Endpoint), false);
            suppressToggle = false;
            currentProxyToolTip.SetToolTip(statusLabel, current.GetDetails());
            currentProxyLabel.Text = current.GetSummary();
            currentProxyToolTip.SetToolTip(currentProxyLabel, current.GetDetails());
        }
        else currentProxyLabel.Text = "无法读取系统设置";
        UpdateProxyMismatch();
        if (result.NoticeKind == ProxyTakeoverNoticeKind.Error && result.Message != null)
        {
            validationLabel.Text = result.Message;
            validationLabel.ForeColor = palette.Error;
        }
        else if (result.Message != null)
        {
            validationLabel.Text = result.Message;
            validationLabel.ForeColor = result.NoticeKind == ProxyTakeoverNoticeKind.Warning ?
                palette.Warning : palette.Success;
        }
    }

    private void UpdateProxyMismatch()
    {
        if (palette == null || mismatchPanel == null) return;
        string address;
        int port;
        string endpointError;
        bool validEndpoint = TryGetEndpoint(out address, out port, out endpointError);
        bool proxyMismatch = currentConfiguration != null && currentConfiguration.ProxyEnabled &&
            (!validEndpoint || !currentConfiguration.UsesEndpoint(new ProxyEndpoint(address, port)));
        bool visibilityChanged = mismatchPanel.Visible != proxyMismatch;
        mismatchPanel.Visible = proxyMismatch;
        if (proxyMismatch)
        {
            mismatchDetailLabel.Text = "系统当前代理：" + currentConfiguration.GetSummary();
            mismatchConfiguredLabel.Text = validEndpoint
                ? "所选主机代理：" + new ProxyEndpoint(address, port).GetServerAddress()
                : "尚未选择主机";
            currentProxyToolTip.SetToolTip(mismatchDetailLabel, mismatchDetailLabel.Text);
            currentProxyToolTip.SetToolTip(mismatchConfiguredLabel, mismatchConfiguredLabel.Text);
        }
        suppressToggle = true;
        proxySwitch.SetState(currentConfiguration != null && validEndpoint &&
            currentConfiguration.UsesEndpoint(new ProxyEndpoint(address, port)), false);
        suppressToggle = false;
        switchHintLabel.Text = proxyMismatch
            ? "开启开关即可应用所选主机。"
            : "地址切换立即保存，开关控制启用";
        switchHintLabel.ForeColor = proxyMismatch ? palette.Warning : palette.MutedText;
        if (visibilityChanged) LayoutControls();
        statusDot.ForeColor = proxyMismatch ? palette.Warning :
            (currentConfiguration != null && currentConfiguration.ProxyEnabled ? palette.Success : palette.MutedText);
        statusLabel.Text = proxyMismatch ? "未生效" :
            (currentConfiguration == null ? "无法确认" :
                (currentConfiguration.ProxyEnabled ? "已开启" : "已关闭"));
        statusLabel.ForeColor = statusDot.ForeColor;
    }

    private bool TryGetEndpoint(out string address, out int port, out string error)
    {
        ProxyHost host = hostCatalog == null ? null : hostCatalog.Selected;
        address = host == null ? "" : host.Endpoint.Address;
        port = host == null ? 0 : host.Endpoint.Port;
        error = host == null ? "请选择代理主机。" : null;
        return error == null;
    }

    private void PopulateHosts()
    {
        suppressSelection = true;
        hostCombo.Items.Clear();
        foreach (ProxyHost host in hostCatalog.Hosts) hostCombo.Items.Add(host);
        hostCombo.SelectedItem = hostCatalog.Selected;
        suppressSelection = false;
        UpdateHostPreview();
    }

    private void UpdateHostPreview()
    {
        ProxyHost host = hostCatalog.Selected;
        addressPreview.Text = host.Endpoint.Address;
        currentProxyToolTip.SetToolTip(addressPreview, host.Endpoint.Address);
        portPreview.Text = host.Endpoint.Port.ToString();
        resultLabel.Text = "";
        UpdateProxyMismatch();
    }

    private void HostSelectionChanged(object sender, EventArgs e)
    {
        if (suppressSelection || hostCatalog == null || hostCombo.SelectedItem == null) return;
        string previousId = hostCatalog.SelectedId;
        hostCatalog.SelectedId = ((ProxyHost)hostCombo.SelectedItem).Id;
        try { hostCatalog.Save(); }
        catch (Exception ex)
        {
            hostCatalog.SelectedId = previousId;
            PopulateHosts();
            validationLabel.Text = "保存主机选择失败：" + ex.Message;
            validationLabel.ForeColor = palette.Error;
            return;
        }
        UpdateHostPreview();
        ApplySelectedHost();
    }

    private void ApplySelectedHost()
    {
        validationLabel.Text = "";
        ProxyTakeoverResult observed = proxyLifecycle.ObserveExternalChange();
        if (observed.NoticeKind == ProxyTakeoverNoticeKind.Error) { ApplyTakeoverResult(observed); return; }
        ApplyTakeoverResult(proxyLifecycle.SelectEndpoint(hostCatalog.Selected.Endpoint));
    }

    private void EditHostsClick(object sender, EventArgs e)
    {
        ProxyEndpoint previous = hostCatalog.Selected.Endpoint;
        using (ProxyHostEditor editor = new ProxyHostEditor(hostCatalog, palette))
        {
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            hostCatalog = editor.Catalog;
        }
        latencies.Clear();
        PopulateHosts();
        if (!string.Equals(previous.GetServerAddress(), hostCatalog.Selected.Endpoint.GetServerAddress(), StringComparison.OrdinalIgnoreCase))
            ApplySelectedHost();
    }

    private void DrawHostItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        ProxyHost host = (ProxyHost)hostCombo.Items[e.Index];
        string latency;
        string text = host.ToString() + (latencies.TryGetValue(host.Id, out latency) ? "  ·  " + latency : "");
        TextRenderer.DrawText(e.Graphics, text, hostCombo.Font, e.Bounds, e.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void StartLatencyTest()
    {
        if (latencyWorker.IsBusy || hostCatalog == null) return;
        latencyButton.Enabled = false;
        latencyButton.Text = "正在测试…";
        editHostsButton.Enabled = false;
        latencies.Clear();
        foreach (ProxyHost host in hostCatalog.Hosts) latencies[host.Id] = "测试中…";
        hostCombo.Invalidate();
        latencyWorker.RunWorkerAsync(hostCatalog.Hosts.ToArray());
    }

    private static void LatencyWorkerDoWork(object sender, DoWorkEventArgs e)
    {
        ProxyHost[] hosts = (ProxyHost[])e.Argument;
        string[] results = new string[hosts.Length];
        Parallel.For(0, hosts.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, delegate(int i)
        {
            try { results[i] = ProxyHostProbe.Measure(hosts[i].Endpoint, 1500) + " ms"; }
            catch { results[i] = "不可达"; }
        });
        e.Result = new object[] { hosts, results };
    }

    private void LatencyWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        if (IsDisposed) return;
        latencyButton.Enabled = true;
        latencyButton.Text = "测试所有延迟";
        editHostsButton.Enabled = true;
        latencies.Clear();
        if (e.Error == null)
        {
            object[] data = (object[])e.Result;
            ProxyHost[] hosts = (ProxyHost[])data[0];
            string[] results = (string[])data[1];
            for (int i = 0; i < hosts.Length; i++) latencies[hosts[i].Id] = results[i];
        }
        else { validationLabel.Text = "延迟测试失败，请重试。"; validationLabel.ForeColor = palette.Error; }
        hostCombo.Invalidate();
        currentProxyToolTip.SetToolTip(hostCombo, "TCP 连接耗时；展开列表查看所有主机结果。");
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
            ProxyTakeoverResult observed = proxyLifecycle.ObserveExternalChange();
            if (observed.NoticeKind == ProxyTakeoverNoticeKind.Error) { ApplyTakeoverResult(observed); return; }
            ProxyTakeoverResult result = proxyLifecycle.Enable(new ProxyEndpoint(address, port));
            ApplyTakeoverResult(result);
        }
        else
        {
            ApplyTakeoverResult(proxyLifecycle.DisableOrRestore());
        }
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
        testedEndpoint = new ProxyEndpoint(address, port);
        testWorker.RunWorkerAsync(testedEndpoint);
    }

    private static void TestWorkerDoWork(object sender, DoWorkEventArgs e)
    {
        ProxyEndpoint endpoint = (ProxyEndpoint)e.Argument;
        try
        {
            ProxyHostProbe.Measure(endpoint, 1500);
        }
        catch
        {
            e.Result = new TestResult(TestResultKind.Error, "代理端口不可达");
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
        if (IsDisposed) return;
        testButton.Text = "测试连接";
        testButton.Enabled = true;
        if (hostCatalog == null || !string.Equals(testedEndpoint.GetServerAddress(),
            hostCatalog.Selected.Endpoint.GetServerAddress(), StringComparison.OrdinalIgnoreCase)) return;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (statusTimer != null) statusTimer.Dispose();
            if (currentProxyToolTip != null) currentProxyToolTip.Dispose();
            if (testWorker != null) testWorker.Dispose();
            if (latencyWorker != null) latencyWorker.Dispose();
            if (processWorker != null) processWorker.Dispose();
        }
        base.Dispose(disposing);
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

    public bool UsesEndpoint(ProxyEndpoint endpoint)
    {
        return ProxyEnabled && proxyServer.Exists &&
            string.Equals(proxyServer.Value, endpoint.GetServerAddress(),
                StringComparison.OrdinalIgnoreCase);
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

    public string GetServerAddress()
    {
        IPAddress parsed;
        string host = IPAddress.TryParse(Address, out parsed) &&
            parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? "[" + Address + "]"
            : Address;
        return host + ":" + Port;
    }
}

internal enum TestResultKind { Success, Warning, Error }

internal sealed class TestResult
{
    public readonly TestResultKind Kind;
    public readonly string Message;
    public TestResult(TestResultKind kind, string message) { Kind = kind; Message = message; }
}
