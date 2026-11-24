using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChojemaTickerBar;

var now = DateTimeOffset.Parse("2026-11-24T14:00:00+09:00");
var evening = new DateTimeOffset(new DateTime(2026, 11, 24, 23, 59, 0, DateTimeKind.Local));
var midnight = new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Local));
var state = new TickerState
{
    Items =
    [
        new("메모"),
        new("나중 일정", now.AddHours(2)),
        new("고정 메모", Pinned: true),
        new("지난 일정", now),
        new("완료 메모", Completed: true),
        new("가까운 일정", now.AddMinutes(10))
    ],
    AtBottom = true
};

var directory = Directory.CreateTempSubdirectory("ChojemaTickerBar-checks-");
string path = Path.Combine(directory.FullName, "state.json");
try
{
    state.Save(path);
    var loaded = TickerState.Load(path);
    Assert(loaded.AtBottom && !loaded.AlwaysOnTop && loaded.Items.SequenceEqual(state.Items),
        "Items and settings should survive a save and reload.");
    Assert(loaded.ActiveItems(now).Select(item => item.Text)
        .SequenceEqual(new[] { "고정 메모", "가까운 일정", "나중 일정", "메모" }),
        "The ticker should omit completed and expired entries and order the remaining entries.");
    Assert(!loaded.ActiveItems(now.AddMinutes(10)).Any(item => item.Text == "가까운 일정"),
        "An appointment should disappear at its scheduled instant.");
    (loaded with { AlwaysOnTop = true, MoveLeftToRight = true, ShowCurrentDate = false }).Save(path);
    var updated = TickerState.Load(path);
    Assert(updated.AlwaysOnTop && updated.MoveLeftToRight && !updated.ShowCurrentDate &&
        !File.Exists(path + ".tmp"),
        "Saving a changed setting should replace the previous file.");

    File.WriteAllText(path, """{"Items":[{"Text":"기존 메모"}],"AlwaysOnTop":true}""");
    var legacy = TickerState.Load(path);
    Assert(!legacy.MoveLeftToRight && legacy.AlwaysOnTop && legacy.Items.Single().Text == "기존 메모",
        "A file without a direction setting should use right-to-left motion and preserve existing data.");
    var today = legacy.GetContent(evening);
    Assert(today.DateLabel == "11월 24일 火" && today.Labels.Single() == "기존 메모" &&
        today.NextRefresh == midnight, "The fixed date should be separate and refresh at midnight.");
    var tomorrow = new TickerState().GetContent(midnight);
    Assert(tomorrow.DateLabel == "11월 25일 水" && tomorrow.Labels.Length == 0,
        "The fixed date should roll over even when there are no items to scroll.");
    var earlierAppointment = new TickerState { Items = [new("일정", evening.AddSeconds(30))] };
    Assert(earlierAppointment.GetContent(evening).NextRefresh == evening.AddSeconds(30),
        "An earlier appointment still refreshes before midnight.");
    var hiddenDate = (legacy with { ShowCurrentDate = false }).GetContent(evening);
    Assert(hiddenDate.Labels.Single() == "기존 메모" && hiddenDate.DateLabel is null && hiddenDate.NextRefresh is null,
        "Turning off the date keeps items and skips the date timer.");
    Assert(legacy.Items.Single().Repeat == RepeatInterval.None, "Existing items should remain one-time items.");

    var repeatDue = now.AddHours(-1);
    var recurring = new TickerItem("반복 일정", repeatDue, Repeat: RepeatInterval.Daily);
    var repeating = new TickerState { Items = [recurring], ShowCurrentDate = false };
    var past = repeating.GetContent(now);
    Assert(past.Labels.Single().StartsWith("[지연]") && past.NextRefresh is null,
        "An overdue recurrence remains visible without rearming an elapsed deadline.");
    Assert(!repeating.GetContent(repeatDue.AddMinutes(-1)).Labels.Single().Contains("[지연]") &&
        repeating.GetContent(repeatDue.AddMinutes(-1)).NextRefresh == repeatDue,
        "A recurrence becomes overdue at its scheduled time.");
    var next = (recurring with { Completed = true }).AdvanceIfCompleted();
    Assert(next.DueAt!.Value.LocalDateTime == repeatDue.LocalDateTime.AddDays(1) && !next.Completed,
        "Completion advances the due time and reopens the next occurrence.");
    (repeating with { Items = [next] }).Save(path);
    Assert(TickerState.Load(path).Items.Single() == next, "The next recurrence persists.");

    var january = new DateTimeOffset(new DateTime(2027, 1, 31, 9, 0, 0, DateTimeKind.Local));
    var february = new TickerItem("월말", january, Completed: true, Repeat: RepeatInterval.Monthly)
        .AdvanceIfCompleted();
    var march = (february with { Completed = true }).AdvanceIfCompleted();
    Assert(february.DueAt!.Value.LocalDateTime.Day == 28 && march.DueAt!.Value.LocalDateTime.Day == 31,
        "A monthly recurrence should recover its original day after a short month.");

    var day = new DateOnly(2026, 11, 24);
    var allDay = new TickerItem("날짜만 있는 일정", DueDate: day);
    var allDayState = new TickerState { Items = [allDay], ShowCurrentDate = false };
    Assert(allDayState.GetContent(evening).Labels.Single() == "[임박] 11/24(火)  날짜만 있는 일정" &&
        allDayState.GetContent(evening).NextRefresh == midnight && !allDayState.ActiveItems(midnight).Any(),
        "A date-only appointment displays without a time and expires at the following midnight.");
    var soon = new TickerState { Items = [new("내일 일정", evening.AddHours(25))], ShowCurrentDate = false };
    Assert(soon.GetContent(evening).Urgency.Single() == TickerUrgency.None &&
        soon.GetContent(evening).NextRefresh == evening.AddHours(1) &&
        soon.GetContent(evening.AddHours(2)).Labels.Single().StartsWith("[임박]") &&
        soon.GetContent(evening.AddHours(2)).Urgency.Single() == TickerUrgency.Imminent,
        "A deadline becomes imminent 24 hours ahead and the ticker refreshes at that moment.");
    var daily = new TickerItem("매일 일정", evening.AddHours(2), Repeat: RepeatInterval.Daily);
    Assert(daily.UrgencyAt(evening) == TickerUrgency.None &&
        daily.UrgencyAt(evening.AddMinutes(90)) == TickerUrgency.Imminent &&
        daily.UrgencyAt(evening.AddHours(3)) == TickerUrgency.Overdue &&
        (daily with { Completed = true }).UrgencyAt(evening.AddMinutes(90)) == TickerUrgency.None,
        "A daily recurrence warns only in its last hour, and completed items never warn.");
    var decemberFirst = new DateTime(2026, 12, 1, 14, 0, 0, DateTimeKind.Local);
    Assert(TickerItem.FormatSchedule(null, DateOnly.FromDateTime(decemberFirst)) == "12/1(火)" &&
        TickerItem.FormatSchedule(new DateTimeOffset(decemberFirst), null) == "12/1(火) 14:00",
        "Date labels should include a slash and the weekday whether or not a time is present.");
    var repeatAllDay = allDay with { Repeat = RepeatInterval.Daily };
    var allDayRepeatState = allDayState with { Items = [repeatAllDay] };
    Assert(!repeatAllDay.IsOverdue(evening) &&
        allDayRepeatState.GetContent(midnight).Labels.Single().StartsWith("[지연]"),
        "A date-only recurrence becomes overdue at the end of its calendar day.");
    var advancedDay = (repeatAllDay with { Completed = true }).AdvanceIfCompleted();
    (allDayState with { Items = [advancedDay] }).Save(path);
    Assert(TickerState.Load(path).Items.Single() == advancedDay && advancedDay.DueDate == day.AddDays(1) &&
        advancedDay.DueAt is null && !advancedDay.Completed,
        "Completing and saving a date-only recurrence should keep it date-only.");

    File.WriteAllText(path, """{"Items":[null]}""");
    try
    {
        TickerState.Load(path);
        throw new InvalidOperationException("A damaged file should be rejected.");
    }
    catch (JsonException)
    {
        Assert(File.ReadAllText(path) == """{"Items":[null]}""", "Loading should preserve the damaged file.");
    }

    Console.WriteLine("Ticker state checks passed.");
}
finally
{
    File.Delete(path);
    directory.Delete();
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
