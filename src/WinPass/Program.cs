﻿using System.Reflection;
using System.Text;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using WinPass.Core.Services;
using WinPass.Shared;
using WinPass.Shared.Helpers;

namespace WinPass;

public static class Program
{
    #region Public methods

    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        var dirName = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
        var logDir = Path.Join(dirName, "logs");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Join(logDir, ".txt"), LogEventLevel.Information,
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var (hasUpdate, _, newVersion) = UpdateHelper.CheckForUpdate().Result;
        if (hasUpdate)
        {
            AnsiConsole.MarkupLine(
                $"[green]New version {newVersion} available! Go to https://github.com/nomis51/winpass/releases/latest to download the update[/]");
        }

        UpdateHelper.EnsureAppLinked();

        try
        {
            var (settings, error) = AppService.Instance.GetSettings();
            if (error is null && settings is not null)
            {
                Locale.SetLanguage(settings.Language);
            }

            new Cli().Run(args);
        }
        catch (Exception e)
        {
            Log.Error("Unexpected error occured: {Message}{Skip}{CallStack}", e.Message, Environment.NewLine,
                e.StackTrace);
            AnsiConsole.MarkupLine($"[red]An error occured. Please see the logs at {logDir}[/]");
        }
    }

    #endregion
}