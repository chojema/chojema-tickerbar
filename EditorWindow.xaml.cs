using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace ChojemaTickerBar;

public partial class EditorWindow : Window
{
    private const string DayFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";
    private static readonly string[] DayInputs = [DayFormat, "yyyy-M-d", "M-d", "M/d"];
    private static readonly string[] TimeInputs = [DateTimeFormat, "yyyy-M-d HH:mm", "M-d HH:mm", "M/d HH:mm"];
    private readonly Action<TickerState> save;
    private TickerState state;

    internal EditorWindow(TickerState state, Action<TickerState> save)
    {
        this.state = state;
        this.save = save;
        InitializeComponent();

        DueText.TextChanged += (_, _) =>
        {
            var schedule = ReadSchedule();
            DatePreview.Text = TickerItem.FormatSchedule(schedule?.Time, schedule?.Day);
        };
        ItemsGrid.ItemsSource = state.Items;
        var monitors = new List<MonitorOption> { new("기본 모니터 따라가기", null) };
        foreach (var screen in Forms.Screen.AllScreens)
        {
            string name = screen.DeviceName[4..];
            monitors.Add(new(screen.Primary ? $"{name} (주 모니터)" : name, screen.DeviceName));
        }

        MonitorPicker.ItemsSource = monitors;
        MonitorPicker.SelectedValue = state.Monitor;
        if (MonitorPicker.SelectedIndex < 0)
            MonitorPicker.SelectedIndex = 0;
        EdgePicker.SelectedIndex = state.AtBottom ? 1 : 0;
        DirectionPicker.SelectedIndex = state.MoveLeftToRight ? 1 : 0;
        DateCheck.IsChecked = state.ShowCurrentDate;
        TopmostCheck.IsChecked = state.AlwaysOnTop;
        MonitorPicker.SelectionChanged += Monitor_SelectionChanged;
        EdgePicker.SelectionChanged += Setting_Changed;
        DirectionPicker.SelectionChanged += Setting_Changed;
        foreach (var toggle in new[] { DateCheck, TopmostCheck })
        {
            toggle.Checked += Setting_Changed;
            toggle.Unchecked += Setting_Changed;
        }
    }

