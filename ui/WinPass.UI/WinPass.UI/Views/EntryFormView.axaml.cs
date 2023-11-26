﻿using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using WinPass.UI.Extensions;
using WinPass.UI.ViewModels;

namespace WinPass.UI.Views;

public partial class EntryFormView : ViewBase<EntryFormViewModel>
{
    #region Constructors

    public EntryFormView()
    {
        InitializeComponent();
    }

    #endregion

    #region Public methods

    public void SetEntryItem(string name)
    {
        ViewModel?.SetEntryItem(name);
    }

    #endregion

    #region Private methods

    private void ButtonRevealMetadatas_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.RetrieveMetadatas();
    }

    private void ButtonAddMetadata_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.AddMetadata();
    }

    private void ButtonRemoveMetadata_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.RemoveMetadata(sender!.GetTag<Guid>());
    }

    private void ButtonCancelMetadatas_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CancelMetadatas();
    }

    private void ButtonSaveMetadatas_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SaveMetadatas();
    }

    private void ButtonEditPassword_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.EditPassword();
    }

    private void ButtonCancelPassword_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CancelPassword();
    }

    private void ButtonSavePassword_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SavePassword();
    }

    private void SliderPasswordLength_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        SliderPasswordLength.Value = Math.Round(SliderPasswordLength.Value);
        ViewModel?.GeneratePassword();
    }

    private void ButtonGenerateNewPassword_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.GeneratePassword();
    }

    private void TextBoxCustomPasswordAlphabet_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ViewModel?.GeneratePassword();
    }

    private void ButtonCopyOldPassword_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CopyOldPassword();
    }

    #endregion
}