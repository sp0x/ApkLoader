using System;
using System.IO;
using AppLoader.Services;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Log = Serilog.Log;

namespace AppLoader
{
    public class Program
    {
        private static CommandOption apkStoreHost;

        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            //Cli
            var cli = new CommandLineApplication(throwOnUnexpectedArg: true);
            apkStoreHost = cli.Option("--host", "The APK Store Hostname", CommandOptionType.SingleValue);
            cli.HelpOption("-? | --help");
         //   cli.Command("apkloader", (command) => { command.OnExecute(() => Execute(serviceProvider)); });
            
            cli.OnExecute(() => Execute(serviceProvider));
            cli.Execute(args);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings{environment}.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<SecureSettings>()
                .AddEnvironmentVariables();
            IConfiguration config = builder.Build();
            var appSettings = config.GetSection("AppSettings");
            var secureSettings = config.GetSection("apkloader");
            //services.Configure<AppSettings>(appSettings);
            services.Configure<SecureSettings>(secureSettings);
            services.AddTransient<AppService>();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("app.log")
                .CreateLogger();
            services.AddLogging(c => c.AddSerilog());
            if (config["LOG_LEVEL"] == "true")
            {
                services.Configure<LoggerFilterOptions>(options =>
                    options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Trace);
            }
            else
            {
                services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
            }
        }

        private static int Execute(IServiceProvider services)
        {
            services.GetService<AppService>().Run();
            return 0;
        }
    }
}