using Microsoft.Extensions.Options;

using System.Diagnostics;

namespace ComposeBackup;

record class Backup(string Path, Entry Entry);
public partial class Worker : BackgroundService
{
    readonly ILogger<Worker> _logger;
    readonly Settings _settings;
    readonly List<Backup> _backups;

    public Worker(ILogger<Worker> logger, IOptions<Settings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        _backups = _settings.Backups
            .Select(b => new Backup(b.Key, b.Value))
            .Where(b => b.Entry.Cron.GetNextOccurrence(DateTime.UtcNow) is not null)
            .ToList();
    }

    void OrderBackups(DateTime now)
    {
        _backups.Sort((a, b) =>
        {
            var first = a.Entry.Cron.GetNextOccurrence(now);
            var second = b.Entry.Cron.GetNextOccurrence(now);
            return (first, second) switch
            {
                (null, null) => 0,
                (_, null) => -1,
                (null, _) => 1,
                (var l, var r) => l.Value.CompareTo(r.Value)
            };
        });
    }

    async Task Backup(Backup backup)
    {
        List<string> args = ["backup", $"--tag='{backup.Path}'", backup.Path];
        await RunCommand("restic", args, null);
    }

    async Task Prune(Backup backup)
    {
        _ = _logger;
        _ = backup;
    }

    async Task StopAll()
    {
        foreach (var dir in Directory.EnumerateDirectories(_settings.ComposeDirectory))
        {
            await RunCommand("docker", ["compose", "down"], dir);
        }
    }

    async Task StartAll()
    {
        foreach (var dir in Directory.EnumerateDirectories(_settings.ComposeDirectory))
        {
            await RunCommand("docker", ["compose", "up"], dir);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Running with {Settings}", _settings);

        if (_backups.Count == 0)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var last = DateTime.UtcNow;
            OrderBackups(last);
            var next = _backups[0].Entry.Cron.GetNextOccurrence(last)!.Value;

            _logger.LogInformation("Next job is {tag} at {datetime}", _backups[0].Path, next);

            try
            {
                var delay = next.Subtract(DateTime.UtcNow);
                await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, stoppingToken);
            }
            catch (TaskCanceledException)
            {

                return;
            }

            var batch = _backups
                .Where(b =>
                        b.Entry.Cron.GetNextOccurrence(last)!.Value < DateTime.UtcNow.AddMinutes(_settings.BatchMinutes))
                .ToList();

            await StopAll();

            foreach (var backup in batch)
                await Backup(backup);

            await StartAll();

            foreach (var backup in batch)
                await Prune(backup);
        }
    }

    async Task RunCommand(string command, IEnumerable<string> args, string? cwd)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = command,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (cwd is not null)
            startInfo.WorkingDirectory = cwd;
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        var processName = cwd switch
        {
            null => string.Join(" ", [command, .. args]),
            var _ => $"{string.Join(" ", [command, .. args])} in {cwd}"
        };

        _logger.LogInformation("Running {command}", processName);

        if (_settings.DryRun)
            return;

        var process = Process.Start(startInfo) ?? throw new Exception($"failed to start {processName}");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new Exception($"{processName} exited with an error")
            {
                Data = {
                    { "exitcode", process.ExitCode },
                    { "stderr", stderr }
                }
            };
        }
    }
}