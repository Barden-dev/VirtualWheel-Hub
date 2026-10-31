using System;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Runtime
{
    /// <summary>
    /// Hotkey monitoring service managing global Toggle Hub and Toggle Lock shortcuts.
    /// Reads key state from an injected key provider and maintains single ownership of the toggle lock state.
    /// Ensures that mouse grab is never engaged outside an active runtime state.
    /// </summary>
    public class HotkeyService : IDisposable
    {
        public event Action? OnToggleHub;
        public event Action? OnToggleLock;

        private readonly Func<int, bool>? _keyStateProvider;
        private readonly ICursorLockService? _cursorLockService;
        private readonly IInputSource? _managedInputSource;
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private ResolvedProfile? _currentProfile;
        private volatile bool _isRuntimeActive;
        private bool _isLocked;
        private bool _wasHubTogglePressed;
        private bool _wasLockTogglePressed;
        private bool _disposed;
        private volatile bool _awaitRelease;
        public bool IsSuppressed { get; set; }
        public void RequireKeyRelease() => _awaitRelease = true;

        /// <summary>
        /// Gets or sets whether mouse lock is toggled on.
        /// When runtime is stopped, mouse cursor is guaranteed not to remain grabbed.
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                _isLocked = value;
                if (!_isRuntimeActive && _cursorLockService?.IsCursorLocked == true)
                {
                    _cursorLockService.UnlockCursor();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the hub runtime is currently active.
        /// Transitioning to false immediately releases any cursor grab.
        /// </summary>
        public bool IsRuntimeActive
        {
            get => _isRuntimeActive;
            set
            {
                _isRuntimeActive = value;
                if (!value && _cursorLockService?.IsCursorLocked == true)
                {
                    _cursorLockService.UnlockCursor();
                }
            }
        }

        public HotkeyService(Func<int, bool>? keyStateProvider = null, ICursorLockService? cursorLockService = null)
        {
            _keyStateProvider = keyStateProvider;
            _cursorLockService = cursorLockService;
        }

        public HotkeyService(IInputSource inputSource, ICursorLockService? cursorLockService = null, bool manageInputSource = false)
            : this(inputSource != null ? inputSource.IsKeyDown : null, cursorLockService)
        {
            _managedInputSource = manageInputSource ? inputSource : null;
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _workerTask = Task.Run(() => HotkeyLoop(token));
        }

        public void Stop()
        {
            var cts = _cts;
            cts?.Cancel();
            if (_workerTask != null && Task.CurrentId != _workerTask.Id) _workerTask.GetAwaiter().GetResult();
            cts?.Dispose();
            _cts = null;
            _workerTask = null;
        }

        public void UpdateProfile(ResolvedProfile profile)
        {
            _currentProfile = profile;
        }

        /// <summary>
        /// Synchronously evaluates hotkey states for a single cycle.
        /// Supports edge-triggering and deterministic unit test verification.
        /// </summary>
        public void ProcessHotkeys()
        {
            var profile = _currentProfile;
            var isKeyDown = _keyStateProvider;
            if (profile == null || isKeyDown == null) return;

            // Toggle Hub (active even when runtime is stopped)
            bool isHubTogglePressed = (profile.KeyToggleHub1 != 0 && isKeyDown(profile.KeyToggleHub1)) ||
                                      (profile.KeyToggleHub2 != 0 && isKeyDown(profile.KeyToggleHub2));

            if (!IsSuppressed && !_awaitRelease && isHubTogglePressed && !_wasHubTogglePressed)
            {
                OnToggleHub?.Invoke();
            }
            _wasHubTogglePressed = isHubTogglePressed;

            // Toggle Lock (single owner)
            bool isLockTogglePressed = (profile.KeyToggleLock1 != 0 && isKeyDown(profile.KeyToggleLock1)) ||
                                       (profile.KeyToggleLock2 != 0 && isKeyDown(profile.KeyToggleLock2));

            if (!IsSuppressed && !_awaitRelease && isLockTogglePressed && !_wasLockTogglePressed)
            {
                _isLocked = !_isLocked;
                OnToggleLock?.Invoke();

                // Guarantee: cursor is never grabbed outside active runtime
                if (!_isRuntimeActive && _cursorLockService?.IsCursorLocked == true)
                {
                    _cursorLockService.UnlockCursor();
                }
            }
            _wasLockTogglePressed = isLockTogglePressed;
            if (!IsSuppressed && !isHubTogglePressed && !isLockTogglePressed) _awaitRelease = false;
        }

        private void HotkeyLoop(CancellationToken token)
        {
            long nextInputRetry = 0;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_managedInputSource is { IsRunning: false })
                    {
                        RequireKeyRelease();
                        if (Environment.TickCount64 >= nextInputRetry)
                        {
                            nextInputRetry = Environment.TickCount64 + 1000;
                            try { _managedInputSource.Start(); }
                            catch (Exception ex) { AppLogger.Instance.LogWarning($"Global shortcuts unavailable: {ex.Message}"); }
                        }
                    }
                    // The independent reader also observes mouse buttons without grabbing
                    // the pointer. Its motion is never used for steering.
                    _managedInputSource?.ConsumeDeltaX();
                    ProcessHotkeys();
                    if (token.IsCancellationRequested) break;
                    try
                    {
                        if (token.WaitHandle.WaitOne(50)) break;
                    }
                    catch (ThreadInterruptedException)
                    {
                        break;
                    }
                }
            }
            finally { _managedInputSource?.Stop(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
