# Chojema TickerBar

A Windows app that scrolls your key appointments and notes along the edge of the screen. All data stays on your PC and is never sent anywhere.

[한국어](README.md) | English

The app interface is in Korean.

## Install

1. Install the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Download `ChojemaTickerBar.exe` from [Releases](https://github.com/chojema/chojema-tickerbar/releases/latest), put it in any folder, and run it.

The app is unsigned, so Windows may show a warning on first launch. Choose **More info**, then **Run anyway**.

To start the app at sign-in, press `Win + R`, enter `shell:startup`, and put a shortcut to the executable in the folder that opens.

## Usage

- **Manage items**: Double-click the ticker or the tray icon.
- **Dates**: Type `12-1`, `2026-12-01`, or `12-1 14:30`, or pick with the calendar and clock buttons. Leave the date empty to make a note.
- **Repeat**: Choose daily, weekly, monthly, or yearly. Checking **완료** (Done) on a recurring item moves it to the next occurrence.
- **Warnings**: Items due within 24 hours (1 hour for daily items) are shown in a warning color as **[임박]** (due soon), and missed recurring items as **[지연]** (overdue).
- **Display settings**: Change the monitor, top or bottom edge, scroll direction, date badge, and always-on-top mode. Changes apply immediately.
- **Hide and exit**: Use the tray menu to hide the ticker or exit. The **×** at the right end of the bar also exits.

The left end of the bar shows the input mode of the current window (**한** for Korean, **A** for English), and the right end shows today's date.

## Data location

`%LOCALAPPDATA%\Chojema TickerBar\state.json`

To uninstall, delete the executable and this folder.

## License

[MIT](LICENSE)
