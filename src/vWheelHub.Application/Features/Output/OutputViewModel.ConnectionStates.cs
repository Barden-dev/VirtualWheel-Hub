using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Features.Output;

public partial class OutputViewModel
{
    public Action? NavigateGameBindings { get; set; }
    public Action? NavigateGameConnection { get; set; }
    private VJoyDeviceInfo? EffectiveVJoyDevice => _vJoyInventory?.Devices.FirstOrDefault(device =>
        device.DeviceId == (_vJoyInventory.SelectedDeviceId ?? VJoyDeviceId));
    public string GameBindingsStateText => _localizationManager.GetString("Output_GameBindingsUnverified");
    public string VirtualDeviceStateText
    {
        get
        {
            string key = "Output_DeviceNotChecked";
            if (!ShowVJoySettings) key = DeviceDiagnosticSnapshot is { IsAvailable: true } diagnostic && diagnostic.Mode == SelectedOutputMode ? "Output_DeviceReady" : key;
            else if (EffectiveVJoyDevice is { } device)
            {
                key = _vJoyInventory?.SelectionError != null ? "Output_DeviceNeedsAttention" :
                    device.IsUsable && device.MeetsRequirements(true) ? "Output_DeviceReady" : "Output_DeviceNeedsAttention";
            }
            return _localizationManager.GetString(key);
        }
    }
    [RelayCommand] public void OpenGameBindings() => NavigateGameBindings?.Invoke();
    [RelayCommand] public void OpenGameConnection() => NavigateGameConnection?.Invoke();
    partial void ConnectOutputConnectionObserver();
    partial void DisconnectOutputConnectionObserver();
    partial void OnDeviceDiagnosticSnapshotChanged(DeviceDiagnosticSnapshot? value) => OnPropertyChanged(nameof(VirtualDeviceStateText));
    private void RefreshOutputConnectionText()
    {
        OnPropertyChanged(nameof(VirtualDeviceStateText));
        OnPropertyChanged(nameof(GameBindingsStateText));
        OnPropertyChanged(nameof(GameDataConnectionText));
    }
}
