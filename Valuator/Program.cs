using StackExchange.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);


        // Add services to the container.
        builder.Services.AddRazorPages();

        // аутентификация с помощью куки
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/Login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            });
        builder.Services.AddAuthorization();

        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASS");
        var redisOptions = new ConfigurationOptions
        {
            EndPoints = { "localhost:6379" },
            Password = redisPassword,
        };
        builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

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

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}
