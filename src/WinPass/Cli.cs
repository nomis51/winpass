﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;
using Spectre.Console;
using WinPass.Core.Services;
using WinPass.Core.WinApi;
using WinPass.Shared.Enums;
using WinPass.Shared.Models.Fs;

namespace WinPass;

public class Cli
{
    #region Constants

    private static readonly Regex RegGpgKeyId =
        new("[a-z0-9]{40}|[a-z0-9]{16}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #endregion

    #region Public methods

    public void Run(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No arguments provided[/]");
            return;
        }

        var commandArgs = args.Skip(1).ToList();
        switch (args[0])
        {
            case "init":
                Init(commandArgs);
                break;

            case "ls":
            case "list":
                List();
                break;

            case "show":
                Show(commandArgs);
                break;

            case "insert":
            case "add":
                Insert(commandArgs);
                break;

            case "edit":
                Edit(commandArgs);
                break;

            case "grep":
            case "find":
            case "search":
                Search(commandArgs);
                break;

            case "generate":
                Generate(commandArgs);
                break;

            case "delete":
            case "remove":
                Delete(commandArgs);
                break;

            case "rename":
            case "move":
                Rename(commandArgs);
                break;

            case "help":
                Help();
                break;

            case "cc":
                ClearClipboard(commandArgs);
                break;
        }
    }

    #endregion

    #region Commands

    private void Help()
    {
        Table table = new()
        {
            Border = TableBorder.Rounded
        };
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.AddColumn("Example");

        table.AddRow(
            "winpass init [gpg-id]".EscapeMarkup(),
            "Initialize the password store using the GPG ID provided",
            "winpass init A034B347F727EAA5"
        );
        table.AddRow(
            "winpass (list|ls|*blank*)".EscapeMarkup(),
            "Show the list of passwords in the store",
            "winpass list"
        );
        table.AddRow(
            "winpass show [args] [name]".EscapeMarkup(),
            "Show the password requested by [name]\n\nArguments:\n".EscapeMarkup() + string.Join("\n",
                "-c : Copy the password to the clipboard instead of showing it",
                "-m : Also show metadata of the password if any",
                "-f : Don't automatically clear the terminal after a while"
            ),
            "winpass show -c -m github/work"
        );
        table.AddRow(
            "winpass (insert|add) [name]".EscapeMarkup(),
            "Insert a new password named [name]",
            "winpass add -m github/work"
        );
        table.AddRow(
            "winpass generate [args] [name]".EscapeMarkup(),
            "Generate a new password named [name]\n\nArguments:\n".EscapeMarkup() + string.Join("\n",
                "-s : Size of the password (default: 20)",
                "-a : Custom alphabet to generate the password",
                "-c : Copy the password to the clipboard instead of showing it",
                "-f : Don't automatically clear the terminal after a while"
            ),
            "winpass generate -s=12 -a=abc123 -c github/work"
        );
        table.AddRow(
            "winpass (remove|delete) [name]".EscapeMarkup(),
            "Delete the password named [name]",
            "winpass remove github/work"
        );
        table.AddRow(
            "winpass (rename|move) [args] [name]".EscapeMarkup(),
            "Rename or duplicate the password named [name]\n\nArguments:\n".EscapeMarkup() + string.Join("\n",
                "-d : Duplicate the password named [name] instead of renaming it"
            ),
            "winpass generate -s=12 -a=abc123 -c github/work"
        );
        table.AddRow(
            "winpass (find|search|grep) [text]".EscapeMarkup(),
            "Find passwords or metadata containing [text]" +
            "winpass find \"email: my-email@github.com\""
        );
        table.AddRow(
            "winpass help",
            "Show the help (this)",
            "winpass help"
        );

        AnsiConsole.Write(table);
    }

    private void Edit(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Password name argument required[/]");
            return;
        }

        var name = args.Last();
        var (_, error) = AppService.Instance.EditPassword(name);

        if (error is null) return;

