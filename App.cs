using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ChojemaTickerBar;

internal sealed class App : Application
{
    private readonly string statePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chojema TickerBar", "state.json");
    private TickerState state = new();
    private TickerWindow? ticker;
    private EditorWindow? editor;
    private Forms.ContextMenuStrip? menu;
    private Forms.ToolStripItem? toggle;
    private Forms.NotifyIcon? tray;
    private Icon? trayIcon;

    [STAThread]
    private static int Main()
    {
        using var instance = new Mutex(initiallyOwned: false, @"Local\ChojemaTickerBar");
        try
        {
            if (!instance.WaitOne(0))
                return 0;
        }
        catch (AbandonedMutexException) { }

        try
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            return app.Run();
        }
        finally
        {
            instance.ReleaseMutex();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            state = TickerState.Load(statePath);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"일정과 메모를 읽을 수 없습니다.\n{statePath}\n\n{error.Message}",
                "Chojema TickerBar", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        menu = new Forms.ContextMenuStrip
        {
            Font = new Font("Segoe UI", 10f),
            ShowImageMargin = false,
            Padding = new Forms.Padding(4),
            Renderer = new Forms.ToolStripProfessionalRenderer(new FlatMenuColors()) { RoundedEdges = false }
        };
        toggle = menu.Items.Add("티커 숨기기");
        toggle.Click += (_, _) => ToggleTicker();
        menu.Items.Add("일정과 메모 관리", null, (_, _) => OpenEditor());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Shutdown());
        foreach (Forms.ToolStripItem item in menu.Items)
            item.Padding = new Forms.Padding(10, 6, 24, 6);
        using var iconStream = GetResourceStream(
            new Uri("/ChojemaTickerBar;component/Assets/TickerBar.ico", UriKind.Relative)).Stream;
        trayIcon = new Icon(iconStream, Forms.SystemInformation.SmallIconSize);
        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "Chojema TickerBar",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.MouseDoubleClick += (_, click) =>
        {
            if (click.Button == Forms.MouseButtons.Left)
                OpenEditor();
        };

        ToggleTicker();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        trayIcon?.Dispose();
        menu?.Dispose();
        base.OnExit(e);
    }

    private void ToggleTicker()
    {
        if (ticker is not null)
        {
            ticker.Close();
            return;
        }

        ticker = new TickerWindow();
        ticker.Apply(state);
        ticker.MouseDoubleClick += (_, click) =>
        {
            if (click.ChangedButton == System.Windows.Input.MouseButton.Left)
                OpenEditor();
        };
        ticker.Closed += (_, _) =>
        {
            ticker = null;
            toggle!.Text = "티커 표시";
        };
        toggle!.Text = "티커 숨기기";
        ticker.Show();
    }

    private void OpenEditor()
    {
        if (editor is not null)
        {
            if (editor.WindowState == WindowState.Minimized)
                editor.WindowState = WindowState.Normal;
            editor.Activate();
            return;
        }

        editor = new EditorWindow(state, Save);
        editor.Closed += (_, _) => editor = null;
        editor.Show();
    }

    private void Save(TickerState updated)
    {
        updated.Save(statePath);
        state = updated;
        ticker?.Apply(updated);
    }

    // shortcut: light colours only, the tray menu does not follow Windows dark mode.
    private sealed class FlatMenuColors : Forms.ProfessionalColorTable
    {
        private static readonly Color Surface = Color.FromArgb(249, 249, 249);
        private static readonly Color Hover = Color.FromArgb(234, 234, 234);
        private static readonly Color Line = Color.FromArgb(222, 222, 222);
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
    }
}
