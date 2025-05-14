using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);


        // Чтение переменных окружения
        var mainDbHost = Environment.GetEnvironmentVariable("DB_MAIN")/* ?? "localhost:6000"*/;
        var ruDbHost = Environment.GetEnvironmentVariable("DB_RU")/* ?? "localhost:6001"*/;
        var euDbHost = Environment.GetEnvironmentVariable("DB_EU")/* ?? "localhost:6002"*/;
        var asiaDbHost = Environment.GetEnvironmentVariable("DB_ASIA") /*?? "localhost:6003"*/;

        // Регистрация главного подключения Redis (DB_MAIN)
        builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(mainDbHost));

        // Регистрация сервисов Redis
        // Создание словаря с экземплярами IConnectionMultiplexer
        builder.Services.AddSingleton<IDictionary<string, IConnectionMultiplexer>>(sp =>
        {
            return new Dictionary<string, IConnectionMultiplexer>
            {
                { "RU", ConnectionMultiplexer.Connect(ruDbHost) },
                { "EU", ConnectionMultiplexer.Connect(euDbHost) },
                { "ASIA", ConnectionMultiplexer.Connect(asiaDbHost) }
            };
        });
        //builder.Services.AddSingleton(multiplexers);

        // Add services to the container.
        builder.Services.AddRazorPages();

        //builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379"));

        var app = builder.Build();

        var port = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(':').LastOrDefault() ?? "5000";

        app.Logger.LogInformation("Application is running on port {Port}", port);
        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}
