using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace CodexQuotaWidget;

public partial class MainWindow : Window
{
    private const double FoldedWidth=270, FoldedHeight=50, OpenWidth=584, OpenHeight=390;
    private CodexRpc? rpc;
    private Quota? five, week;
    private Usage? usage;
    private string? identity;
    private DateTime? lastUpdate;
    private bool refreshing, expanded, exiting;
    private int refreshFailures;
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer displayTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer foldTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private Forms.NotifyIcon? tray;
    private Drawing.Icon? trayIcon;
    private ContextMenu? menu;
    private Exception? displayError;
    private bool choosingLanguagePack;
    private static string L(string key, params object[] args) => Localization.Current.Text(key, args);

    public MainWindow(bool preview = false)
    {
        previewMode = preview;
        InitializeComponent();
        ApplyLanguage();
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
        SizeChanged += (_, _) => ApplyWindowPosition();
        dragTimer.Tick += DragTimerTick;
        PreviewMouseLeftButtonDown += DragPress;
        PreviewMouseLeftButtonUp += (_, e) => { if (dragPending) { EndPointerDrag(); e.Handled = true; } };
        LostMouseCapture += (_, _) => { if (dragPending && !LeftButtonPressed) EndPointerDrag(); };
        MouseEnter += (_, _) => { foldTimer.Stop(); if (!dragPending) SetExpanded(true); };
        MouseMove += (_, e) =>
        {
            if (dragPending) { UpdatePointerDrag(PointerPixels()); return; }
            if(!expanded || !SystemParameters.ClientAreaAnimation || menu?.IsOpen==true) return;
            var pointer=e.GetPosition(Full);
            MoveLayers(pointer.X/OpenWidth*2-1,pointer.Y/OpenHeight*2-1);
        };
        MouseLeave += (_, _) => { if (!dragPending) { foldTimer.Stop(); foldTimer.Start(); } };
        foldTimer.Tick += (_, _) => { foldTimer.Stop(); if (!IsMouseOver && menu?.IsOpen != true && !choosingLanguagePack && !dragPending) SetExpanded(false); };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x00000080); // no activation + tool window
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook((IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (msg == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
                if (msg == 0x2E0) Dispatcher.BeginInvoke(new Action(ApplyWindowPosition));
                return IntPtr.Zero;
            });
            InitializePlacement();
        };
        Closed += (_, _) =>
        {
            exiting = true; refreshTimer.Stop(); displayTimer.Stop(); foldTimer.Stop(); dragTimer.Stop(); rpc?.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); } trayIcon?.Dispose();
        };
        if (preview) return;
        CreateTray();
        refreshTimer.Tick += async (_, _) => await Refresh();
        displayTimer.Tick += (_, _) => UpdateDisplay();
        Loaded += async (_, _) => { refreshTimer.Start(); displayTimer.Start(); await Refresh(); };
    }

    public void SetExpanded(bool value, bool animate = true)
    {
        if(value==expanded && animate) return;
        expanded = value;
        if (value) ChooseExpansionDirection();
        Mini.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        Full.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        var width = value ? OpenWidth : FoldedWidth;
        var height = value ? OpenHeight : FoldedHeight;
        // Choose the background from the logical state, never from intermediate native resize dimensions.
        // The compact surface stays anchored at 270x50 throughout a collapse, including upward expansion.
        ArmorChrome.IsExpanded = value;
        ArmorChrome.Width = width;
        ArmorChrome.Height = height;
        ArmorChrome.VerticalAlignment = value || !opensUpwards ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        Mini.VerticalAlignment = opensUpwards ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        bool motion = value && animate && SystemParameters.ClientAreaAnimation;
        FiveRotation.BeginAnimation(RotateTransform.AngleProperty, motion ? new DoubleAnimation(-3,3,TimeSpan.FromSeconds(16)) { AutoReverse=true, RepeatBehavior=RepeatBehavior.Forever } : null);
        WeekRotation.BeginAnimation(RotateTransform.AngleProperty, motion ? new DoubleAnimation(4,-4,TimeSpan.FromSeconds(18)) { AutoReverse=true, RepeatBehavior=RepeatBehavior.Forever } : null);
        CoreGlow.BeginAnimation(OpacityProperty,motion ? new DoubleAnimation(.24,.34,TimeSpan.FromSeconds(5)) { AutoReverse=true, RepeatBehavior=RepeatBehavior.Forever } : null);
        Full.BeginAnimation(OpacityProperty,motion ? new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(300)) : null);
        RevealLayer(HeroReveal,motion,0); RevealLayer(WeekReveal,motion,55);
        RevealLayer(TodayReveal,motion,100); RevealLayer(LifetimeReveal,motion,140);
        if (!value)
        {
            HudSweep.BeginAnimation(TelemetrySweep.PhaseProperty,null); HudSweep.Visibility=Visibility.Collapsed;
            FiveDial.BeginAnimation(QuotaDial.ValueProperty,null); WeekDial.BeginAnimation(QuotaDial.ValueProperty,null);
            foreach(var layer in new[] { HeroMotion,WeekMotion,TodayMotion,LifetimeMotion })
            {
                layer.BeginAnimation(TranslateTransform.XProperty,null); layer.BeginAnimation(TranslateTransform.YProperty,null);
                layer.X=0; layer.Y=0;
            }
        }
        UpdateFeedback();
        // Animate surfaces, not the layered HWND. Independent width/height clocks and the
        // SizeChanged position correction otherwise fight over the window's screen bounds.
        SetWindowBounds(width, height);
    }

    private static void RevealLayer(TranslateTransform layer,bool animate,int delay)
    {
        layer.BeginAnimation(TranslateTransform.YProperty,animate ? new DoubleAnimation(6,0,TimeSpan.FromMilliseconds(320)) { BeginTime=TimeSpan.FromMilliseconds(delay), EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut }, FillBehavior=FillBehavior.Stop } : null);
    }
    private void MoveLayers(double x,double y)
    {
        x=Math.Clamp(x,-1,1); y=Math.Clamp(y,-1,1);
        foreach(var pair in new[] { (HeroMotion,.8d),(WeekMotion,.7d),(TodayMotion,.6d),(LifetimeMotion,.6d) })
        {
            Slide(pair.Item1,TranslateTransform.XProperty,x*pair.Item2);
            Slide(pair.Item1,TranslateTransform.YProperty,y*pair.Item2*.65);
        }
    }
    private static void Slide(TranslateTransform layer,DependencyProperty axis,double destination)
    {
        layer.BeginAnimation(axis,new DoubleAnimation(destination,TimeSpan.FromMilliseconds(180)) { EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseOut } });
    }
    private async Task Refresh()
    {
        if (refreshing || exiting) return;
        refreshing = true;
        refreshSweepShown = false;
        displayError = null;
        UpdateStatus();
        try
        {
            if (rpc?.IsAlive != true) { rpc?.Dispose(); rpc = await CodexRpc.Connect(); }
            else await rpc.SynchronizeAccount();
            var accountResponse = await rpc.Call("account/read", new { refreshToken = false });
            if (!accountResponse.TryGetProperty("account", out var account) || account.ValueKind != JsonValueKind.Object || Metrics.Text(account, "type") != "chatgpt")
            {
                ClearData();
                throw new WidgetException("error.login");
            }
            var newIdentity = rpc.AccountIdentity + "/" + Metrics.Text(account, "planType");
            if (newIdentity != identity) { ClearData(); identity = newIdentity; }
            Plan.Text = (Metrics.Text(account, "planType") ?? L("account")).ToUpperInvariant();
            var quotaTask = Attempt(rpc.Call("account/rateLimits/read", new { excludeResetCreditDetails = true }));
            var usageTask = Attempt(rpc.Call("account/usage/read"));
            var quotas = await quotaTask;
            var tokens = await usageTask;
            if (exiting) return;
            // A refresh of account identity prevents mixing data during a concurrent sign-in change.
            var after = await rpc.Call("account/read", new { refreshToken = false });
            rpc.EnsureAccountUnchanged();
            if (!after.TryGetProperty("account", out var afterAccount) || afterAccount.ValueKind != JsonValueKind.Object || rpc.AccountIdentity + "/" + Metrics.Text(afterAccount, "planType") != identity)
            {
                ClearData(); throw new WidgetException("error.accountChanged");
            }
            if (quotas.Data.HasValue) (five, week) = Metrics.ParseLimits(quotas.Data.Value);
            if (tokens.Data.HasValue) { todayDate = Metrics.LocalDay; usage = Metrics.ParseUsage(tokens.Data.Value, todayDate); }
            if (quotas.Data.HasValue || tokens.Data.HasValue) lastUpdate = DateTime.Now;
            UpdateDisplay();
            displayError = quotas.Error ?? tokens.Error;
            if (displayError != null) { rpc.Dispose(); rpc = null; }
        }
        catch (Exception error)
        {
            if (exiting) return;
            if (error is WidgetException { Key: "error.login" or "error.accountChanged" }) { ClearData(); identity = null; }
            displayError = error;
            rpc?.Dispose(); rpc = null;
        }
        finally
        {
            refreshing = false;
            refreshFailures = displayError == null ? 0 : Math.Min(4, refreshFailures + 1);
            refreshTimer.Interval = TimeSpan.FromMinutes(Math.Min(10, 1 << refreshFailures));
            UpdateStatus();
        }
    }
    private static async Task<(JsonElement? Data, Exception? Error)> Attempt(Task<JsonElement> request)
    {
        try { return (await request, null); }
        catch (Exception e) { return (null, e); }
    }
    private void ClearData()
    {
        five = null; week = null; usage = null; lastUpdate = null; identity = null;
        UpdateDisplay();
    }
    private static Brush QuotaColor(Quota? quota) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(quota == null ? "#AD8978" : Metrics.Remaining(quota) <= 20 ? "#FF4551" : Metrics.Remaining(quota) <= 40 ? "#FFB45C" : "#FF7138"));
    private static string Percent(Quota? quota) => quota == null ? "—" : Metrics.Remaining(quota).ToString("0.#") + "%";
    private void ApplyLanguage()
    {
        foreach (var key in Localization.Current.Keys) Resources[key] = L(key);
        Title = L("app.title");
        if (identity == null) Plan.Text = L("account");
        UpdateDisplay();
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        var error = displayError == null ? null : displayError is WidgetException known ? L(known.Key) : L("error.connection");
        Status.Text = refreshing ? lastUpdate.HasValue ? L("status.refreshing", lastUpdate.Value.ToString("HH:mm:ss")) : L("status.connecting")
            : error != null ? L("status.retry", error) + (lastUpdate.HasValue ? L("status.last", lastUpdate.Value.ToString("HH:mm")) : "")
            : lastUpdate.HasValue ? L("status.ready", lastUpdate.Value.ToString("HH:mm:ss")) : L("status.connecting");
        Status.ToolTip = error ?? L("status.tooltip");
        if (error != null) Mini.ToolTip = Status.Text;
        UpdateFeedback();
    }
    private void UpdateDisplay()
    {
        SetMiniQuota(MiniFive,"mini.five",five); SetMiniQuota(MiniWeek,"mini.week",week);
        MiniFiveResetAt.Text = Metrics.ResetTime(five?.ResetsAt, compact: true, timeOnly: true);
        MiniWeekResetAt.Text = Metrics.ResetTime(week?.ResetsAt, compact: true);
        MiniFiveResetAt.ToolTip = ResetTooltip(five); MiniWeekResetAt.ToolTip = ResetTooltip(week);
        MiniFive.Foreground = QuotaColor(five); MiniWeek.Foreground = QuotaColor(week);
        FiveValue.Text = Percent(five);
        WeekValue.Text = Percent(week);
        FiveValue.Foreground = QuotaColor(five); WeekValue.Foreground = QuotaColor(week);
        UpdateDial(FiveDial,five); UpdateDial(WeekDial,week);
        FiveReset.Text = five == null ? L("quota.missing") : Metrics.Countdown(five.ResetsAt);
        WeekReset.Text = week == null ? L("quota.missing") : Metrics.Countdown(week.ResetsAt);
        FiveResetAt.Text = Metrics.ResetTime(five?.ResetsAt);
        WeekResetAt.Text = Metrics.ResetTime(week?.ResetsAt);
        FiveReset.ToolTip = ResetTooltip(five); WeekReset.ToolTip = ResetTooltip(week);
        FiveResetAt.ToolTip = ResetTooltip(five); WeekResetAt.ToolTip = ResetTooltip(week);
        // Prevent a previous day's value from remaining under today's label at midnight.
        if (usage != null && usage.Today.HasValue && Metrics.LocalDay != todayDate) usage = usage with { Today = null };
        TodayValue.Text = usage?.Today.HasValue == true ? Metrics.Compact(usage.Today) : usage == null ? "—" : L("today.pending");
        TodayNote.Text = usage?.Today.HasValue == true ? L("today.official", Metrics.LocalDay[5..]) : usage?.LatestDate != null ? L("today.latest", usage.LatestDate[5..]) : L("today.waiting");
        TodayValue.ToolTip = usage?.Today.HasValue == true ? L("today.tooltip", usage.Today.Value.ToString("N0"), Metrics.LocalDay) : L("today.missing");
        LifetimeValue.Text = Metrics.Compact(usage?.Lifetime);
        // Keep both numeric cards at the same size and preserve the unit on longer counts.
        double TokenWidth(TextBlock block) => new FormattedText(block.Text, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
            29, block.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
        bool todayNumeric = usage?.Today.HasValue == true || usage == null;
        double widest = Math.Max(TokenWidth(LifetimeValue), todayNumeric ? TokenWidth(TodayValue) : 0);
        double tokenSize = widest > 98 ? 29 * 98 / widest : 29;
        LifetimeValue.FontSize = tokenSize;
        TodayValue.FontSize = todayNumeric ? tokenSize : 16;
        LifetimeNote.Text = L("lifetime.note");
        LifetimeValue.ToolTip = usage?.Lifetime.HasValue == true ? L("lifetime.tooltip", usage.Lifetime.Value.ToString("N0"), usage.FirstDate ?? L("unknown")) : L("lifetime.missing");
        Mini.ToolTip = L("mini.tooltip", Percent(five), Metrics.ResetTime(five?.ResetsAt), Percent(week), Metrics.ResetTime(week?.ResetsAt)) + "\n" + L("placement.dragHint");
        if (tray != null) { var text = L("tray.tooltip", Percent(five), Percent(week)); tray.Text = text.Length > 63 ? text[..63] : text; }
        if (displayError != null) UpdateStatus();
        else UpdateFeedback();
    }
    private string todayDate = Metrics.LocalDay;
    private void UpdateDial(QuotaDial dial,Quota? quota)
    {
        bool hadData=dial.HasData;
        dial.HasData=quota!=null; dial.Accent=QuotaColor(quota);
        double target=quota==null?0:Metrics.Remaining(quota);
        // Repainting timestamps or switching language must not restart an in-flight reading.
        if(hadData==dial.HasData && Math.Abs((double)dial.GetAnimationBaseValue(QuotaDial.ValueProperty)-target)<.01) return;
        double old=dial.Value;
        dial.BeginAnimation(QuotaDial.ValueProperty,null); dial.Value=target;
        if(hadData && quota!=null && expanded && SystemParameters.ClientAreaAnimation && Math.Abs(old-target)>.01)
            dial.BeginAnimation(QuotaDial.ValueProperty,new DoubleAnimation(old,target,TimeSpan.FromMilliseconds(450)) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut }, FillBehavior=FillBehavior.Stop });
    }
    private static string ResetTooltip(Quota? quota) => Metrics.ResetTime(quota?.ResetsAt, full: true);
    private async void RefreshClick(object sender, RoutedEventArgs e) => await Refresh();
    private void MenuClick(object sender, RoutedEventArgs e) => OpenMenu();
    private void OpenMenu()
    {
        menu = new ContextMenu { Style = (Style)FindResource("MechaMenu") };
        var refresh = new MenuItem { Header = L("refresh") }; refresh.Click += async (_, _) => await Refresh();
        var startup = new MenuItem { Header = L("startup"), IsCheckable = true, IsChecked = Startup.Enabled };
        startup.Click += (_, _) => { try { Startup.Set(startup.IsChecked); } catch { MessageBox.Show(L("startup.error"), L("app.title")); } };
        var language = new MenuItem { Header = L("language") };
        foreach (var pack in Localization.Current.Packs)
        {
            var item = new MenuItem { Header = pack.DisplayName, IsCheckable = true, IsChecked = pack.Id == Localization.Current.LanguageId, Style = (Style)FindResource(typeof(MenuItem)) };
            item.Click += (_, _) => SelectLanguage(pack.Id);
            language.Items.Add(item);
        }
        language.Items.Add(new Separator());
        var install = new MenuItem { Header = L("language.install"), Style = (Style)FindResource(typeof(MenuItem)) };
        install.Click += (_, _) => InstallLanguagePack(); language.Items.Add(install);
        var exit = new MenuItem { Header = L("exit") }; exit.Click += (_, _) => Close();
        var restore = new MenuItem { Header = L("placement.reset") }; restore.Click += (_, _) => RestorePosition();
        menu.Items.Add(refresh); menu.Items.Add(language); menu.Items.Add(restore); menu.Items.Add(startup); menu.Items.Add(new Separator()); menu.Items.Add(exit);
        foreach (var item in menu.Items) if (item is MenuItem entry) entry.Style = (Style)FindResource(typeof(MenuItem));
        menu.PlacementTarget = this;
        menu.Closed += (_, _) => { if (!IsMouseOver) { foldTimer.Stop(); foldTimer.Start(); } };
        menu.IsOpen = true;
    }
    private void SelectLanguage(string id)
    {
        try { Localization.Current.Select(id); ApplyLanguage(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { MessageBox.Show(L("language.saveError"), L("app.title")); }
    }
    private void InstallLanguagePack()
    {
        choosingLanguagePack = true; foldTimer.Stop();
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = L("language.dialog"), Filter = L("language.filter"), CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog(this) != true) return;
            var pack = Localization.Current.Install(dialog.FileName);
            SelectLanguage(pack.Id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { MessageBox.Show(L("language.invalid"), L("app.title")); }
        finally { choosingLanguagePack = false; if (!IsMouseOver) foldTimer.Start(); }
    }
    private void CreateTray()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var background = new Drawing.SolidBrush(Drawing.Color.FromArgb(27, 19, 21));
            using var foreground = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 105, 48));
            var plate = new[] { new Drawing.Point(2, 1), new Drawing.Point(25, 1), new Drawing.Point(31, 7), new Drawing.Point(31, 25), new Drawing.Point(25, 31), new Drawing.Point(2, 31) };
            graphics.FillPolygon(background, plate);
            using var border = new Drawing.Pen(Drawing.Color.FromArgb(209, 78, 40), 1.5f);
            graphics.DrawPolygon(border, plate);
            using var font = new Drawing.Font("Bahnschrift", 20, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            graphics.DrawString("C", font, foreground, 6, 4);
        }
        var handle = bitmap.GetHicon();
        trayIcon = (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
        DestroyIcon(handle);
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = L("app.title"), Visible = true };
        var trayMenu = new Forms.ContextMenuStrip
        {
            BackColor = Drawing.Color.FromArgb(29,22,24), ForeColor = Drawing.Color.FromArgb(244,212,191),
            Renderer = new Forms.ToolStripProfessionalRenderer(new MechaMenuColors())
        };
        var show = trayMenu.Items.Add(L("show"), null, (_, _) => Dispatcher.Invoke(() => { Show(); SetExpanded(true); }));
        var refresh = trayMenu.Items.Add(L("refresh"), null, async (_, _) => await Dispatcher.InvokeAsync(Refresh));
        var language = new Forms.ToolStripMenuItem(L("language")); trayMenu.Items.Add(language);
        var restore = trayMenu.Items.Add(L("placement.reset"), null, (_, _) => Dispatcher.Invoke(RestorePosition));
        var startup = new Forms.ToolStripMenuItem(L("startup")) { CheckOnClick = true };
        startup.Click += (_, _) => { try { Startup.Set(startup.Checked); } catch { Forms.MessageBox.Show(L("startup.error"), L("app.title")); } };
        trayMenu.Items.Add(startup);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        var exit = trayMenu.Items.Add(L("exit"), null, (_, _) => Dispatcher.Invoke(Close));
        trayMenu.Opening += (_, _) =>
        {
            show.Text = L("show"); refresh.Text = L("refresh"); restore.Text = L("placement.reset"); startup.Text = L("startup"); startup.Checked = Startup.Enabled; exit.Text = L("exit"); language.Text = L("language");
            while (language.DropDownItems.Count > 0) { var old = language.DropDownItems[0]; language.DropDownItems.RemoveAt(0); old.Dispose(); }
            foreach (var pack in Localization.Current.Packs)
            {
                var item = new Forms.ToolStripMenuItem(pack.DisplayName) { Checked = pack.Id == Localization.Current.LanguageId };
                item.Click += (_, _) => Dispatcher.Invoke(() => SelectLanguage(pack.Id)); language.DropDownItems.Add(item);
            }
            language.DropDownItems.Add(new Forms.ToolStripSeparator());
            language.DropDownItems.Add(L("language.install"), null, (_, _) => Dispatcher.Invoke(InstallLanguagePack));
        };
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); SetExpanded(true); });
        MouseRightButtonUp += (_, _) => OpenMenu();
    }

    public void RenderPreview(string path)
    {
        five = new Quota(9, DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds());
        week = new Quota(19, DateTimeOffset.UtcNow.AddDays(4).ToUnixTimeSeconds());
        usage = new Usage(1087118362, null, "2026-10-01", "2026-09-07");
        Plan.Text = "PLUS"; lastUpdate = DateTime.Now; UpdateDisplay();
        UpdateStatus();
        SetExpanded(true, false);
        Shell.Measure(new Size(OpenWidth, OpenHeight)); Shell.Arrange(new Rect(0, 0, OpenWidth, OpenHeight)); Shell.UpdateLayout();
        var render = new RenderTargetBitmap((int)OpenWidth*2, (int)OpenHeight*2, 192, 192, PixelFormats.Pbgra32);
        var backdrop=new DrawingVisual();
        using(var dc=backdrop.RenderOpen())
            dc.DrawRectangle(new LinearGradientBrush(MechaChrome.Ink("#18222D").Color,MechaChrome.Ink("#10121A").Color,60),null,new Rect(0,0,OpenWidth,OpenHeight));
        render.Render(backdrop);
        render.Render(Shell);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
        using var stream = File.Create(path); encoder.Save(stream);
        SetExpanded(false, false);
        if (Width != FoldedWidth || Height != FoldedHeight || Full.Visibility != Visibility.Collapsed || Mini.Visibility != Visibility.Visible) throw new InvalidOperationException("收起状态检查失败");
        Shell.Measure(new Size(FoldedWidth,FoldedHeight)); Shell.Arrange(new Rect(0,0,FoldedWidth,FoldedHeight)); Shell.UpdateLayout();
        var collapsedRender=new RenderTargetBitmap((int)FoldedWidth*2,(int)FoldedHeight*2,192,192,PixelFormats.Pbgra32);
        collapsedRender.Render(backdrop); collapsedRender.Render(Shell);
        var collapsedEncoder=new PngBitmapEncoder(); collapsedEncoder.Frames.Add(BitmapFrame.Create(collapsedRender));
        using(var collapsedStream=File.Create(Path.Combine(Path.GetDirectoryName(path)!,"收起预览.png"))) collapsedEncoder.Save(collapsedStream);
        SetExpanded(true, false);
        if (Width != OpenWidth || Height != OpenHeight || Full.Visibility != Visibility.Visible || Mini.Visibility != Visibility.Collapsed) throw new InvalidOperationException("展开状态检查失败");
        if (!Topmost || ShowActivated || ShowInTaskbar) throw new InvalidOperationException("置顶与焦点设置检查失败");
        SetExpanded(false, false);
        RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = MouseEnterEvent });
        if (Full.Visibility != Visibility.Visible) throw new InvalidOperationException("悬停展开事件检查失败");
        if(FiveDial.HasAnimatedProperties || WeekDial.HasAnimatedProperties || Math.Abs(FiveDial.Value-91)>.01 || Math.Abs(WeekDial.Value-81)>.01) throw new InvalidOperationException("展开不应重新增长额度圆弧");
        var todayOrigin=TodayValue.TranslatePoint(new Point(),Full);
        var weekOrigin=WeekValue.TranslatePoint(new Point(),Full);
        MoveLayers(10,-10);
        var driftFrame=new DispatcherFrame();
        var driftEnd=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(240) };
        driftEnd.Tick+=(_,_)=> { driftEnd.Stop(); driftFrame.Continue=false; };
        driftEnd.Start(); Dispatcher.PushFrame(driftFrame);
        if(Math.Abs(HeroMotion.X-.8)>.01 || Math.Abs(LifetimeMotion.X-.6)>.01 || Math.Abs(LifetimeMotion.Y+.39)>.01) throw new InvalidOperationException("背景视差幅度限制检查失败");
        if((TodayValue.TranslatePoint(new Point(),Full)-todayOrigin).Length>.01 || (WeekValue.TranslatePoint(new Point(),Full)-weekOrigin).Length>.01) throw new InvalidOperationException("背景视差不应移动文字");
        RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = MouseLeaveEvent });
        var frame = new DispatcherFrame();
        var end = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        end.Tick += (_, _) => { end.Stop(); frame.Continue = false; };
        end.Start(); Dispatcher.PushFrame(frame);
        if (Full.Visibility != Visibility.Collapsed || expanded) throw new InvalidOperationException("移开收起事件检查失败");
        if (FiveRotation.HasAnimatedProperties || WeekRotation.HasAnimatedProperties || HudSweep.HasAnimatedProperties || CoreGlow.HasAnimatedProperties || HeroMotion.HasAnimatedProperties || WeekMotion.HasAnimatedProperties || TodayMotion.HasAnimatedProperties || LifetimeMotion.HasAnimatedProperties) throw new InvalidOperationException("收起后停止动态效果检查失败");
    }
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr handle, int index, int value);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
