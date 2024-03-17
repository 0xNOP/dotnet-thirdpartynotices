using DotnetThirdPartyNotices.Commands;
using DotnetThirdPartyNotices.Models;
using DotnetThirdPartyNotices.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("DotnetThirdPartyNotices", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = new CommandLineBuilder(new ScanCommand())
    .UseDefaults()
    .UseHost(Host.CreateDefaultBuilder, builder =>
    {
        builder.UseCommandHandler<ScanCommand, ScanCommand.Handler>();
        builder.ConfigureServices(x =>
        {
            x.AddLogging(x =>
            {
                x.ClearProviders();
                x.AddSerilog(dispose: true);
            });
            x.AddSingleton<ILicenseService, LicenseService>();
            x.AddSingleton<ILocalPackageService, LocalPackageService>();
            x.AddSingleton<IProjectService, ProjectService>();
            x.AddHttpClient();
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes();
            foreach (var type in types.Where(t => typeof(ILicenseUriLicenseResolver).IsAssignableFrom(t) && !t.IsInterface))
                x.AddSingleton(typeof(ILicenseUriLicenseResolver), type);
            foreach (var type in types.Where(t => typeof(IProjectUriLicenseResolver).IsAssignableFrom(t) && !t.IsInterface))
                x.AddSingleton(typeof(IProjectUriLicenseResolver), type);
            foreach (var type in types.Where(t => typeof(IRepositoryUriLicenseResolver).IsAssignableFrom(t) && !t.IsInterface))
                x.AddSingleton(typeof(IRepositoryUriLicenseResolver), type);
            foreach (var type in types.Where(t => typeof(IFileVersionInfoLicenseResolver).IsAssignableFrom(t) && !t.IsInterface))
                x.AddSingleton(typeof(IFileVersionInfoLicenseResolver), type);
        });
    })
    .Build();
await builder.InvokeAsync(args);
return 0;