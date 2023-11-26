﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReactiveUI;
using WinPass.Core.Services;
using WinPass.Shared.Enums;
using WinPass.Shared.Models.Data;
using WinPass.UI.Models;
using WinPass.UI.Services;

namespace WinPass.UI.ViewModels;

public class EntryFormViewModel : ViewModelBase
{
    #region Props

    private string _entryName = string.Empty;

    public string EntryName
    {
        get => _entryName;
        set
        {
            this.RaiseAndSetIfChanged(ref _entryName, value);
            this.RaisePropertyChanged(nameof(HasEntryName));
        }
    }

    public bool HasEntryName => !string.IsNullOrWhiteSpace(EntryName);

    public ObservableCollection<MetadataModel> Metadatas { get; } = new();
    public ObservableCollection<MetadataModel> InternalMetadatas { get; } = new();
    private bool _metadatasRevealed;

    public bool MetadatasRevealed
    {
        get => _metadatasRevealed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _metadatasRevealed, value);
            this.RaisePropertyChanged(nameof(HasMetadatas));
        }
    }

    public bool HasMetadatas => Metadatas.Any() || InternalMetadatas.Any();
    public bool HasNormalMetadatas => Metadatas.Any();
    public int[] MetadatasPlaceholders { get; private set; } = { 0, 0, 0 };
    private string _password = string.Empty;

    public string Password
    {
        get => _password;
        set
        {
            this.RaiseAndSetIfChanged(ref _password, value);
            this.RaisePropertyChanged(nameof(IsPasswordValid));
        }
    }

    private string _confirmPassword = string.Empty;

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            this.RaiseAndSetIfChanged(ref _confirmPassword, value);
            this.RaisePropertyChanged(nameof(IsPasswordValid));
        }
    }

    public bool IsPasswordValid => Password == ConfirmPassword && !string.IsNullOrEmpty(Password);

    private bool _isEditingPassword;

    public bool IsEditingPassword
    {
        get => _isEditingPassword;
        private set => this.RaiseAndSetIfChanged(ref _isEditingPassword, value);
    }

    private double _passwordLength = 12;

    public double PasswordLength
    {
        get => _passwordLength;
        set => this.RaiseAndSetIfChanged(ref _passwordLength, value);
    }

    private bool _isGeneratingPassword;

    public bool IsGeneratingPassword
    {
        get => _isGeneratingPassword;
        set => this.RaiseAndSetIfChanged(ref _isGeneratingPassword, IsEditingPassword && value);
    }

    private bool _isPasswordVisible;

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => this.RaiseAndSetIfChanged(ref _isPasswordVisible, IsEditingPassword && value);
    }

    private string _customPasswordAlphabet = string.Empty;

    public string CustomPasswordAlphabet
    {
        get => _customPasswordAlphabet;
        set => this.RaiseAndSetIfChanged(ref _customPasswordAlphabet, value);
    }

    #endregion

    #region Constructors

    public EntryFormViewModel()
    {
        ResetPassword();
    }

    #endregion

    #region Public methods

    public void CopyOldPassword()
    {
        var (_, error) = AppService.Instance.GetPassword(EntryName);
        if (error is null) return;

        SnackbarService.Instance.Show("Unable to copy password", "warning");
    }

    public void GeneratePassword()
    {
        if (!IsEditingPassword) return;

        if (!IsGeneratingPassword)
        {
            IsGeneratingPassword = true;
        }

        var (password, error) = AppService.Instance.GenerateNewPassword(Convert.ToInt32(PasswordLength), CustomPasswordAlphabet);
        if (error is not null)
        {
            SnackbarService.Instance.Show("Unable generate password", "warning");
            return;
        }

        IsPasswordVisible = true;
        Password = password!.ValueAsString;
        ConfirmPassword = password.ValueAsString;
        password.Dispose();
    }

    public void SavePassword()
    {
        var result = AppService.Instance.EditPassword(EntryName, new Password(Password));
        if (result.HasError)
        {
            SnackbarService.Instance.Show("Unable to save password", "error", 5000);
            return;
        }

        SnackbarService.Instance.Show("Password saved", "success");
        CancelPassword();
    }

    public void EditPassword()
    {
        ClearPassword();
        IsEditingPassword = true;
    }

    public void CancelPassword()
    {
        IsEditingPassword = false;
        IsGeneratingPassword = false;
        IsPasswordVisible = false;
        ResetPassword();
    }

    public void SaveMetadatas()
    {
        var result = AppService.Instance.EditMetadatas(
            EntryName,
            new MetadataCollection(
                EntryName,
                InternalMetadatas.ToList()
                    .Concat(Metadatas.ToList())
                    .Select(e => new Metadata(e.Key, e.Value, e.Type))
                    .Where(m => !string.IsNullOrWhiteSpace(m.Key) || !string.IsNullOrWhiteSpace(m.Value))
                    .ToList()
            )
        );
        if (result.HasError)
        {
            SnackbarService.Instance.Show("Unable to save metadata", "error", 5000);
            return;
        }

        SnackbarService.Instance.Show("Metadata saved", "success");
        CancelMetadatas();
    }

    public void RemoveMetadata(Guid id)
    {
        var index = Metadatas.Select(m => m.Id).IndexOf(id);
        if (index == -1) return;

        Metadatas.RemoveAt(index);
        this.RaisePropertyChanged(nameof(HasNormalMetadatas));
    }

    public void SetEntryItem(string name)
    {
        EntryName = name;
    }

    public void RetrieveMetadatas()
    {
        var (metadatas, error) = AppService.Instance.GetMetadatas(EntryName);
        if (error is not null) return;

        foreach (var metadata in metadatas)
        {
            var item = new MetadataModel
            {
                Key = metadata.Key,
                Value = metadata.Value,
                Type = metadata.Type,
            };

            if (metadata.Type == MetadataType.Internal)
            {
                InternalMetadatas.Add(item);
            }
            else if (metadata.Type == MetadataType.Normal)
            {
                Metadatas.Add(item);
            }
        }

        MetadatasRevealed = true;
        this.RaisePropertyChanged(nameof(HasNormalMetadatas));
    }

    public void AddMetadata()
    {
        Metadatas.Add(new MetadataModel());
        this.RaisePropertyChanged(nameof(HasNormalMetadatas));
    }

    public void CancelMetadatas()
    {
        Metadatas.Clear();
        InternalMetadatas.Clear();
        MetadatasRevealed = false;
        this.RaisePropertyChanged(nameof(HasNormalMetadatas));
    }

    #endregion

    #region Private methods

    private void ClearPassword()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    private void ResetPassword()
    {
        Password = "************";
        ConfirmPassword = "************";
    }

    #endregion
}