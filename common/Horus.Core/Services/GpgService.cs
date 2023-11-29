﻿using System.Management.Automation;
using Horus.Core.Services.Abstractions;
using Horus.Shared.Extensions;
using Horus.Shared.Models.Abstractions;
using Horus.Shared.Models.Data;
using Horus.Shared.Models.Errors.Gpg;
using Newtonsoft.Json;
using Serilog;

namespace Horus.Core.Services;

public class GpgService : IGpgService
{
    #region Constants

    private const string GpgProcessName = "gpg";

    #endregion

    #region Public methods

    public bool Verify()
    {
        try
        {
            var lines = GetPowerShellInstance()
                .AddArgument("--version")
                .Invoke<string>();
            var ok = lines.FirstOrDefault()?.StartsWith("gpg (GnuPG)") ?? false;
            if (!ok) return false;

            PowerShell.Create()
                .AddCommand("gpg-connect-agent")
                .AddArgument("reloadagent")
                .AddArgument("/bye")
                .Invoke();

            if (ok)
            {
                StartGpgAgent();
            }

            return ok;
        }
        catch (Exception e)
        {
            Log.Warning("Unable to verify GPG installation: {Message}", e.Message);
            return false;
        }
    }

    public EmptyResult Encrypt(string path, string value, string gpgId = "")
    {
        return EncryptOne(path, value, gpgId);
    }

    public Result<string, Error?> Decrypt(string path)
    {
        return DecryptOne(path);
    }

    public EmptyResult EncryptMetadatas(string path, MetadataCollection metadatas, string gpgId = "")
    {
        return EncryptOne(path, metadatas.ToString(), gpgId);
    }

    public EmptyResult EncryptPassword(string path, Password password, string gpgId = "")
    {
        return EncryptOne(path, password.ToString(), gpgId);
    }

    public Result<MetadataCollection?, Error?> DecryptMetadatas(string path)
    {
        var (data, error) = DecryptOne(path);
        if (error is not null) return new Result<MetadataCollection?, Error?>(error);

        try
        {
            var lstMetadata = JsonConvert.DeserializeObject<List<Metadata>>(data);
            return lstMetadata is null
                ? new Result<MetadataCollection?, Error?>(new GpgDecryptError("Resulting data was null"))
                : new Result<MetadataCollection?, Error?>(new MetadataCollection(path, lstMetadata));
        }
        catch (Exception e)
        {
            Log.Error("Unable to deserialize metadatas: {Message}", e.Message);
            return new Result<MetadataCollection?, Error?>(new GpgDecryptError(e.Message));
        }
    }

    public Result<List<MetadataCollection?>, Error?> DecryptManyMetadatas(List<Tuple<string, string>> items)
    {
        var filePaths = items.Select(i => i.Item2);

        List<string> lines = new();
        foreach (var filePath in filePaths)
        {
            var (line, error) = DecryptOne(filePath);
            lines.Add(error is not null ? string.Empty : line);
        }

        List<MetadataCollection?> results = new();
        for (var i = 0; i < lines.Count; ++i)
        {
            try
            {
                results.Add(
                    new MetadataCollection(
                        items[i].Item1,
                        JsonConvert.DeserializeObject<List<Metadata>>(lines[i])!
                    )
                );
            }
            catch (Exception e)
            {
                Log.Error("Unable to deserialize metadatas: {Message}", e.Message);
                results.Add(default);
            }
        }

        return new Result<List<MetadataCollection?>, Error?>(results);
    }

    public Result<Password?, Error?> DecryptPassword(string path)
    {
        var (data, error) = DecryptOne(path);
        return error is not null
            ? new Result<Password?, Error?>(error)
            : new Result<Password?, Error?>(new Password(data));
    }

    public ResultStruct<bool, Error?> IsIdValid(string id = "")
    {
        try
        {
            var lines = GetPowerShellInstance()
                .AddArgument("--list-keys")
                .Invoke<string>();

            if (lines.FirstOrDefault()?.StartsWith("gpg: error reading key: No public key") ?? true)
                return new ResultStruct<bool, Error?>(new GpgKeyNotFoundError());
            if (!lines.Any(e => e.StartsWith("pub")))
                return new ResultStruct<bool, Error?>(new GpgKeyNotFoundError());

            if (string.IsNullOrEmpty(id))
            {
                var (currentId, error) = AppService.Instance.GetStoreId();
                if (error is not null) return new ResultStruct<bool, Error?>(error);
                id = currentId;
            }

            // TODO: fix this to validdate the actual ID, not the first line coming out of GPG
            const string expireTag = "[E]";
            const string expireLabel = "[expires: ";
            var expireLine = lines.FirstOrDefault(l => l.Contains(expireTag));
            if (string.IsNullOrEmpty(expireLine)) return new ResultStruct<bool, Error?>(new GpgInvalidKeyError());

            if (!expireLine.Contains(expireLabel)) return new ResultStruct<bool, Error?>(true);

            var startIndex = expireLine.IndexOf(expireLabel, StringComparison.Ordinal);
            if (startIndex == -1) return new ResultStruct<bool, Error?>(new GpgInvalidKeyError());

            startIndex += expireLabel.Length;

            var endIndex = expireLine.LastIndexOf("]", StringComparison.Ordinal);
            if (endIndex == -1 || endIndex <= startIndex)
                return new ResultStruct<bool, Error?>(new GpgInvalidKeyError());

            var date = expireLine[startIndex..endIndex];
            return !DateTime.TryParse(date, out var dateTime)
                ? new ResultStruct<bool, Error?>(new GpgInvalidKeyError())
                : new ResultStruct<bool, Error?>(dateTime > DateTime.Now);
        }
        catch (Exception e)
        {
            Log.Error("Unable to verify GPG ID: {Message}", e.Message);
            return new ResultStruct<bool, Error?>(new GpgKeyNotFoundError());
        }
    }

