using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChojemaTickerBar;

internal enum RepeatInterval { None, Daily, Weekly, Monthly, Yearly }

internal enum TickerUrgency { None, Imminent, Overdue }

internal sealed record TickerItem(string Text, DateTimeOffset? DueAt = null, bool Pinned = false,
    bool Completed = false, RepeatInterval Repeat = RepeatInterval.None, int? RepeatDay = null,
    DateOnly? DueDate = null)
{
    [JsonIgnore]
    public string DisplayTime => FormatSchedule(DueAt, DueDate);

    public static string FormatSchedule(DateTimeOffset? time, DateOnly? day)
    {
        var localTime = time?.LocalDateTime;
        day ??= localTime is { } local ? DateOnly.FromDateTime(local) : null;
        if (day is not { } date)
            return "";

        string label = FormattableString.Invariant($"{date.Month}/{date.Day}({WeekdayHanja(date.DayOfWeek)})");
        return localTime is { } value ? label + " " + value.ToString("HH:mm", CultureInfo.InvariantCulture) : label;
    }

    public static char WeekdayHanja(DayOfWeek day) => "日月火水木金土"[(int)day];

    [JsonIgnore]
    public DateOnly? CalendarDate => DueDate ??
        (DueAt is { } due ? DateOnly.FromDateTime(due.LocalDateTime) : null);

    [JsonIgnore]
    public DateTimeOffset? Deadline
    {
        get
        {
            if (DueDate is not { } date)
                return DueAt;

            return date == DateOnly.MaxValue
                ? DateTimeOffset.MaxValue
                : new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));
        }
    }

    [JsonIgnore]
    public string Status => Completed ? "완료" : UrgencyAt(DateTimeOffset.Now) switch
    {
        TickerUrgency.Overdue => "지연",
        TickerUrgency.Imminent => "임박",
        _ => Pinned ? "고정" : ""
    };

    [JsonIgnore]
    public string RepeatLabel => Repeat switch
    {
        RepeatInterval.Daily => "매일",
        RepeatInterval.Weekly => "매주",
        RepeatInterval.Monthly => "매월",
        RepeatInterval.Yearly => "매년",
        _ => ""
    };

    public bool IsOverdue(DateTimeOffset now) =>
        Repeat != RepeatInterval.None && !Completed && Deadline <= now;

    // A daily item is always within 24 hours of its next deadline, so it warns only in the last hour.
    [JsonIgnore]
    public TimeSpan ImminentWindow => TimeSpan.FromHours(Repeat == RepeatInterval.Daily ? 1 : 24);

    public TickerUrgency UrgencyAt(DateTimeOffset now) =>
        IsOverdue(now) ? TickerUrgency.Overdue :
        !Completed && Deadline is { } deadline && deadline > now && deadline - now <= ImminentWindow
            ? TickerUrgency.Imminent : TickerUrgency.None;

    public TickerItem AdvanceIfCompleted()
    {
        if (!Completed || Repeat == RepeatInterval.None ||
            (DueDate?.ToDateTime(TimeOnly.MinValue) ?? DueAt?.LocalDateTime) is not { } time)
            return this;

        var next = Repeat switch
        {
            RepeatInterval.Daily => time.AddDays(1),
            RepeatInterval.Weekly => time.AddDays(7),
            RepeatInterval.Monthly => time.AddMonths(1),
            RepeatInterval.Yearly => time.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(Repeat))
        };
        int? repeatDay = null;
        if (Repeat is RepeatInterval.Monthly or RepeatInterval.Yearly)
        {
            repeatDay = RepeatDay ?? time.Day;
            int day = Math.Min(repeatDay.Value, DateTime.DaysInMonth(next.Year, next.Month));
            next = next.AddDays(day - next.Day);
        }

        var advanced = this with { Completed = false, RepeatDay = repeatDay };
        return DueDate is not null
            ? advanced with { DueDate = DateOnly.FromDateTime(next) }
            : advanced with { DueAt = new DateTimeOffset(next) };
    }
}

internal sealed record TickerState
{
    public TickerItem[] Items { get; init; } = [];
    public bool AlwaysOnTop { get; init; }
    public bool AtBottom { get; init; }
    public bool MoveLeftToRight { get; init; }
    public bool ShowCurrentDate { get; init; } = true;
    public string? Monitor { get; init; }

    public IEnumerable<TickerItem> ActiveItems(DateTimeOffset now) => Items
        .Where(item => !item.Completed &&
            (item.Deadline is not { } deadline || deadline > now || item.Repeat != RepeatInterval.None))
        .OrderBy(item => item.IsOverdue(now) ? 0 : item.CalendarDate is not null ? 2 : item.Pinned ? 1 : 3)
        .ThenBy(item => item.Deadline);

    public (string[] Labels, string? DateLabel, DateTimeOffset? NextRefresh, TickerUrgency[] Urgency)
        GetContent(DateTimeOffset now)
    {
        var items = ActiveItems(now).ToArray();
        var urgency = items.Select(item => item.UrgencyAt(now)).ToArray();
        var labels = items.Select((item, index) =>
        {
            string schedule = item.DisplayTime;
            string label = schedule.Length == 0 ? item.Text : $"{schedule}  {item.Text}";
            return urgency[index] switch
            {
                TickerUrgency.Overdue => $"[지연] {label}",
                TickerUrgency.Imminent => $"[임박] {label}",
                _ => label
            };
        });
        var nextRefresh = items
            .SelectMany(item => item.Deadline is { } deadline && deadline > now
                ? new[] { deadline, deadline - item.ImminentWindow } : [])
            .Where(time => time > now)
            .Select(time => (DateTimeOffset?)time).Min();
        string? dateLabel = null;
        if (ShowCurrentDate)
        {
            var localTime = now.LocalDateTime;
            var date = localTime.ToString("M월 d일", CultureInfo.GetCultureInfo("ko-KR"));
            dateLabel = $"{date} {TickerItem.WeekdayHanja(localTime.DayOfWeek)}";
            var midnight = new DateTimeOffset(localTime.Date.AddDays(1));
            if (nextRefresh is null || midnight < nextRefresh.Value)
                nextRefresh = midnight;
        }

        return (labels.ToArray(), dateLabel, nextRefresh, urgency);
    }

    public static TickerState Load(string path)
    {
        using FileStream file = File.OpenRead(path);
        var state = JsonSerializer.Deserialize<TickerState>(file);
        if (state is null || state.Items is null || state.Items.Any(item =>
            item is null || string.IsNullOrWhiteSpace(item.Text) || !Enum.IsDefined(item.Repeat) ||
            (item.Repeat != RepeatInterval.None && item.CalendarDate is null) ||
            (item.DueAt is not null && item.DueDate is not null) || item.RepeatDay is < 1 or > 31))
        {
            throw new JsonException("일정과 메모 파일의 형식이 올바르지 않습니다.");
        }

        return state;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + ".tmp";
        using (FileStream file = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(file, this);
            file.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }
}
