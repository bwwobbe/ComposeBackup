using Cronos;

namespace ComposeBackup;

public class Settings
{
    public Dictionary<string, Entry> Backups { get; set; } = [];
}

public class Entry
{
    public string When { get; init; } = string.Empty;
    public CronExpression Cron => CronExpression.Parse(When);
    public Keep Keep = new();
}

public class Keep
{
    public int Last { get; init; }
    public int Days { get; init; }
    public int Weeks { get; init; }
    public int Months { get; init; }
    public int Years { get; init; }
}