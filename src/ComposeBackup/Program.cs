namespace ComposeBackup;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();

        builder.Configuration.SetBasePath("/config");
        builder.Services.Configure<Settings>(
            builder.Configuration.GetSection("Settings"));

        var host = builder.Build();
        host.Run();
    }
}