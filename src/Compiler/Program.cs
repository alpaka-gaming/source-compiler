using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SourceSDK.Builders;
using SourceSDK.Interfaces;
using SourceSDK.Models;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SourceSDK
{
    public static class Program
    {

        #region IOC


        public static IConfiguration Configuration { get; private set; }
        public static ILogger Logger { get; private set; }

        public static IServiceCollection Services { get; private set; }
        public static IServiceProvider Container { get; private set; }


        #endregion

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;
#if DEBUG
            Console.WriteLine(@"Press any key to start...");
            Console.ReadKey();
            Console.Clear();
#endif

            // Arguments
            _options = Parser.Default.ParseArguments<Option>(args).Value;

            Initialize(args);

            using (var context = Container.GetService<ICompilerContext>())
            {
                if (context == null) throw new NullReferenceException("Unable to locate the compiler context");

                context.Options = Options;

                context.Progress += (_, e) =>
                {
                    Logger.LogInformation("Completed: {Progress} - State: {State}", e.ProgressPercentage + "%", e.UserState);
                };

                context.Compile(Options.Source);
            }

#if DEBUG
            Console.WriteLine(@"Press any key to end...");
            Console.ReadKey();
#endif

        }
        private static void Initialize(string[] args)
        {
            // Services

            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
#if DEBUG
                .AddJsonFile("appsettings.Development.json", true, true)
#endif
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .CreateLogger();

            Services = new ServiceCollection();
            Services.AddSingleton(Configuration);
            Services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddSerilog();
            }).AddOptions();

            Services.AddScoped<ICompilerContext, CompilerContext>();
            Services.AddScoped<IMaterialBuilder, MaterialBuilder>();
            Services.AddScoped<IModelBuilder, ModelBuilder>();
            Services.AddScoped<IMapBuilder, MapBuilder>();

            Container = Services.BuildServiceProvider();
            var factory = Container.GetService<ILoggerFactory>();
            if (factory != null)
                Logger = factory.CreateLogger(typeof(Program));

        }

        private static Option _options;
        public static Option Options => _options;
        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            if (Logger != null)
            {
                Logger.LogError(ex, "{Message}", ex.Message);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

    }
}
