namespace LaptopForensics.Core.Helpers;

public static class SizeFormatter
{
    private const double Kb = 1024d;
    private const double Mb = 1024d * 1024d;
    private const double Gb = 1024d * 1024d * 1024d;

    public static string FormatBytes(long bytes)
    {
        if (bytes >= Gb)
        {
            return $"{bytes / Gb:0.00} GB";
        }

        if (bytes >= Mb)
        {
            return $"{bytes / Mb:0.00} MB";
        }

        if (bytes >= Kb)
        {
            return $"{bytes / Kb:0.00} KB";
        }

        return $"{bytes} B";
    }

    public static string FormatBytesShort(long bytes)
    {
        if (bytes >= Gb)
        {
            return $"{bytes / Gb:0.0}GB";
        }

        if (bytes >= Mb)
        {
            return $"{bytes / Mb:0.0}MB";
        }

        if (bytes >= Kb)
        {
            return $"{bytes / Kb:0.0}KB";
        }

        return $"{bytes}B";
    }
}
