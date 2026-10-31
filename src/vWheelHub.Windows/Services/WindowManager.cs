using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Core;

namespace SimRacingHub.Services
{
    public class WindowManager : ICursorLockService
    {
        private const string CleanupKey = "cursor-clip";
        private const int EnforceIntervalMs = 15;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClipCursor(out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private readonly Func<bool> _releaseClip;
        private readonly Func<(int X, int Y)?> _readPosition;
        private readonly Func<RECT, bool> _applyClip;
        private readonly Action<int, int> _setPosition;
        private readonly bool _runEnforcer;
        private bool _releasePending;
        private CancellationTokenSource? _releaseCts;

        public WindowManager() : this(() => ClipCursor(IntPtr.Zero),
            () => GetCursorPos(out var point) ? (point.X, point.Y) : null,
            rect => ClipCursor(ref rect), (x, y) => SetCursorPos(x, y), true) { }

        internal WindowManager(Func<bool> releaseClip, Func<(int X, int Y)?> readPosition,
            Func<RECT, bool> applyClip, Action<int, int> setPosition, bool runEnforcer = false)
        {
            _releaseClip = releaseClip; _readPosition = readPosition;
            _applyClip = applyClip; _setPosition = setPosition; _runEnforcer = runEnforcer;
        }

        private readonly object _lock = new object();
        private bool _isCursorLocked;
        private POINT _cursorAnchor;
        private CancellationTokenSource? _enforcerCts;
        private Task? _enforcerTask;

        public bool IsCursorLocked
        {
            get
            {
                lock (_lock)
                {
                    return _isCursorLocked || _releasePending;
                }
            }
        }

        public void LockCursorAtCurrentPosition()
        {
            lock (_lock)
            {
                if (!_isCursorLocked)
                {
                    var position = _readPosition();
                    if (position is null)
                    {
                        AppLogger.Instance.LogWarning($"Could not read cursor position (Win32 error {Marshal.GetLastWin32Error()})");
                        return;
                    }

                    _cursorAnchor = new POINT { X = position.Value.X, Y = position.Value.Y };
                    _releasePending = false;
                    _isCursorLocked = true;
                    CrashSafety.Register(CleanupKey, UnlockCursor);
                }

                _releaseCts?.Cancel();
                _releaseCts?.Dispose();
                _releaseCts = null;
                ApplyClipInternal();
                StartEnforcerLoopLocked();
            }
        }

        public void ReapplyLock()
        {
            lock (_lock)
            {
                if (_isCursorLocked)
                {
                    ApplyClipInternal();
                    StartEnforcerLoopLocked();
                }
            }
        }

        public void UnlockCursor()
        {
            CancellationTokenSource? cts;
            Task? task;

            lock (_lock)
            {
                if (!_isCursorLocked && !_releasePending) return;

                _isCursorLocked = false;
                cts = _enforcerCts;
                task = _enforcerTask;
                _enforcerCts = null;
                _enforcerTask = null;

                try { cts?.Cancel(); } catch { }

                _releasePending = !_releaseClip();
                if (_releasePending)
                {
                    AppLogger.Instance.LogWarning($"Could not release the cursor (Win32 error {Marshal.GetLastWin32Error()}); retrying");
                    StartReleaseRetryLocked();
                }
                else
                {
                    _releaseCts?.Cancel();
                    CrashSafety.Unregister(CleanupKey);
                }
            }

            try
            {
                if (task != null)
                {
                    task.Wait(100);
                }
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
            catch (Exception) { }
            finally
            {
                cts?.Dispose();
            }
        }

        private void ApplyClipInternal()
        {
            var rect = new RECT
            {
                Left = _cursorAnchor.X,
                Top = _cursorAnchor.Y,
                Right = _cursorAnchor.X + 1,
                Bottom = _cursorAnchor.Y + 1
            };

            _setPosition(_cursorAnchor.X, _cursorAnchor.Y);
            if (!_applyClip(rect))
            {
                AppLogger.Instance.LogWarning($"Could not lock the cursor (Win32 error {Marshal.GetLastWin32Error()})");
            }
        }

        private void StartReleaseRetryLocked()
        {
            if (_releaseCts is { IsCancellationRequested: false }) return;
            _releaseCts?.Dispose();
            _releaseCts = new CancellationTokenSource();
            var token = _releaseCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                        lock (_lock)
                        {
                            if (token.IsCancellationRequested || _isCursorLocked || !_releasePending) return;
                            if (_releaseClip())
                            {
                                _releasePending = false;
                                CrashSafety.Unregister(CleanupKey);
                                return;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });
        }

        private void StartEnforcerLoopLocked()
        {
            if (!_runEnforcer) return;
            if (_enforcerTask != null && !_enforcerTask.IsCompleted)
            {
                return;
            }

            _enforcerCts?.Dispose();
            _enforcerCts = new CancellationTokenSource();
            var token = _enforcerCts.Token;

            _enforcerTask = Task.Run(() => EnforceLoop(token), token);
        }

        private void EnforceLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    POINT anchor;
                    lock (_lock)
                    {
                        if (!_isCursorLocked || token.IsCancellationRequested) break;
                        anchor = _cursorAnchor;
                    }

                    var expectedRect = new RECT
                    {
                        Left = anchor.X,
                        Top = anchor.Y,
                        Right = anchor.X + 1,
                        Bottom = anchor.Y + 1
                    };

                    bool needsReapply = false;

                    if (GetClipCursor(out RECT currentClip))
                    {
                        if (currentClip.Left != expectedRect.Left ||
                            currentClip.Top != expectedRect.Top ||
                            currentClip.Right != expectedRect.Right ||
                            currentClip.Bottom != expectedRect.Bottom)
                        {
                            needsReapply = true;
                        }
                    }

                    if (GetCursorPos(out POINT currentPos))
                    {
                        if (currentPos.X != anchor.X || currentPos.Y != anchor.Y)
                        {
                            needsReapply = true;
                        }
                    }

                    if (needsReapply)
                    {
                        lock (_lock)
                        {
                            if (_isCursorLocked && !token.IsCancellationRequested)
                            {
                                SetCursorPos(anchor.X, anchor.Y);
                                ClipCursor(ref expectedRect);
                            }
                        }
                    }

                    Thread.Sleep(EnforceIntervalMs);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogWarning($"Cursor lock enforcer error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        public void Dispose()
        {
            UnlockCursor();
        }
    }
}