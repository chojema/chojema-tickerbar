using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ChojemaTickerBar;

internal static class KeyboardInputMode
{
    private const string Unavailable = "—";
    private const uint ImeControl = 0x0283;
    private const uint GetConversionMode = 1;
    private const uint GetOpenStatus = 5;
    private const ulong NativeMode = 1;
    private const uint AbortIfHung = 0x0002;

    public static string Read()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return Unavailable;

        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        var target = GetGUIThreadInfo(0, ref info) && info.Focus != IntPtr.Zero ? info.Focus : foreground;
        uint thread = GetWindowThreadProcessId(target, IntPtr.Zero);
        if (thread == 0)
            return Unavailable;

        int language = unchecked((ushort)GetKeyboardLayout(thread).ToInt64());
        switch (language & 0x03ff)
        {
            case 0x09:
                return "A";
            case 0x12:
                return ReadKorean(target);
        }

        if (language == 0)
            return Unavailable;

        try
        {
            return CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            return Unavailable;
        }
    }

    private static string ReadKorean(IntPtr target)
    {
        var ime = ImmGetDefaultIMEWnd(target);
        if (ime == IntPtr.Zero || !QueryIme(ime, GetConversionMode, out var conversion))
            return Unavailable;
        if ((conversion.ToUInt64() & NativeMode) == 0)
            return "A";

        return QueryIme(ime, GetOpenStatus, out var open)
            ? open == UIntPtr.Zero ? "A" : "한"
            : Unavailable;
    }

    private static bool QueryIme(IntPtr ime, uint command, out UIntPtr result) =>
        SendMessageTimeout(ime, ImeControl, (UIntPtr)command, IntPtr.Zero, AbortIfHung, 25, out result) != IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint thread);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
}
