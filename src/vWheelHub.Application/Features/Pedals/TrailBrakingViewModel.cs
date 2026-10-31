using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Pedals
{
    /// <summary>
    /// Isolated ViewModel for Trail Braking settings (Enable switch, Threshold/MinLimit, ReleaseRate/DecaySeconds, Intensity),
    /// managing effective/inherited values from ProfileSession, override tracking, reset commands,
    /// and localized descriptions.
    /// </summary>
    public partial class TrailBrakingViewModel : ViewModelBase, IDisposable
    {
        private readonly ProfileSession _profileSession;
        private readonly ILocalizationManager _localizationManager;

        private Profile? _subscribedCurrentProfile;
        private ResolvedProfile? _subscribedResolvedProfile;
        private bool _eventsSubscribed;
        private bool _isDisposed;

        public ProfileSession ProfileSession => _profileSession;

        public Profile CurrentProfile => _profileSession.CurrentProfile;

        public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;

        public string PageTitle =>
            _localizationManager.GetString("Page_TrailBraking_Title", "Trail Braking");

        public string Description =>
            _localizationManager.GetString("Page_TrailBraking_Desc", "Gradually releases brakes as steering angle increases.");

        public string HubRateHint =>
            _localizationManager.GetString("Page_TrailBraking_HubRateHint", "Release rate is adjusted per tick according to hub update frequency.");

        public TrailBrakingViewModel(
            ProfileSession profileSession,
            ILocalizationManager localizationManager)
        {
            _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
            _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));

            SubscribeEvents();
        }

        #region Trail Braking Enable Switch

        /// <summary>
        /// Gets or sets whether Trail Braking is enabled.
        /// </summary>
        public bool? TrailBrakingEnabled
        {
            get => CurrentProfile?.EnableTrailBraking;
            set
            {
                if (CurrentProfile != null && CurrentProfile.EnableTrailBraking != value)
                {
                    CurrentProfile.EnableTrailBraking = value;
                    NotifyTrailBrakingEnabledProperties();
                }
            }
        }

        public bool? EnableTrailBraking
        {
            get => TrailBrakingEnabled;
            set => TrailBrakingEnabled = value;
        }

        public bool EffectiveTrailBrakingEnabled { get => CurrentProfile?.EnableTrailBraking ?? ResolvedProfile?.EnableTrailBraking ?? true; set => TrailBrakingEnabled = value; }

        public bool EffectiveEnableTrailBraking => EffectiveTrailBrakingEnabled;

        public bool IsTrailBrakingEnabledOverridden => CurrentProfile?.EnableTrailBraking.HasValue ?? false;

        public bool IsEnableTrailBrakingOverridden => IsTrailBrakingEnabledOverridden;

        #endregion

        #region Threshold (MinLimit)

        /// <summary>
        /// Gets or sets the Trail Braking activation threshold / minimum limit (0.0 to 1.0).
        /// </summary>
        public double? TrailBrakingThreshold
        {
            get => CurrentProfile?.TbMinLimit;
            set
            {
                if (CurrentProfile == null) return;
                double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.0, 1.0) : null;
                if (CurrentProfile.TbMinLimit != clamped)
                {
                    CurrentProfile.TbMinLimit = clamped;
                    NotifyThresholdProperties();
                }
            }
        }

        public double? TbMinLimit
        {
            get => TrailBrakingThreshold;
            set => TrailBrakingThreshold = value;
        }

        public double EffectiveTrailBrakingThreshold { get => CurrentProfile?.TbMinLimit ?? ResolvedProfile?.TbMinLimit ?? 0.0; set { if (Math.Abs(value - EffectiveTrailBrakingThreshold) > 1e-9) TrailBrakingThreshold = value; } }

        public double EffectiveTbMinLimit => EffectiveTrailBrakingThreshold;

        public bool IsTrailBrakingThresholdOverridden => CurrentProfile?.TbMinLimit.HasValue ?? false;

        public bool IsTbMinLimitOverridden => IsTrailBrakingThresholdOverridden;

        #endregion

        #region Release Rate (Decay)

        /// <summary>
        /// Gets or sets the Trail Braking release rate in seconds.
        /// </summary>
        public double? TrailBrakingReleaseRate
        {
            get => CurrentProfile?.TbDecaySeconds;
            set
            {
                if (CurrentProfile != null && CurrentProfile.TbDecaySeconds != value)
                {
                    CurrentProfile.TbDecaySeconds = value;
                    NotifyReleaseRateProperties();
                }
            }
        }

        public double? TbDecaySeconds
        {
            get => TrailBrakingReleaseRate;
            set => TrailBrakingReleaseRate = value;
        }

        public double? TbDecay
        {
            get => CurrentProfile?.TbDecay;
            set
            {
                if (CurrentProfile != null && CurrentProfile.TbDecay != value)
                {
                    CurrentProfile.TbDecay = value;
                    NotifyReleaseRateProperties();
                }
            }
        }

        public double EffectiveTrailBrakingReleaseRate
        {
            get => CurrentProfile?.TbDecaySeconds ??
                (ResolvedProfile != null && ResolvedProfile.TbDecay > 0
                    ? Math.Round(65536.0 / ResolvedProfile.TbDecay, 3) : 22.141);
            set { if (Math.Abs(value - EffectiveTrailBrakingReleaseRate) > 1e-9) TrailBrakingReleaseRate = value; }
        }

        public double EffectiveTbDecaySeconds => EffectiveTrailBrakingReleaseRate;

        public double EffectiveTbDecay => CurrentProfile?.TbDecay ?? ResolvedProfile?.TbDecay ?? Profile.DefaultTbDecay * 1000.0;

        public bool IsTrailBrakingReleaseRateOverridden => CurrentProfile?.TbDecay.HasValue ?? false;

        public bool IsTbDecayOverridden => IsTrailBrakingReleaseRateOverridden;

        #endregion

        #region Intensity

        /// <summary>
        /// Gets or sets the Trail Braking intensity factor.
        /// </summary>
        public double? TbIntensity
        {
            get => CurrentProfile?.TbIntensity;
            set
            {
                if (CurrentProfile != null && CurrentProfile.TbIntensity != value)
                {
                    CurrentProfile.TbIntensity = value;
                    NotifyIntensityProperties();
                }
            }
        }

        public double? TrailBrakingIntensity
        {
            get => TbIntensity;
            set => TbIntensity = value;
        }

        public double EffectiveTbIntensity { get => CurrentProfile?.TbIntensity ?? ResolvedProfile?.TbIntensity ?? 0.0; set { if (Math.Abs(value - EffectiveTbIntensity) > 1e-9) TbIntensity = value; } }

        public double EffectiveTrailBrakingIntensity => EffectiveTbIntensity;

        public bool IsTbIntensityOverridden => CurrentProfile?.TbIntensity.HasValue ?? false;

        public bool IsTrailBrakingIntensityOverridden => IsTbIntensityOverridden;

        #endregion

        #region Clear and Reset Commands

        [RelayCommand]
        public void ClearTrailBrakingEnabledOverride() => TrailBrakingEnabled = null;
        public void ClearEnableTrailBrakingOverride() => EnableTrailBraking = null;
        [RelayCommand]
        public void ClearTrailBrakingThresholdOverride() => TrailBrakingThreshold = null;
        public void ClearTbMinLimitOverride() => TbMinLimit = null;
        [RelayCommand]
        public void ClearTrailBrakingReleaseRateOverride() => TrailBrakingReleaseRate = null;
        public void ClearTbDecayOverride() => TbDecay = null;
        [RelayCommand]
        public void ClearTbIntensityOverride() => TbIntensity = null;
        public void ClearTrailBrakingIntensityOverride() => TrailBrakingIntensity = null;

        [RelayCommand]
        public void ClearOverride(string? propertyName)
        {
            TryClearOverride(propertyName);
        }

        public bool TryClearOverride(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return false;

            switch (propertyName)
            {
                case nameof(TrailBrakingEnabled):
                case nameof(EnableTrailBraking):
                    ClearTrailBrakingEnabledOverride();
                    return true;

                case nameof(TrailBrakingThreshold):
                case nameof(TbMinLimit):
                    ClearTrailBrakingThresholdOverride();
                    return true;

                case nameof(TrailBrakingReleaseRate):
                case nameof(TbDecaySeconds):
                case nameof(TbDecay):
                    ClearTrailBrakingReleaseRateOverride();
                    return true;

                case nameof(TbIntensity):
                case nameof(TrailBrakingIntensity):
                    ClearTbIntensityOverride();
                    return true;

                default:
                    return false;
            }
        }

        [RelayCommand]
        public void ClearAllTrailBrakingOverrides()
        {
            using var edit = _profileSession.BeginEditGroup(nameof(ClearAllTrailBrakingOverrides));
            ClearTrailBrakingEnabledOverride();
            ClearTrailBrakingThresholdOverride();
            ClearTrailBrakingReleaseRateOverride();
            ClearTbIntensityOverride();
        }

        [RelayCommand]
        public void ResetTrailBraking()
        {
            ClearAllTrailBrakingOverrides();
            NotifyAllTrailBrakingProperties();
        }

        [RelayCommand]
        public void ResetTbDecay()
        {
            if (CurrentProfile != null)
            {
                var defaults = ProfileDefaults.CreateDefault();
                CurrentProfile.TbDecay = defaults.TbDecay ?? Profile.DefaultTbDecay * 1000.0;
                NotifyReleaseRateProperties();
            }
        }

        #endregion

        #region Lifecycle & Events

        private void SubscribeEvents()
        {
            if (_eventsSubscribed) return;

            _profileSession.PropertyChanged += OnProfileSessionPropertyChanged;

            if (_profileSession.CurrentProfile != null)
            {
                _subscribedCurrentProfile = _profileSession.CurrentProfile;
                _subscribedCurrentProfile.PropertyChanged += OnCurrentProfilePropertyChanged;
            }

            if (_profileSession.ResolvedProfile != null)
            {
                _subscribedResolvedProfile = _profileSession.ResolvedProfile;
                _subscribedResolvedProfile.PropertyChanged += OnResolvedProfilePropertyChanged;
            }

            _localizationManager.LanguageChanged += OnLanguageChanged;
            _localizationManager.PropertyChanged += OnLocalizationPropertyChanged;

            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed) return;

            _profileSession.PropertyChanged -= OnProfileSessionPropertyChanged;

            if (_subscribedCurrentProfile != null)
            {
                _subscribedCurrentProfile.PropertyChanged -= OnCurrentProfilePropertyChanged;
                _subscribedCurrentProfile = null;
            }

            if (_subscribedResolvedProfile != null)
            {
                _subscribedResolvedProfile.PropertyChanged -= OnResolvedProfilePropertyChanged;
                _subscribedResolvedProfile = null;
            }

            _localizationManager.LanguageChanged -= OnLanguageChanged;
            _localizationManager.PropertyChanged -= OnLocalizationPropertyChanged;

            _eventsSubscribed = false;
        }

        private void OnProfileSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileSession.CurrentProfile))
            {
                if (_subscribedCurrentProfile != null)
                {
                    _subscribedCurrentProfile.PropertyChanged -= OnCurrentProfilePropertyChanged;
                }
                _subscribedCurrentProfile = _profileSession.CurrentProfile;
                if (_subscribedCurrentProfile != null)
                {
                    _subscribedCurrentProfile.PropertyChanged += OnCurrentProfilePropertyChanged;
                }
                NotifyAllTrailBrakingProperties();
            }
            else if (e.PropertyName == nameof(ProfileSession.ResolvedProfile))
            {
                if (_subscribedResolvedProfile != null)
                {
                    _subscribedResolvedProfile.PropertyChanged -= OnResolvedProfilePropertyChanged;
                }
                _subscribedResolvedProfile = _profileSession.ResolvedProfile;
                if (_subscribedResolvedProfile != null)
                {
                    _subscribedResolvedProfile.PropertyChanged += OnResolvedProfilePropertyChanged;
                }
                NotifyAllTrailBrakingProperties();
            }
        }

        private void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                NotifyAllTrailBrakingProperties();
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(Profile.EnableTrailBraking):
                    NotifyTrailBrakingEnabledProperties();
                    break;
                case nameof(Profile.TbMinLimit):
                    NotifyThresholdProperties();
                    break;
                case nameof(Profile.TbDecay):
                case nameof(Profile.TbDecaySeconds):
                    NotifyReleaseRateProperties();
                    break;
                case nameof(Profile.TbIntensity):
                    NotifyIntensityProperties();
                    break;
            }
        }

        private void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                NotifyAllTrailBrakingProperties();
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(ResolvedProfile.EnableTrailBraking):
                    NotifyTrailBrakingEnabledProperties();
                    break;
                case nameof(ResolvedProfile.TbMinLimit):
                    NotifyThresholdProperties();
                    break;
                case nameof(ResolvedProfile.TbDecay):
                    NotifyReleaseRateProperties();
                    break;
                case nameof(ResolvedProfile.TbIntensity):
                    NotifyIntensityProperties();
                    break;
            }
        }

        private void OnLanguageChanged(string cultureCode)
        {
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(HubRateHint));
        }

        private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(HubRateHint));
        }

        #endregion

        #region Property Notification Helpers

        private void NotifyTrailBrakingEnabledProperties()
        {
            OnPropertyChanged(nameof(TrailBrakingEnabled));
            OnPropertyChanged(nameof(EnableTrailBraking));
            OnPropertyChanged(nameof(EffectiveTrailBrakingEnabled));
            OnPropertyChanged(nameof(EffectiveEnableTrailBraking));
            OnPropertyChanged(nameof(IsTrailBrakingEnabledOverridden));
            OnPropertyChanged(nameof(IsEnableTrailBrakingOverridden));
        }

        private void NotifyThresholdProperties()
        {
            OnPropertyChanged(nameof(TrailBrakingThreshold));
            OnPropertyChanged(nameof(TbMinLimit));
            OnPropertyChanged(nameof(EffectiveTrailBrakingThreshold));
            OnPropertyChanged(nameof(EffectiveTbMinLimit));
            OnPropertyChanged(nameof(IsTrailBrakingThresholdOverridden));
            OnPropertyChanged(nameof(IsTbMinLimitOverridden));
        }

        private void NotifyReleaseRateProperties()
        {
            OnPropertyChanged(nameof(TrailBrakingReleaseRate));
            OnPropertyChanged(nameof(TbDecaySeconds));
            OnPropertyChanged(nameof(TbDecay));
            OnPropertyChanged(nameof(EffectiveTrailBrakingReleaseRate));
            OnPropertyChanged(nameof(EffectiveTbDecaySeconds));
            OnPropertyChanged(nameof(EffectiveTbDecay));
            OnPropertyChanged(nameof(IsTrailBrakingReleaseRateOverridden));
            OnPropertyChanged(nameof(IsTbDecayOverridden));
        }

        private void NotifyIntensityProperties()
        {
            OnPropertyChanged(nameof(TbIntensity));
            OnPropertyChanged(nameof(TrailBrakingIntensity));
            OnPropertyChanged(nameof(EffectiveTbIntensity));
            OnPropertyChanged(nameof(EffectiveTrailBrakingIntensity));
            OnPropertyChanged(nameof(IsTbIntensityOverridden));
            OnPropertyChanged(nameof(IsTrailBrakingIntensityOverridden));
        }

        public void NotifyAllTrailBrakingProperties()
        {
            NotifyTrailBrakingEnabledProperties();
            NotifyThresholdProperties();
            NotifyReleaseRateProperties();
            NotifyIntensityProperties();
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            UnsubscribeEvents();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
