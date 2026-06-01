namespace LaptopForensics.Core.Models;

public class AppSettings
{
    public StorageSettings Storage { get; set; } = new();
}

public class StorageSettings
{
    public string DatabasePath { get; set; } = @"C:\LaptopForensics\Data\scans.db";
    public int KeepHistoryDays { get; set; } = 90;
}
