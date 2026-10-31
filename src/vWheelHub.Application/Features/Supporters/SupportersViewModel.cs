using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Features.Supporters;

public partial class SupportersViewModel : ViewModelBase, IDisposable
{
    private readonly ISupportersFeed _feed;
    private readonly IUiDispatcher _dispatcher;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VipSponsors))]
    [NotifyPropertyChangedFor(nameof(ProjectSupporters))]
    [NotifyPropertyChangedFor(nameof(Thanks))]
    [NotifyPropertyChangedFor(nameof(HasVipSponsors))]
    [NotifyPropertyChangedFor(nameof(HasProjectSupporters))]
    [NotifyPropertyChangedFor(nameof(HasThanks))]
    private IReadOnlyList<Supporter> _entries;

    public IReadOnlyList<Supporter> VipSponsors => Entries.Where(e => e.Tier == SupporterTier.Vip).ToArray();
    public IReadOnlyList<Supporter> ProjectSupporters => Entries.Where(e => e.Tier == SupporterTier.Supporter).ToArray();
    public IReadOnlyList<Supporter> Thanks => Entries.Where(e => e.Tier == SupporterTier.Thanks).ToArray();
    public bool HasVipSponsors => Entries.Any(e => e.Tier == SupporterTier.Vip);
    public bool HasProjectSupporters => Entries.Any(e => e.Tier == SupporterTier.Supporter);
    public bool HasThanks => Entries.Any(e => e.Tier == SupporterTier.Thanks);

    public SupportersViewModel(ISupportersFeed feed, IUiDispatcher dispatcher)
    {
        _feed = feed; _dispatcher = dispatcher;
        _entries = feed.ReadCached();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed) return;
        try
        {
            var entries = await _feed.RefreshAsync(_lifetime.Token).ConfigureAwait(false);
            if (!_disposed) await _dispatcher.InvokeAsync(() => { if (!_disposed) Entries = entries; }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposed) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel(); _lifetime.Dispose();
    }
}
