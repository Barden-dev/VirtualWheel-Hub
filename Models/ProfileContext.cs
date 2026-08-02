using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Models
{
    public partial class ProfileContext : ObservableObject, IEquatable<ProfileContext>
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPath))]
        private string _game;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPath))]
        private string _carClass;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPath))]
        private string _car;

        public ProfileContext(string game = null, string carClass = null, string car = null)
        {
            Game = game;
            CarClass = carClass;
            Car = car;
        }

        public string DisplayPath
        {
            get
            {
                if (string.IsNullOrEmpty(Game)) return "Universal";
                
                string path = Game;
                if (!string.IsNullOrEmpty(CarClass))
                {
                    path += $" / {CarClass}";
                    if (!string.IsNullOrEmpty(Car))
                    {
                        path += $" / {Car}";
                    }
                }
                return path;
            }
        }

        public bool Equals(ProfileContext other)
        {
            if (other is null) return false;
            return Game == other.Game && CarClass == other.CarClass && Car == other.Car;
        }
        
        public override bool Equals(object obj) => Equals(obj as ProfileContext);
        public override int GetHashCode() => HashCode.Combine(Game, CarClass, Car);
    }
}