    public Result<string, Error?> RestartGpgAgent()
    {
        var (result, error) = StopGpgAgent();
        if (error is not null) return new Result<string, Error?>(error);

        var (result2, error2) = StartGpgAgent();
        if (error2 is not null) return new Result<string, Error?>(error2);

        return new Result<string, Error?>(string.Join("\n", result, result2));
    }

    public Result<string, Error?> StartGpgAgent()
    {
        try
        {
            var lines = PowerShell.Create()
                .AddCommand("gpg-connect-agent")
                .AddArgument("reloadagent")
                .AddArgument("/bye")
                .Invoke<string>();
            return lines.FirstOrDefault(l => l.StartsWith("OK")) is not null
                ? new Result<string, Error?>(string.Join(", ", lines))
                : new Result<string, Error?>(new GpgDecryptError("Unable to start GPG agent"));
        }
        catch (Exception e)
        {
            return new Result<string, Error?>(new GpgDecryptError(e.Message));
        }
    }

    public Result<string, Error?> StopGpgAgent()
    {
        try
        {
            PowerShell.Create()
                .AddCommand("gpgconf")
                .AddArgument("--kill")
                .AddArgument("gpg-agent")
                .Invoke();
            return new Result<string, Error?>("");
        }
        catch (Exception e)
        {
            return new Result<string, Error?>(new GpgDecryptError(e.Message));
        }
    }

    public void Initialize()
    {
    }

    #endregion

    #region Private methods

    private Result<List<string>, Error?> DecryptMany(IEnumerable<string> filePaths)
    {
        try
        {
            var scriptBlock = ScriptBlock.Create(
                "@(" + string.Join(",", filePaths.Select(f => $"\"{f}\"")) +
                ") | ForEach-Object -Parallel { gpg --quiet --yes --compress-algo=none --no-encrypt-to --decrypt $_; echo \"\"}"
            );
            var pwsh = GetPowerShellInstance(noSetup: true);
            pwsh.AddCommand("Invoke-Command")
                .AddArgument(scriptBlock);
            var lines = pwsh.Invoke<string>()
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            if (!lines.Any())
            {
                return new Result<List<string>, Error?>(new GpgDecryptError(string.Join("\n",
                    pwsh.Streams.Error.ReadAll().Select(e => e.Exception.Message))));
            }

            return new Result<List<string>, Error?>(lines);
        }
        catch (Exception e)
        {
            Log.Error("Unable to decrypt many: {Message}", e.Message);
            return new Result<List<string>, Error?>(new GpgDecryptError(e.Message));
        }
    }

    private Result<string, Error?> DecryptOne(string filePath)
    {
        try
        {
            var pwsh = GetPowerShellInstance()
                .AddArgument("--quiet")
                .AddArgument("--yes")
                .AddArgument("--compress-algo=none")
                .AddArgument("--no-encrypt-to")
                .AddArgument("--decrypt")
                .AddArgument(filePath);
            var lines = pwsh.Invoke<string>().ToList();
            lines.AddRange(pwsh.Streams.Error.ReadAll().Select(e => e.Exception.Message));

            return new Result<string, Error?>(
                string.Join(
                    string.Empty,
                    lines
                ).FromBase64()
            );
        }
        catch (Exception e)
        {
            Log.Error("Unable to decrypt: {Message}", e.Message);
            return new Result<string, Error?>(new GpgDecryptError(e.Message));
        }
    }

    private EmptyResult EncryptOne(string filePath, string value, string gpgId = "")
    {
        string id;

        if (string.IsNullOrEmpty(gpgId))
        {
            var (gid, error) = AppService.Instance.GetStoreId();
            if (error is not null) return new EmptyResult(error);

            id = gid;
        }
        else
        {
            id = gpgId;
        }

        try
        {
            var pwsh = GetPowerShellInstance(true)
                .AddArgument("-Command")
                .AddArgument("Write-Output")
                .AddArgument(value.ToBase64())
                .AddArgument("|")
                .AddArgument(GpgProcessName)
                .AddArgument("--quiet")
                .AddArgument("--yes")
                .AddArgument("--compress-algo=none")
                .AddArgument("--no-encrypt-to")
                .AddArgument("--encrypt")
                .AddArgument("--recipient")
                .AddArgument(id)
                .AddArgument("--output")
                .AddArgument(filePath);
            pwsh.Invoke<string>();
            var errors = pwsh.Streams.Error.ReadAll().Select(e => e.Exception.Message).ToList();

            if (errors.FirstOrDefault(e => e.Contains("encryption failed")) is not null)
            {
                return new EmptyResult(new GpgEncryptError(string.Join("\n", errors)));
            }

            return new EmptyResult();
        }
        catch (Exception e)
        {
            Log.Error("Unable to encrypt: {Message}", e.Message);
            return new EmptyResult(new GpgEncryptError(e.Message));
        }
    }

    private PowerShell GetPowerShellInstance(bool usePwshDirectly = false, bool noSetup = false)
    {
        var pwsh = PowerShell.Create();
        if (noSetup) return pwsh;

        pwsh.Runspace.SessionStateProxy.Path.SetLocation(AppService.Instance.GetStoreLocation());
        pwsh.AddCommand(usePwshDirectly ? "pwsh" : GpgProcessName);
        return pwsh;
    }

    #endregion
}