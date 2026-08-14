using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using SimRacingHub.Models;
using System.Linq;

using SimRacingHub.Core;

namespace SimRacingHub.Services
{
    public class ProfileManager
    {
        private readonly string _profileDirectory = StoragePaths.ProfilesDirectory;

        public ProfileManager()
        {
            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }
        }

        public static string SanitizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct();
            string clean = new string(input.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
            while (clean.Contains("..")) clean = clean.Replace("..", "");
            return clean;
        }

        public string GetProfilePath(ProfileContext context)
        {
            string game = SanitizeName(context?.Game);
            if (string.IsNullOrEmpty(game)) game = "Universal";
            string carClass = SanitizeName(context?.CarClass);
            string car = SanitizeName(context?.Car);

            if (game.Equals("Universal", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(carClass))
            {
                return Path.Combine(_profileDirectory, "Universal.json");
            }
            
            if (string.IsNullOrEmpty(carClass))
            {
                return Path.Combine(_profileDirectory, game, "Default.json");
            }
            
            if (string.IsNullOrEmpty(car))
                return Path.Combine(_profileDirectory, game, carClass, "Default.json");
            
            return Path.Combine(_profileDirectory, game, carClass, $"{car}.json");
        }

        private (Profile profile, bool isNew) LoadFile(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var profile = JsonConvert.DeserializeObject<Profile>(json) ?? new Profile();
                    
                    var defaults = Profile.CreateDefault();
                    bool modified = false;
                    foreach (var prop in typeof(Profile).GetProperties())
                    {
                        if (prop.CanWrite && prop.Name != "Name" && prop.GetValue(profile) == null)
                        {
                            prop.SetValue(profile, prop.GetValue(defaults));
                            modified = true;
                        }
                    }
                    
                    if (modified)
                    {
                        SaveFile(path, profile);
                    }
                    
                    return (profile, false);
                }
                catch
                {
                    return (new Profile(), false);
                }
            }
            
            return (new Profile(), true);
        }

        public (Profile active, ResolvedProfile resolved) LoadContext(ProfileContext context)
        {
            var resolved = new ResolvedProfile();
            // Start with hardcoded defaults so we don't have zeros if a profile is empty
            resolved.UpdateFrom(Profile.CreateDefault("Base"));
            
            Profile activeProfile;

            string game = SanitizeName(context?.Game);
            if (string.IsNullOrEmpty(game)) game = "Universal";
            string carClass = SanitizeName(context?.CarClass);
            string car = SanitizeName(context?.Car);

            // 1. Game Level
            Profile gameProf;
            if (game.Equals("Universal", StringComparison.OrdinalIgnoreCase))
            {
                var (universal, universalIsNew) = LoadFile(Path.Combine(_profileDirectory, "Universal.json"));
                if (universalIsNew || !universal.MouseSensitivity.HasValue) 
                {
                    universal = Profile.CreateDefault("Universal");
                    SaveFile(Path.Combine(_profileDirectory, "Universal.json"), universal);
                }
                gameProf = universal;
            }
            else
            {
                var (gProf, gIsNew) = LoadFile(Path.Combine(_profileDirectory, game, "Default.json"));
                if (gIsNew)
                {
                    var (universal, _) = LoadFile(Path.Combine(_profileDirectory, "Universal.json"));
                    gProf.CopyFrom(universal);
                    gProf.Name = $"{game} (Default)";
                }
                gameProf = gProf;
            }

            resolved.UpdateFrom(gameProf);
            activeProfile = gameProf;

            // 2. Class Level
            if (!string.IsNullOrEmpty(carClass))
            {
                var (classProf, classIsNew) = LoadFile(Path.Combine(_profileDirectory, game, carClass, "Default.json"));
                if (classIsNew)
                {
                    classProf.CopyFrom(gameProf);
                    classProf.Name = $"{carClass} (Default)";
                }
                
                resolved.UpdateFrom(classProf);
                activeProfile = classProf;

                // 3. Car Level
                if (!string.IsNullOrEmpty(car))
                {
                    var (carProf, carIsNew) = LoadFile(Path.Combine(_profileDirectory, game, carClass, $"{car}.json"));
                    if (carIsNew)
                    {
                        carProf.CopyFrom(classProf);
                        carProf.Name = car;
                    }
                    
                    resolved.UpdateFrom(carProf);
                    activeProfile = carProf;
                }
            }

            return (activeProfile, resolved);
        }

        private void SaveFile(string path, Profile profile)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void SaveContext(ProfileContext context, Profile profile)
        {
            string path = GetProfilePath(context);
            SaveFile(path, profile);
        }

        public List<string> GetAvailableGames()
        {
            var games = new List<string>();
            if (Directory.Exists(_profileDirectory))
            {
                foreach (var dir in Directory.GetDirectories(_profileDirectory))
                {
                    games.Add(Path.GetFileName(dir));
                }
            }
            return games;
        }

        public List<string> GetAvailableClasses(string game)
        {
            var classes = new List<string>();
            if (string.IsNullOrEmpty(game)) return classes;
            
            var path = Path.Combine(_profileDirectory, game);
            if (Directory.Exists(path))
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    classes.Add(Path.GetFileName(dir));
                }
            }
            return classes;
        }

        public List<string> GetAvailableCars(string game, string carClass)
        {
            var cars = new List<string>();
            if (string.IsNullOrEmpty(game) || string.IsNullOrEmpty(carClass)) return cars;
            
            var path = Path.Combine(_profileDirectory, game, carClass);
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name != "Default")
                    {
                        cars.Add(name);
                    }
                }
            }
            return cars;
        }
    }
}
