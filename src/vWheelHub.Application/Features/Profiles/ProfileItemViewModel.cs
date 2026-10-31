using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Profiles
{
    /// <summary>
    /// Represents an individual profile item in the profiles list.
    /// </summary>
    public partial class ProfileItemViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LocalizedName))]
        private string _name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LocalizedPath))]
        private ProfileContext _context;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LocalizedName))]
        private bool _isDefault;

        [ObservableProperty]
        private bool _canDelete;

        [ObservableProperty]
        private bool _canRename;

        public string DisplayPath => Context == null ? Name : DetectedProfileNames.Path(Context, Name);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LocalizedName))]
        [NotifyPropertyChangedFor(nameof(LocalizedPath))]
        private string _systemProfileLabel = "Universal";

        public string LocalizedName => IsDefault ? SystemProfileLabel : Name;
        public string LocalizedPath => DisplayPath == "Universal" ? SystemProfileLabel : DisplayPath;

        public ProfileItemViewModel(
            string name,
            ProfileContext context,
            bool isDefault = false,
            bool canDelete = true,
            bool canRename = true)
        {
            _name = name;
            _context = context ?? new ProfileContext("Universal");
            _isDefault = isDefault;
            _canDelete = canDelete;
            _canRename = canRename;
        }

        public override string ToString() => DisplayPath;
    }
}