    private void Monitor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MonitorPicker.SelectedItem is not MonitorOption selected || selected.DeviceName == state.Monitor)
            return;

        if (Persist(state with { Monitor = selected.DeviceName }, $"티커 표시 모니터를 저장했습니다. ({selected.Label})"))
            return;

        MonitorPicker.SelectionChanged -= Monitor_SelectionChanged;
        MonitorPicker.SelectedItem = e.RemovedItems.Count == 0 ? null : e.RemovedItems[0];
        MonitorPicker.SelectionChanged += Monitor_SelectionChanged;
    }

    private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e) => SetFields();

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ItemsGrid.SelectedIndex = -1;
        SetFields();
        Report("");
        ItemText.Focus();
    }

    private void SetFields()
    {
        var item = ItemsGrid.SelectedItem as TickerItem;
        ItemText.Text = item?.Text ?? "";
        DueText.Text = item?.DueDate?.ToString(DayFormat, CultureInfo.InvariantCulture) ??
            item?.DueAt?.ToLocalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture) ?? "";
        PinnedCheck.IsChecked = item?.Pinned ?? false;
        RepeatPicker.SelectedIndex = (int)(item?.Repeat ?? RepeatInterval.None);
        CompletedCheck.IsChecked = item?.Completed ?? false;
        SaveItemButton.Content = item is null ? "항목 추가" : "항목 저장";
        EditorTitle.Text = item is null ? "새 항목" : "항목 편집";
        DeleteButton.IsEnabled = item is not null;
    }

    private (DateTimeOffset? Time, DateOnly? Day)? ReadSchedule()
    {
        string input = DueText.Text.Trim();
        if (input.Length == 0)
            return (null, null);
        if (DateOnly.TryParseExact(input, DayInputs, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return (null, day);
        if (DateTimeOffset.TryParseExact(input, TimeInputs, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var time))
            return (time, null);
        return null;
    }

    private DateTime PickerTime
    {
        get
        {
            var schedule = ReadSchedule();
            return schedule?.Time?.LocalDateTime ?? schedule?.Day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now;
        }
    }

    private void OpenCalendar_Click(object sender, RoutedEventArgs e)
    {
        var date = PickerTime.Date;
        DueCalendar.SelectedDate = date;
        DueCalendar.DisplayDate = date;
        CalendarPopup.IsOpen = true;
        DueCalendar.Focus();
    }

    private void ApplyDate_Click(object sender, RoutedEventArgs e)
    {
        if (DueCalendar.SelectedDate is { } date)
            DueText.Text = ReadSchedule()?.Time is { } time
                ? (date + time.LocalDateTime.TimeOfDay).ToString(DateTimeFormat, CultureInfo.InvariantCulture)
                : date.ToString(DayFormat, CultureInfo.InvariantCulture);

        CalendarPopup.IsOpen = false;
        DueText.Focus();
    }

    private void OpenClock_Click(object sender, RoutedEventArgs e)
    {
        HourList.ItemsSource ??= Enumerable.Range(0, 24).Select(value => value.ToString("00")).ToArray();
        MinuteList.ItemsSource ??= Enumerable.Range(0, 60).Select(value => value.ToString("00")).ToArray();
        var time = PickerTime;
        HourList.SelectedIndex = time.Hour;
        MinuteList.SelectedIndex = time.Minute;
        ClockPopup.IsOpen = true;
        HourList.ScrollIntoView(HourList.SelectedItem);
        MinuteList.ScrollIntoView(MinuteList.SelectedItem);
        HourList.Focus();
    }

    private void ApplyTime_Click(object sender, RoutedEventArgs e)
    {
        if (HourList.SelectedIndex >= 0 && MinuteList.SelectedIndex >= 0)
        {
            var time = PickerTime.Date.AddHours(HourList.SelectedIndex).AddMinutes(MinuteList.SelectedIndex);
            DueText.Text = time.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }

        ClockPopup.IsOpen = false;
        DueText.Focus();
    }

    private void ClearTime_Click(object sender, RoutedEventArgs e)
    {
        if (ReadSchedule()?.Time is { } time)
            DueText.Text = time.LocalDateTime.ToString(DayFormat, CultureInfo.InvariantCulture);

        ClockPopup.IsOpen = false;
        DueText.Focus();
    }

    private void Picker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        CalendarPopup.IsOpen = false;
        ClockPopup.IsOpen = false;
        DueText.Focus();
        e.Handled = true;
    }

    private void Completed_Checked(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is TickerItem { Repeat: not RepeatInterval.None, Completed: false } item &&
            RepeatPicker.SelectedIndex == (int)item.Repeat && ItemText.Text.Trim() == item.Text &&
            ReadSchedule() is { } schedule && schedule.Time == item.DueAt && schedule.Day == item.DueDate)
        {
            SaveItem_Click(sender, e);
        }
    }

    private void SaveItem_Click(object sender, RoutedEventArgs e)
    {
        string text = ItemText.Text.Trim();
        if (text.Length == 0)
        {
            Report("내용을 입력하세요.");
            ItemText.Focus();
            return;
        }

        if (ReadSchedule() is not { } schedule)
        {
            Report("12-1 또는 2026-12-01처럼 입력하세요. 필요하면 뒤에 HH:mm을 붙입니다.");
            DueText.Focus();
            return;
        }

        var (due, day) = schedule;
        bool hasDate = due is not null || day is not null;
        var repeat = (RepeatInterval)RepeatPicker.SelectedIndex;
        if (repeat != RepeatInterval.None && !hasDate)
        {
            Report("반복할 일정의 일자를 입력하세요.");
            DueText.Focus();
            return;
        }

        var items = new List<TickerItem>(state.Items);
        int index = ItemsGrid.SelectedIndex;
        var previous = ItemsGrid.SelectedItem as TickerItem;
        var item = new TickerItem(text, due, !hasDate && PinnedCheck.IsChecked == true,
            CompletedCheck.IsChecked == true, repeat, DueDate: day);
        if (previous is not null && previous.Repeat == repeat && previous.CalendarDate == item.CalendarDate)
            item = item with { RepeatDay = previous.RepeatDay };
        bool advancing = item.Completed && repeat != RepeatInterval.None;
        try
        {
            item = item.AdvanceIfCompleted();
        }
        catch (ArgumentOutOfRangeException)
        {
            Report("다음 반복 시각이 지원하는 날짜 범위를 벗어납니다.");
            return;
        }
        if (index >= 0)
            items[index] = item;
        else
        {
            index = items.Count;
            items.Add(item);
        }

        string confirmation = advancing ? "완료 처리하고 다음 회차를 저장했습니다." : "항목을 저장했습니다.";
        if (Persist(state with { Items = items.ToArray() }, confirmation))
        {
            ItemsGrid.ItemsSource = state.Items;
            ItemsGrid.SelectedIndex = index;
            ItemsGrid.ScrollIntoView(item);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        int index = ItemsGrid.SelectedIndex;
        if (index < 0 || MessageBox.Show(this, "선택한 항목을 삭제하시겠습니까?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        var items = new List<TickerItem>(state.Items);
        items.RemoveAt(index);
        if (Persist(state with { Items = items.ToArray() }, "항목을 삭제했습니다."))
        {
            ItemsGrid.ItemsSource = state.Items;
            ItemsGrid.SelectedIndex = Math.Min(index, items.Count - 1);
        }
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        Persist(state with
        {
            AlwaysOnTop = TopmostCheck.IsChecked == true,
            AtBottom = EdgePicker.SelectedIndex == 1,
            MoveLeftToRight = DirectionPicker.SelectedIndex == 1,
            ShowCurrentDate = DateCheck.IsChecked == true
        }, "표시 설정을 저장했습니다.");
    }

    private bool Persist(TickerState updated, string success)
    {
        try
        {
            save(updated);
            state = updated;
            Report(success);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"저장할 수 없습니다.\n\n{error.Message}", Title,
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void Report(string message)
    {
        Feedback.Text = message;
        UIElementAutomationPeer.FromElement(Feedback)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private sealed record MonitorOption(string Label, string? DeviceName);
}
