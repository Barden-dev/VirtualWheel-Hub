using System;
using System.Runtime.InteropServices;
using SimRacingHub.Core;
using SimRacingHub.Services;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux
{
    public sealed class EvdevCursorLockService : ICursorLockService
    {
        private readonly Func<int> _getMouseFd;
        private readonly object _lock = new object();
        private bool _isLocked;
        private bool _disposed;
        private readonly EvdevInputSource? _inputSource;
        private UInputPointerRelay? _pointerRelay;

        public EvdevCursorLockService(Func<int> getMouseFd)
        {
            _getMouseFd = getMouseFd ?? throw new ArgumentNullException(nameof(getMouseFd));
        }

        public EvdevCursorLockService(EvdevInputSource inputSource)
            : this(() => inputSource.MouseFd)
        {
            _inputSource = inputSource;
            inputSource.MouseEvent += ForwardMouseEvent;
        }

        public bool IsCursorLocked
        {
            get
            {
                lock (_lock) return _isLocked;
            }
        }

        public void LockCursorAtCurrentPosition()
        {
            lock (_lock)
            {
                if (_disposed) return;
                int fd = _getMouseFd();
                if (fd >= 0)
                {
                    if (_inputSource != null && _pointerRelay == null)
                    {
                        try { _pointerRelay = new UInputPointerRelay(); }
                        catch (Exception ex)
                        {
                            AppLogger.Instance.LogWarning($"Cursor lock unavailable: {ex.Message}");
                            return;
                        }
                    }
                    int ret = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGRAB, 1);
                    _isLocked = (ret == 0);
                }
                else
                {
                    _isLocked = false;
                }
            }
        }

        public void UnlockCursor()
        {
            lock (_lock)
            {
                int fd = _getMouseFd();
                if (fd >= 0)
                {
                    LinuxNative.Ioctl(fd, LinuxNative.EVIOCGRAB, 0);
                }
                _isLocked = false;
                try { _pointerRelay?.ReleaseButtons(); }
                catch (Exception) { _pointerRelay?.Dispose(); _pointerRelay = null; }
            }
        }

        public void ReapplyLock()
        {
            lock (_lock)
            {
                if (_isLocked)
                {
                    LockCursorAtCurrentPosition();
                }
            }
        }

        private void ForwardMouseEvent(InputEvent ev)
        {
            lock (_lock)
            {
                if (!_isLocked) return;
                try { _pointerRelay?.Forward(ev); }
                catch (Exception ex)
                {
                    UnlockCursor();
                    _pointerRelay?.Dispose();
                    _pointerRelay = null;
                    AppLogger.Instance.LogWarning($"Cursor released after button relay failure: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            UnlockCursor();
            if (_inputSource != null) _inputSource.MouseEvent -= ForwardMouseEvent;
            _pointerRelay?.Dispose();
        }
    }
}
