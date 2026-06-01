using System.Management;

namespace LaptopForensics.Core.Helpers;

public static class DateTimeHelper
{
    public static DateTime? ParseWmiDate(string wmiDate)
    {
        if (string.IsNullOrWhiteSpace(wmiDate))
        {
            return null;
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(wmiDate);
        }
        catch
        {
            return null;
        }
    }

    public static string FormatRelative(DateTime dt)
    {
        var span = DateTime.Now - dt;

        if (span.TotalSeconds < 30)
        {
            return "Just now";
        }

        if (span.TotalMinutes < 60)
        {
            var minutes = Math.Max(1, (int)Math.Floor(span.TotalMinutes));
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (span.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)Math.Floor(span.TotalHours));
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        var days = Math.Max(1, (int)Math.Floor(span.TotalDays));
        return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
    }

    public static string FormatDuration(long ms)
    {
        if (ms < 60_000)
        {
            return $"{ms / 1000d:0.0} sec";
        }

        var totalSeconds = (int)Math.Round(ms / 1000d);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes} min {seconds} sec";
    }
}
