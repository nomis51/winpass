﻿using Spectre.Console;
using WinPass.Shared.Abstractions;
using WinPass.Shared.Extensions;
using WinPass.Shared.Helpers;
using WinPass.Shared.Models;
using WinPass.Shared.Models.Abstractions;
using WinPass.Shared.Models.Errors.Gpg;

namespace WinPass.Core.Services;

public class GpgService : IService
{
    #region Constants

    private const string Gpg = "gpg";

    #endregion

    #region Public methods

    public ResultStruct<byte, Error?> Encrypt(string key, string filePath, string value)
    {
        var (ok, result, error) = ProcessHelper.Exec("cmd", new[]
        {
            "/c",
            "echo",
            value.ToBase64(),
            "|",
            Gpg,
            "--quiet",
            "--yes",
            "--compress-algo=none",
            "--no-encrypt-to",
            "-e",
            "-r",
            key,
            "-o",
            filePath
        });
        return !ok ? new ResultStruct<byte, Error?>(new GpgEncryptError(error)) : new ResultStruct<byte, Error?>(0);
    }

    public Result<Password?, Error?> Decrypt(string filePath)
    {
        var (ok, result, error) = ProcessHelper.Exec(Gpg, new[]
        {
            "--quiet",
            "--yes",
            "--compress-algo=none",
            "--no-encrypt-to",
            "-d",
            filePath
        });
        if (!ok) return new Result<Password?, Error?>(new GpgDecryptError(error));

        if (string.IsNullOrWhiteSpace(result)) return new Result<Password?, Error?>(new GpgDecryptError(error));

        var lines = result.FromBase64().Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!lines.Any()) return new Result<Password?, Error?>(new GpgEmptyPasswordError());

        Password password = new(lines.First());

        for (var i = 1; i < lines.Length; ++i)
        {
            var parts = lines[i].Split(":", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            password.Metadata.Add(new Metadata(parts[0], string.Join(string.Empty, parts.Skip(1))));
        }

        return new Result<Password?, Error?>(password);
    }

    public bool DoKeyExists(string key)
    {
        var (ok, result, error) = ProcessHelper.Exec(Gpg, new[] { "--list-keys", key });
        if (error.StartsWith("gpg: error reading key: No public key")) return false;
        if (ok) return string.IsNullOrEmpty(error) && result.StartsWith("pub");

        AnsiConsole.MarkupLine("[red]Unable to verify GPG key[/]");
        return false;
    }

    public void Initialize()
    {
    }

    #endregion
}