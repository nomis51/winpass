﻿using System.Management;
using System.Management.Automation;
using Newtonsoft.Json;
using Serilog;
using WinPass.Core.Abstractions;
using WinPass.Shared.Extensions;
using WinPass.Shared.Models.Abstractions;
using WinPass.Shared.Models.Data;
using WinPass.Shared.Models.Errors.Gpg;

namespace WinPass.Core.Services;

public class GpgService : IService
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
            return lines.FirstOrDefault()?.StartsWith("gpg (GnuPG)") ?? false;
        }
        catch (Exception e)
        {
            Log.Warning("Unable to verify GPG installation: {Message}", e.Message);
            return false;
        }
    }

    public EmptyResult Encrypt(string path, string value)
    {
        return EncryptOne(path, value);
    }

    public Result<string, Error?> Decrypt(string path)
    {
        return DecryptOne(path);
    }

    public EmptyResult EncryptMetadatas(string path, MetadataCollection metadatas)
    {
        return EncryptOne(path, metadatas.ToString());
    }

    public EmptyResult EncryptPassword(string path, Password password)
    {
        return EncryptOne(path, password.ToString());
    }

    public Result<MetadataCollection?, Error?> DecryptMetadatas(string path)
    {
        var (data, error) = DecryptOne(path);
        if (error is not null) return new Result<MetadataCollection?, Error?>(error);

        try
        {
            var lstMetadata = JsonConvert.DeserializeObject<List<Metadata>>(data.FromBase64());
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
        var (lines, error) = DecryptMany(filePaths);
        if (error is not null) return new Result<List<MetadataCollection?>, Error?>(error);

        List<MetadataCollection?> results = new();
        for (var i = 0; i < lines.Count; ++i)
        {
            try
            {
                results.Add(
                    new MetadataCollection(
                        items[i].Item1,
                        JsonConvert.DeserializeObject<List<Metadata>>(lines[i].FromBase64())!
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
            : new Result<Password?, Error?>(new Password(path, data.FromBase64()));
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

    public void Initialize()
    {
    }

    #endregion

    #region Private methods

    private Result<List<string>, Error?> DecryptMany(IEnumerable<string> filePaths)
    {
        try
        {
            return new Result<List<string>, Error?>(
                GetPowerShellInstance(true)
                    .AddArgument("-Command")
                    .AddArgument("foreach($filePath in (" +
                                 string.Join(", ", filePaths) +
                                 ")) { gpg --quiet --yes --compress-algo=none --no-encrypt-to --decrypt $filePath; echo \"\" }")
                    .Invoke<string>()
                    .ToList()
            );
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
                )
            );
        }
        catch (Exception e)
        {
            Log.Error("Unable to decrypt: {Message}", e.Message);
            return new Result<string, Error?>(new GpgDecryptError(e.Message));
        }
    }

    private EmptyResult EncryptOne(string filePath, string value)
    {
        var (id, error) = AppService.Instance.GetStoreId();
        if (error is not null) return new EmptyResult(error);

        try
        {
            var pwsh = GetPowerShellInstance(true)
                .AddArgument("-Command")
                .AddArgument("Write-Output")
                .AddArgument(value)
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

    private PowerShell GetPowerShellInstance(bool usePwshDirectly = false)
    {
        var pwsh = PowerShell.Create();
        pwsh.Runspace.SessionStateProxy.Path.SetLocation(AppService.Instance.GetStoreLocation());
        pwsh.AddCommand(usePwshDirectly ? "pwsh" : GpgProcessName);
        return pwsh;
    }

    #endregion
}