using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horus.Core.Services;
using Horus.UI.Enums;
using Horus.UI.Services;
using ReactiveUI;

namespace Horus.UI.ViewModels;

public class SyncButtonViewModel : ViewModelBase
{
    #region Props

    private bool _isSyncBadgeVisible;

    public bool IsSyncBadgeVisible
    {
        get => _isSyncBadgeVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSyncBadgeVisible, value);
            this.RaisePropertyChanged(nameof(SyncBadgeColor));
            this.RaisePropertyChanged(nameof(SyncBadgeToolTipMessage));
        }
    }

    public string SyncBadgeColor => BehindOfRemoteBy > 0 ? "#c94f4f" : AheadOfRemoteBy > 0 ? "#ffa500" : "#000";

    public string SyncBadgeToolTipMessage
    {
        get
        {
            var messages = new[]
                {
                    BehindOfRemoteBy > 0 ? $"Behind remote by {BehindOfRemoteBy} change{(BehindOfRemoteBy > 1 ? "s" : string.Empty)}" : string.Empty,
                    AheadOfRemoteBy > 0 ? $"Ahead of remote by {AheadOfRemoteBy} change{(AheadOfRemoteBy > 1 ? "s" : string.Empty)}" : string.Empty,
                }.Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            return messages.Count == 0 ? "The store is up to date" : string.Join(" and ", messages);
        }
    }

    private int AheadOfRemoteBy { get; set; }

    private int BehindOfRemoteBy { get; set; }

    #endregion

    #region Constructors

    public SyncButtonViewModel()
    {
        AutoFetchStore();
    }

    #endregion

    #region Public methods

    public void Sync()
    {
        SpinnerOverlayService.Instance.Show("Synchronizing the store, please wait...");

        var result = AppService.Instance.GitPull();
        if (result.HasError)
        {
            SpinnerOverlayService.Instance.Hide();
            SnackbarService.Instance.Show("Fail to synchronize the store", SnackbarSeverity.Error, 5000);
            return;
        }

        result = AppService.Instance.GitPush();
        if (result.HasError)
        {
            SpinnerOverlayService.Instance.Hide();
            SnackbarService.Instance.Show("Fail to synchronize the store", SnackbarSeverity.Error, 5000);
            return;
        }

        SpinnerOverlayService.Instance.Hide();
        SnackbarService.Instance.Show("Store synchronized", SnackbarSeverity.Success);

        Task.Run(CheckStoreSync);
    }

    #endregion

    #region Private methods

    private void CheckStoreSync()
    {
        var (result, error) = AppService.Instance.GitFetch();
        if (error is not null) return;

        var (aheadBy, behindBy) = result;
        AheadOfRemoteBy = aheadBy;
        BehindOfRemoteBy = behindBy;
        IsSyncBadgeVisible = AheadOfRemoteBy > 0 || BehindOfRemoteBy > 0;
    }

    private void AutoFetchStore()
    {
        new Thread(() =>
        {
            while (true)
            {
                CheckStoreSync();

                Thread.Sleep(1000 * 60 * 5);
            }
            // ReSharper disable once FunctionNeverReturns
        })
        {
            IsBackground = true
        }.Start();
    }

    #endregion
}