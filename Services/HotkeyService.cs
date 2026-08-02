using System;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Models;
using SimRacingHub.Input;

namespace SimRacingHub.Services
{
    public class HotkeyService : IDisposable
    {
        public event Action OnToggleHub;
        public event Action OnToggleLock;
        
        private CancellationTokenSource _cts;
        private ResolvedProfile _currentProfile;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => HotkeyLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void UpdateProfile(ResolvedProfile profile)
        {
            _currentProfile = profile;
        }

        private void HotkeyLoop(CancellationToken token)
        {
            bool wasHubTogglePressed = false;
            bool wasLockTogglePressed = false;
            
            while (!token.IsCancellationRequested)
            {
                var profile = _currentProfile;
                if (profile == null)
                {
                    Thread.Sleep(100);
                    continue;
                }
                
                bool isHubTogglePressed = (profile.KeyToggleHub1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyToggleHub1)) || 
                                          (profile.KeyToggleHub2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyToggleHub2));
                
                if (isHubTogglePressed && !wasHubTogglePressed)
                {
                    OnToggleHub?.Invoke();
                }
                wasHubTogglePressed = isHubTogglePressed;
                
                bool isLockTogglePressed = (profile.KeyToggleLock1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyToggleLock1)) || 
                                           (profile.KeyToggleLock2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyToggleLock2));
                
                if (isLockTogglePressed && !wasLockTogglePressed)
                {
                    OnToggleLock?.Invoke();
                }
                wasLockTogglePressed = isLockTogglePressed;
                
                Thread.Sleep(50);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
