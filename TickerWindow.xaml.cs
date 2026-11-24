using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ChojemaTickerBar;

public partial class TickerWindow : Window
{
    private const int BarHeight = 36;
    private const int NoActivateOrReorder = 0x0010 | 0x0004;
    private readonly DispatcherTimer refreshTimer = new();
    private readonly DispatcherTimer inputTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(350)
    };
    private TickerState state = new();
    private AnimationClock? scroll;
    private string[] labels = [];
    private TickerUrgency[] urgencies = [];

    public TickerWindow()
    {
        InitializeComponent();
        refreshTimer.Tick += (_, _) => RefreshContent();
        inputTimer.Tick += (_, _) => RefreshInputMode();
        Loaded += (_, _) =>
        {
            RefreshContent();
            RefreshInputMode();
            inputTimer.Start();
        };
        MouseEnter += (_, _) => scroll?.Controller?.Pause();
        MouseLeave += (_, _) => scroll?.Controller?.Resume();
    }

    internal void Apply(TickerState updated)
    {
        state = updated;
        Topmost = updated.AlwaysOnTop;
        if (IsVisible)
        {
            Place();
            RefreshContent();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this);
        source.AddHook(WindowMessage);
        Place();
    }

    protected override void OnClosed(EventArgs e)
    {
        refreshTimer.Stop();
        inputTimer.Stop();
        StopScroll();
        base.OnClosed(e);
    }

    private void RefreshInputMode()
    {
        string mode = KeyboardInputMode.Read();
        if (InputModeBadge.Text != mode)
            InputModeBadge.Text = mode;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int displayChange = 0x007E;
        const int settingChange = 0x001A;
        const int setWorkArea = 0x002F;
        const int timeChange = 0x001E;
        if (message == displayChange || (message == settingChange && wParam.ToInt64() == setWorkArea))
            Dispatcher.BeginInvoke(() => { if (IsVisible) Place(); });
        if (message == timeChange)
            Dispatcher.BeginInvoke(RefreshContent);

        return IntPtr.Zero;
    }

    private void Place()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        var screen = Forms.Screen.AllScreens.FirstOrDefault(candidate => candidate.DeviceName == state.Monitor)
            ?? Forms.Screen.PrimaryScreen;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        int dpi = (int)GetDpiForWindow(handle);
        Position(dpi);
        int currentDpi = (int)GetDpiForWindow(handle);
        if (currentDpi != dpi)
            Position(currentDpi);

        void Position(int targetDpi)
        {
            int height = (int)Math.Round(BarHeight * (targetDpi == 0 ? 1.0 : targetDpi / 96.0));
            int top = state.AtBottom ? area.Bottom - height : area.Top;
            SetWindowPos(handle, IntPtr.Zero, area.Left, top, area.Width, height, NoActivateOrReorder);
        }
    }

    private void RefreshContent()
    {
        refreshTimer.Stop();
        if (!IsVisible)
            return;

        var now = DateTimeOffset.Now;
        var (content, dateLabel, nextRefresh, urgency) = state.GetContent(now);
        DateLabel.Text = dateLabel ?? "";
        DateBadge.Visibility = dateLabel is null ? Visibility.Collapsed : Visibility.Visible;
        labels = content;
        urgencies = urgency;
        RenderTape();

        if (nextRefresh is { } due)
        {
            refreshTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp((due - now).TotalMilliseconds, 1, 86_400_000));
            refreshTimer.Start();
        }
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && IsVisible)
            RenderTape();
    }

    private void RenderTape()
    {
        StopScroll();
        Tape.Children.Clear();
        EmptyMessage.Visibility = labels.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (labels.Length == 0 || Viewport.ActualWidth <= 0)
            return;

        var style = (Style)FindResource("TapeText");
        var accent = (Brush)FindResource("AlternateItem");
        var separator = (Brush)FindResource("ItemSeparator");
        var imminent = (Brush)FindResource("ImminentItem");
        var overdue = (Brush)FindResource("OverdueItem");
        TextBlock Copy(Thickness margin, bool useAccent)
        {
            var block = new TextBlock { Style = style, Margin = margin };
            for (int index = 0; index < labels.Length; index++)
            {
                if (index > 0)
                    block.Inlines.Add(new Run("     │     ") { Foreground = separator });

                var run = new Run(labels[index]);
                // Warning colours stay out of the white/mint alternation so they never read as decoration.
                if (urgencies[index] != TickerUrgency.None)
                {
                    run.Foreground = urgencies[index] == TickerUrgency.Overdue ? overdue : imminent;
                    run.FontWeight = FontWeights.Bold;
                }
                else
                {
                    if (useAccent)
                        run.Foreground = accent;
                    useAccent = !useAccent;
                }
                block.Inlines.Add(run);
            }

            return block;
        }

        var first = Copy(default, false);
        first.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var gap = Math.Max(84, Viewport.ActualWidth / 2 - first.DesiredSize.Width);
        var margin = new Thickness(0, 0, gap, 0);
        var period = first.DesiredSize.Width + gap;
        Canvas.SetTop(Tape, Math.Max(0, (BarHeight - 1 - first.DesiredSize.Height) / 2));
        first.Margin = margin;
        Tape.Children.Add(first);
        // An odd number of items needs two passes to preserve the alternating colors at the loop boundary.
        int passes = urgencies.Count(value => value == TickerUrgency.None) % 2 == 0 ? 1 : 2;
        int copies = (int)Math.Ceiling(Viewport.ActualWidth / period) + passes;
        for (int index = 1; index < copies; index++)
            Tape.Children.Add(Copy(margin, index % passes != 0));

        double cycleWidth = period * passes;
        double from = state.MoveLeftToRight ? -cycleWidth : 0;
        double to = state.MoveLeftToRight ? 0 : -cycleWidth;
        var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(cycleWidth / 52))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        scroll = (AnimationClock)animation.CreateClock(true);
        Motion.ApplyAnimationClock(TranslateTransform.XProperty, scroll);
        if (IsMouseOver)
            scroll.Controller?.Pause();
    }

    private void StopScroll()
    {
        scroll?.Controller?.Remove();
        Motion.ApplyAnimationClock(TranslateTransform.XProperty, null);
        scroll = null;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
        int x, int y, int width, int height, int flags);
}