        AnsiConsole.MarkupLine($"[{GetErrorColor(error.Severity)}]{error.Message}[/]");
    }

    private void Rename(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Password name argument required[/]");
            return;
        }

        var name = args.Last();
        var duplicate = false;

        for (var i = 0; i < args.Count - 1; ++i)
        {
            if (args[i] == "-d")
            {
                duplicate = true;
            }
        }

        AnsiConsole.Write("Enter the new name: ");
        var newName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(newName))
        {
            AnsiConsole.MarkupLine("[red]The name was empty[/]");
            return;
        }

        AnsiConsole.MarkupLine(
            $"Are you sure you want to {(duplicate ? "duplicate" : "rename")} the password [blue]{name}[/] into [yellow]{newName}[/]?");
        AnsiConsole.Write("(y/n) > ");

        var key = Console.ReadKey();
        Console.WriteLine();
        if (key.Key != ConsoleKey.Y) return;

        var (_, error) = AppService.Instance.RenamePassword(name, newName, duplicate);

        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{GetErrorColor(error.Severity)}]{error.Message}[/]");
            return;
        }

        AnsiConsole.MarkupLine(duplicate
            ? $"Password [blue]{name}[/] duplicated to [blue]{newName}[/]"
            : $"Password [blue]{name}[/] renamed to [blue]{newName}[/]");
    }

    private void Delete(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Password name argument required[/]");
            return;
        }

        var name = args.Last();

        AnsiConsole.MarkupLine($"Are you sure you want to delete de password [blue]{name}[/]?");
        AnsiConsole.Write("(y/n) > ");

        var key = Console.ReadKey();
        Console.WriteLine();
        if (key.Key != ConsoleKey.Y) return;

        var (_, error) = AppService.Instance.DeletePassword(name);

        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{GetErrorColor(error.Severity)}]{error.Message}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"Password [blue]{name}[/] removed");
    }

    private void Generate(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Password name argument required[/]");
            return;
        }

        var name = args.Last();
        var length = 0;
        var customAlphabet = string.Empty;
        var copy = true;
        var dontClear = false;

        for (var i = 0; i < args.Count - 1; ++i)
        {
            if (args[i].StartsWith("-s="))
            {
                var value = args[i].Split("=").Last();
                if (!int.TryParse(value, out var intValue)) continue;

                length = intValue;
            }

            if (args[i].StartsWith("-a="))
            {
                var value = args[i].Split("=").Last();
                if (string.IsNullOrWhiteSpace(value)) continue;

                customAlphabet = value;
            }

            if (args[i] == "-c")
            {
                copy = false;
            }

            if (args[i] == "-f")
            {
                dontClear = true;
            }
        }

        var (password, error) = AppService.Instance.GeneratePassword(name, length, customAlphabet, copy);

        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{GetErrorColor(error.Severity)}]{error.Message}[/]");
            return;
        }

        if (copy)
        {
            AnsiConsole.MarkupLine($"Password for [blue]{name}[/] generated and [yellow]copied[/]");
            AnsiConsole.MarkupLine("[yellow]Clipboard will be cleared in 10 seconds[/]");
            return;
        }

        AnsiConsole.MarkupLine($"Password for [blue]{name}[/] generated");

        Table passwordTable = new()
        {
            Border = TableBorder.Rounded,
            ShowHeaders = false,
            Expand = false,
        };

        passwordTable.AddColumn(string.Empty);
        passwordTable.AddRow($"Password is [yellow]{password.EscapeMarkup()}[/]");

        AnsiConsole.Write(passwordTable);

        if (dontClear) return;
        AnsiConsole.MarkupLine("[yellow]Terminal will clear in 10 seconds[/]");

        Thread.Sleep(10 * 1000);
        Console.Clear();
    }

    private void Search(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Search term argument required[/]");
            return;
        }

        var term = args[0];
        var entries = AppService.Instance.Search(term);
        var tree = new Tree(string.Empty);
        RenderEntries(entries, tree);
        AnsiConsole.Write(tree);
    }

    private void Insert(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]password name argument required[/]");
            return;
        }

        var name = args[0];

        if (AppService.Instance.DoEntryExists(name))
        {
            AnsiConsole.MarkupLine($"[red]Password for {name} already exists[/]");
            return;
        }

        var password = ReadPassword();
        if (string.IsNullOrEmpty(password))
        {
            AnsiConsole.MarkupLine("[yellow]Password was empty[/]");
            return;
        }

        var (_, error) = AppService.Instance.InsertPassword(name, password);
        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{GetErrorColor(error.Severity)}]{error.Message}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"Password for [blue]{name}[/] created");
    }

    private void ClearClipboard(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return;
        if (!int.TryParse(args[0], out var intValue)) return;

        Thread.Sleep(1000 * intValue);
        User32.ClearClipboard();
    }

    private void Show(List<string> args)
    {
        if (args.Count == 0 || string.IsNullOrEmpty(args[0]))
        {
            AnsiConsole.MarkupLine("[red]password name argument required[/]");
            return;
        }

        var name = args[^1];
        args.RemoveAt(args.Count - 1);

        var copy = false;
        var dontClear = false;
        var showMetadata = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "-c":
                    copy = true;
                    continue;
                case "-f":
                    dontClear = true;
                    break;

                case "-m":
                    showMetadata = true;
                    break;
            }
        }


        var (password, error) = AppService.Instance.GetPassword(name, copy);
        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{GetErrorColor(error.Severity)}]{error.Message}[/]");
            return;
        }

        if (copy)
        {
            AnsiConsole.MarkupLine("Password copied");
            AnsiConsole.MarkupLine("[yellow]Clipboard will be cleared in 10 seconds[/]");
            return;
        }

        if (password is null) return;

        Table passwordTable = new()
        {
            Border = TableBorder.Rounded,
            ShowHeaders = false,
        };

        passwordTable.AddColumn(string.Empty);
        passwordTable.AddRow($"Password is [yellow]{password.Value.EscapeMarkup()}[/]");
       
        if (showMetadata && password.Metadata.Any())
        {
            Table metadataTable = new()
            {
                Border = TableBorder.Rounded,
            };
            metadataTable.AddColumn("Key");
            metadataTable.AddColumn("Value");

            foreach (var metadata in password.Metadata)
            {
                metadataTable.AddRow(metadata.Key, metadata.Value);
            }

            AnsiConsole.Write(metadataTable);
        }
        
        AnsiConsole.Write(passwordTable);

        if (dontClear) return;

        AnsiConsole.MarkupLine("[yellow]Terminal will clear in 10 seconds[/]");

        Thread.Sleep(10 * 1000);
        Console.Clear();
    }

    private void List()
    {
        var (entries, error) = AppService.Instance.ListStoreEntries();
        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{error.Severity}]{error.Message}[/]");
            return;
        }

        if (entries is null) return;

        var tree = new Tree(string.Empty);
        RenderEntries(entries, tree);
        AnsiConsole.Write(tree);
    }

    private void Init(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || string.IsNullOrEmpty(args[0]))
        {
            AnsiConsole.MarkupLine("[red]GPG key ID argument required[/]");
            return;
        }

        var match = RegGpgKeyId.Match(args[0]);
        if (!match.Success || match.Index != 0 || match.Length != args[0].Length)
        {
            AnsiConsole.MarkupLine("[red]Invalid GPG key ID provided[/]");
            return;
        }

        var (_, error) = AppService.Instance.InitializeStoreFolder(args[0]);
        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[{error.Severity}]{error.Message}[/]");
        }
    }

    #endregion

    #region Private methods

    private void RenderEntries(List<StoreEntry> entries, IHasTreeNodes node)
    {
        foreach (var entry in entries)
        {
            var style = string.Join(
                " ",
                new[]
                {
                    entry.Highlight ? "underline" : string.Empty,
                    entry.IsFolder ? string.Empty : "blue"
                }.Where(v => !string.IsNullOrEmpty(v))
            );
            var n = node.AddNode(
                string.IsNullOrWhiteSpace(style) ? entry.Name : $"[{style}]{entry.Name}[/]"
            );

            foreach (var metadata in entry.Metadata)
            {
                n.AddNode($"[green]{metadata}[/]");
            }

            if (!entry.IsFolder) continue;

            if (!entry.Entries.Any())
            {
                n.AddNode("...");
                continue;
            }

            RenderEntries(entry.Entries, n);
        }
    }

    private string GetErrorColor(ErrorSeverity severity)
    {
        return severity switch
        {
            ErrorSeverity.Error => "red",
            ErrorSeverity.Info => "blue",
            ErrorSeverity.Success => "green",
            ErrorSeverity.Warning => "yellow",
            _ => "white",
        };
    }

    private string ReadPassword()
    {
        var password = string.Empty;
        ConsoleKey key;
        AnsiConsole.WriteLine("Enter the password: ");
        do
        {
            var keyInfo = Console.ReadKey(intercept: true);
            key = keyInfo.Key;

            if (key == ConsoleKey.Backspace && password.Length > 0)
            {
                Console.Write("\b \b");
                password = password[..^1];
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                Console.Write("*");
                password += keyInfo.KeyChar;
            }
        } while (key != ConsoleKey.Enter);

        var (_, top) = Console.GetCursorPosition();
        Console.SetCursorPosition(0, top - 2 < 0 ? 0 : top - 2);

        for (var i = 0; i < 2; ++i)
        {
            for (var k = 0; k < Console.WindowWidth; ++k)
            {
                Console.Write(" ");
            }
        }

        return password;
    }

    #endregion
}