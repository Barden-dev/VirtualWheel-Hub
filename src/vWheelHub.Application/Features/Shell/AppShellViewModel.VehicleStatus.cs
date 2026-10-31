using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Shell;

public partial class AppShellViewModel
{
    private GameVehicleStatus _gameVehicleStatus = new(null, VehicleIdentityState.NotRunning);
    private ProfileContext? _lastConfirmedVehicle;

    public void UpdateGameVehicleStatus(GameVehicleStatus status)
    {
        if (_gameVehicleStatus == status) return;
        _gameVehicleStatus = status;
        NotifyVehicleStatus();
    }

    public bool IsVehicleStatusVisible => _gameVehicleStatus.State != VehicleIdentityState.NotRunning ||
        Equals(_lastConfirmedVehicle, CurrentGameContext);

    public string VehicleStatusText
    {
        get
        {
            var state = _gameVehicleStatus.State;
            var current = new ProfileContext(_gameVehicleStatus.Game, _gameVehicleStatus.CarClass, _gameVehicleStatus.Car);
            if (state == VehicleIdentityState.Confirmed && current.Equals(CurrentGameContext))
                return _localizationManager.GetString("Vehicle_Confirmed");
            string message = _localizationManager.GetString(state switch
            {
                VehicleIdentityState.Waiting => "Vehicle_Waiting",
                VehicleIdentityState.Unsupported => "Vehicle_Unsupported",
                VehicleIdentityState.DetectionDisabled => "Vehicle_DetectionDisabled",
                VehicleIdentityState.Confirmed => "Vehicle_DifferentProfile",
                _ => "Vehicle_GameNotRunning"
            });
            var role = Equals(_lastConfirmedVehicle, CurrentGameContext) ? "Vehicle_LastProfile" : "Vehicle_SelectedProfile";
            return _localizationManager.GetString(role) + " · " + message;
        }
    }

    private void NotifyVehicleStatus()
    {
        if (_gameVehicleStatus.State == VehicleIdentityState.Confirmed)
        {
            var confirmed = new ProfileContext(_gameVehicleStatus.Game, _gameVehicleStatus.CarClass, _gameVehicleStatus.Car);
            if (confirmed.Equals(CurrentGameContext)) _lastConfirmedVehicle = confirmed;
        }
        OnPropertyChanged(nameof(VehicleStatusText));
        OnPropertyChanged(nameof(IsVehicleStatusVisible));
    }
}
