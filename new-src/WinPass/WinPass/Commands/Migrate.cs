﻿using System.Text.RegularExpressions;
using Spectre.Console;
using WinPass.Commands.Abstractions;
using WinPass.Shared;

namespace WinPass.Commands;

public class Migrate : ICommand
{
    #region Constants

    private static readonly Regex RegGpgKeyId =
        new("[a-z0-9]{40}|[a-z0-9]{16}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #endregion
    
    #region Public methods

    public void Run(List<string> args)
    {
        if (!Cli.AcquireLock()) return;

        var gpgId = AnsiConsole.Ask<string>($"{Locale.Get("questions.gpgId")}: ");

        var match = RegGpgKeyId.Match(gpgId);
        if (!match.Success || match.Index != 0 || match.Length != gpgId.Length)
        {
            AnsiConsole.MarkupLine($"[red]{Locale.Get("error.invalidGpgKey")}[/]");
            return;
        }
        
        var choiceConfirmDelete =
            AnsiConsole.Ask($"{Locale.Get("questions.confirmMigrateStore")}?",
                Locale.Get("n"));
        if (choiceConfirmDelete != Locale.Get("y")) return;
        
        // TODO: 
    }

    #endregion
}