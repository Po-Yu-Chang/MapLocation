using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MapLocationApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("MapLocation Console Application Started");
        
        var host = CreateHostBuilder(args).Build();
        
        // Get the application service and run it
        var app = host.Services.GetRequiredService<ConsoleApp>();
        await app.RunAsync();
        
        Console.WriteLine("Application completed. Press any key to exit...");
        Console.ReadKey();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register services from MauiProgram but adapted for console
                services.AddSingleton<ConsoleApp>();
                services.AddLogging();
            });
}