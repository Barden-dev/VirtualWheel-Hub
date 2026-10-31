using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using SimRacingHub.Core;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux
{
    public sealed class EvdevInputSource : IInputSource
    {
        private readonly string? _mousePathOverride;
        private readonly string? _keyboardPathOverride;
        private readonly object _lifecycleLock = new object();
        private readonly object _operationLock = new();
        private readonly bool _keyboardOnly;
        private readonly bool _queryKeyState;
        private readonly byte[] _keySnapshot = new byte[64];
        private volatile bool _inputLost;
        private bool _discardMouseFrame, _discardKeyboardFrame;

        private Thread? _workerThread;
        private volatile bool _shouldStop;
        private int _mouseFd = -1;
        private int _keyboardFd = -1;

        private int _pendingDeltaX;
        private long _movementVersion, _pressVersion, _keyboardPressVersion;
        public long MovementVersion => Interlocked.Read(ref _movementVersion);
        public long PressVersion => Interlocked.Read(ref _pressVersion);
        public long KeyboardPressVersion => Interlocked.Read(ref _keyboardPressVersion);
        private readonly byte[] _keyStates = new byte[512]; // Linux key codes fit in [0..511]
        private bool _disposed;

        public EvdevInputSource(string? mousePath = null, string? keyboardPath = null, bool keyboardOnly = false, bool queryKeyState = false)
        {
            _mousePathOverride = mousePath;
            _keyboardPathOverride = keyboardPath;
            _keyboardOnly = keyboardOnly;
            _queryKeyState = queryKeyState;
        }

        public bool IsRunning
        {
            get
            {
                lock (_lifecycleLock)
                {
                    return !_shouldStop && !_inputLost && _workerThread != null && _workerThread.IsAlive;
                }
            }
        }

        internal int MouseFd => _mouseFd;
        internal event Action<InputEvent>? MouseEvent;

        internal EvdevInputSource CreateKeyStateObserver() =>
            new(_mousePathOverride, _keyboardPathOverride, queryKeyState: true);

        public void Start()
        {
            lock (_operationLock)
            {
                if (IsRunning) return;
                Stop();
                lock (_lifecycleLock)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(EvdevInputSource));

                    var settings = AppSettings.Load();
                    var resolved = (_mousePathOverride == null || _keyboardPathOverride == null)
                        ? LinuxInputDeviceResolver.ResolveDevices(settings) : null;
                    if (resolved?.IsUnambiguous == true && (settings.LinuxSelectedMousePath == null || settings.LinuxSelectedKeyboardPath == null))
                    {
                        settings.LinuxSelectedMousePath = resolved.MousePath;
                        settings.LinuxSelectedKeyboardPath = resolved.KeyboardPath;
                        settings.LinuxSelectedMouseIdentity = LinuxInputDeviceResolver.GetIdentity(resolved.MousePath!);
                        settings.LinuxSelectedKeyboardIdentity = LinuxInputDeviceResolver.GetIdentity(resolved.KeyboardPath!);
                        settings.Save();
                    }
                    string? mouseDevice = _keyboardOnly ? null : _mousePathOverride ?? resolved?.MousePath;
                    string? kbdDevice = _keyboardPathOverride ?? resolved?.KeyboardPath;
                    AppLogger.Instance.LogInfo($"Linux input selection: mouse='{mouseDevice}', keyboard='{kbdDevice}', keyboardOnly={_keyboardOnly}.");
                    if ((!_keyboardOnly && mouseDevice == null) || kbdDevice == null)
                        throw new IOException(SimRacingHub.Application.Localization.LocalizationManager.Instance.GetString("Prep_InputSelectionRequired"));

                    try
                    {
                        // Wait briefly for udev permissions if needed (e.g. newly created synthetic node)
                        long openDeadline = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(1.5 * System.Diagnostics.Stopwatch.Frequency);
                        while (mouseDevice != null)
                        {
                            _mouseFd = LinuxNative.Open(mouseDevice, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
                            if (_mouseFd >= 0) break;

                            int errno = Marshal.GetLastWin32Error();
                            if (errno != 13 || System.Diagnostics.Stopwatch.GetTimestamp() >= openDeadline)
                            {
                                throw new IOException($"Failed to open mouse device '{mouseDevice}': errno {errno}");
                            }
                            Thread.Sleep(50);
                        }

                        if (!string.IsNullOrEmpty(kbdDevice))
                        {
                            _keyboardFd = LinuxNative.Open(kbdDevice, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
                            if (_keyboardFd < 0) throw new IOException($"Failed to open keyboard device '{kbdDevice}': errno {Marshal.GetLastWin32Error()}");
                        }
                        else if (_keyboardOnly) throw new IOException("No readable keyboard device was found.");

                        _inputLost = false;
                        _discardMouseFrame = _discardKeyboardFrame = false;
                        _shouldStop = false;
                        _workerThread = new Thread(WorkerLoop)
                        {
                            Name = "EvdevInputReader",
                            IsBackground = true,
                            Priority = ThreadPriority.Highest
                        };
                        _workerThread.Start();
                    }
                    catch
                    {
                        _workerThread = null;
                        _shouldStop = true;
                        CloseDevices();
                        ResetState();
                        throw;
                    }
                }
            }
        }

        public void Stop()
        {
            lock (_operationLock)
            {
                Thread? thread;
                lock (_lifecycleLock)
                {
                    _shouldStop = true;
                    thread = _workerThread;
                }

                if (thread != null && !thread.Join(1000))
                {
                    ResetState();
                    throw new TimeoutException("Input reader did not stop; a second reader will not be started.");
                }

                lock (_lifecycleLock)
                {
                    _workerThread = null;
                    CloseDevices();
                    ResetState();
                }
            }
        }

        private void CloseDevices()
        {
            if (_mouseFd >= 0) { LinuxNative.Close(_mouseFd); _mouseFd = -1; }
            if (_keyboardFd >= 0) { LinuxNative.Close(_keyboardFd); _keyboardFd = -1; }
        }

        private void ResetState()
        {
            for (int i = 0; i < _keyStates.Length; i++) Volatile.Write(ref _keyStates[i], (byte)0);
            Interlocked.Exchange(ref _pendingDeltaX, 0);
        }

        public int ConsumeDeltaX()
        {
            return Interlocked.Exchange(ref _pendingDeltaX, 0);
        }

        public bool IsKeyDown(int keyCode)
        {
            if (LinuxKeyCodeMapper.IsGenericModifier(keyCode, out ushort primary, out ushort secondary))
            {
                bool primaryDown = ReadKeyState(primary);
                bool secondaryDown = ReadKeyState(secondary);
                return primaryDown || secondaryDown;
            }

            ushort linuxCode = LinuxKeyCodeMapper.ToLinuxKey(keyCode);
            if (linuxCode == 0 || linuxCode >= _keyStates.Length) return false;
            return ReadKeyState(linuxCode);
        }

        private bool ReadKeyState(ushort code)
        {
            if (code >= _keyStates.Length || _inputLost || _shouldStop) return false;
            if (_queryKeyState)
            {
                lock (_lifecycleLock)
                {
                    int fd = code >= 256 ? _mouseFd : _keyboardFd;
                    if (fd < 0) return false;
                    // EVIOCGRAB suppresses events for other readers, but key-state
                    // queries still work. This also observes keys held at startup.
                    Array.Clear(_keySnapshot);
                    if (LinuxNative.Ioctl(fd, LinuxNative.EVIOCGKEY_64, _keySnapshot) >= 0)
                        return (_keySnapshot[code / 8] & (1 << (code % 8))) != 0;
                    if (Marshal.GetLastWin32Error() != 25) return false; // ENOTTY: finite test streams
                }
            }
            return Volatile.Read(ref _keyStates[code]) != 0;
        }

        private unsafe void WorkerLoop()
        {
            Span<InputEvent> buffer = stackalloc InputEvent[32];
            PollFd[] pollFds = new PollFd[2];

            try
            {
                while (!_shouldStop && !_inputLost)
                {
                    int nfds = 0;
                    lock (_lifecycleLock)
                    {
                        if (_mouseFd >= 0)
                        {
                            pollFds[nfds++] = new PollFd { Fd = _mouseFd, Events = LinuxNative.POLLIN };
                        }
                        if (_keyboardFd >= 0)
                        {
                            pollFds[nfds++] = new PollFd { Fd = _keyboardFd, Events = LinuxNative.POLLIN };
                        }
                    }

                    if (nfds == 0) break;

                    int ret = LinuxNative.Poll(pollFds, (nuint)nfds, 10);
                    if (ret <= 0) continue;

                    for (int i = 0; i < nfds; i++)
                    {
                        short revents = pollFds[i].Revents;
                        int fd = pollFds[i].Fd;

                        if ((revents & (LinuxNative.POLLHUP | LinuxNative.POLLERR)) != 0)
                        {
                            RetireFd(fd);
                            continue;
                        }

                        if ((revents & LinuxNative.POLLIN) == 0) continue;

                        fixed (InputEvent* ptr = buffer)
                        {
                            nint bytesRead = LinuxNative.Read(fd, (IntPtr)ptr, (nuint)(buffer.Length * sizeof(InputEvent)));
                            if (bytesRead > 0)
                            {
                                int count = (int)(bytesRead / sizeof(InputEvent));
                                ProcessEvents(buffer.Slice(0, count), fd == _mouseFd);
                            }
                            else if (bytesRead == 0)
                            {
                                RetireFd(fd);
                            }
                            else
                            {
                                int errno = Marshal.GetLastWin32Error();
                                if (errno != 11 && errno != 4) // 11=EAGAIN, 4=EINTR
                                {
                                    RetireFd(fd);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _inputLost = true;
                AppLogger.Instance.LogError("Input reader stopped unexpectedly", ex);
            }
            finally
            {
                lock (_lifecycleLock)
                {
                    CloseDevices();
                    ResetState();
                }
            }
        }

        private void RetireFd(int fd)
        {
            lock (_lifecycleLock)
            {
                _inputLost = true;
                ResetState();
                if (fd == _mouseFd && _mouseFd >= 0)
                {
                    LinuxNative.Close(_mouseFd);
                    _mouseFd = -1;
                }
                else if (fd == _keyboardFd && _keyboardFd >= 0)
                {
                    LinuxNative.Close(_keyboardFd);
                    _keyboardFd = -1;
                }
            }
        }

        private void ProcessEvents(ReadOnlySpan<InputEvent> events, bool mouse)
        {
            ref bool discardFrame = ref (mouse ? ref _discardMouseFrame : ref _discardKeyboardFrame);
            int deltaX = 0;
            for (int i = 0; i < events.Length; i++)
            {
                ref readonly InputEvent ev = ref events[i];
                if (_inputLost) return;
                if (ev.Type == LinuxNative.EV_SYN && ev.Code == LinuxNative.SYN_DROPPED)
                {
                    if (mouse) MouseEvent?.Invoke(ev);
                    ResetState();
                    deltaX = 0;
                    discardFrame = true;
                    continue;
                }
                if (discardFrame)
                {
                    if (ev.Type == LinuxNative.EV_SYN && ev.Code == LinuxNative.SYN_REPORT) discardFrame = false;
                    continue;
                }
                if (mouse) MouseEvent?.Invoke(ev);
                if (ev.Type == LinuxNative.EV_REL && ev.Value != 0 && (ev.Code == LinuxNative.REL_X || ev.Code == LinuxNative.REL_Y))
                    Interlocked.Increment(ref _movementVersion);
                if (ev.Type == LinuxNative.EV_KEY && ev.Value == 1)
                {
                    Interlocked.Increment(ref _pressVersion);
                    if (ev.Code < 256) Interlocked.Increment(ref _keyboardPressVersion);
                }
                if (ev.Type == LinuxNative.EV_REL && ev.Code == LinuxNative.REL_X)
                {
                    deltaX += ev.Value;
                }
                else if (ev.Type == LinuxNative.EV_KEY && ev.Code < _keyStates.Length)
                {
                    Volatile.Write(ref _keyStates[ev.Code], (byte)(ev.Value != 0 ? 1 : 0));
                }
            }

            if (deltaX != 0)
            {
                Interlocked.Add(ref _pendingDeltaX, deltaX);
            }
        }

        private static string DiscoverMouseDevice()
        {
            if (Directory.Exists("/dev/input/by-id"))
            {
                foreach (string file in Directory.GetFiles("/dev/input/by-id"))
                {
                    if (file.EndsWith("-event-mouse", StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }

            string? fallbackPath = null;
            byte[] relBits = new byte[4];

            for (int i = 0; i < 32; i++)
            {
                string path = $"/dev/input/event{i}";
                if (!File.Exists(path)) continue;

                if (fallbackPath == null) fallbackPath = path;

                int testFd = LinuxNative.Open(path, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
                if (testFd >= 0)
                {
                    try
                    {
                        Array.Clear(relBits, 0, relBits.Length);
                        int ret = LinuxNative.Ioctl(testFd, LinuxNative.EVIOCGBIT_REL_4, relBits);
                        if (ret >= 0 && (relBits[0] & (1 << LinuxNative.REL_X)) != 0)
                        {
                            return path;
                        }
                    }
                    finally
                    {
                        LinuxNative.Close(testFd);
                    }
                }
            }

            return fallbackPath ?? "/dev/input/event0";
        }

        private static string? DiscoverKeyboardDevice()
        {
            if (Directory.Exists("/dev/input/by-id"))
            {
                foreach (string file in Directory.GetFiles("/dev/input/by-id"))
                {
                    if (file.EndsWith("-event-kbd", StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }
            // Built-in and wireless keyboards do not necessarily have a by-id link.
            byte[] keys = new byte[96];
            for (int i = 0; i < 32; i++)
            {
                string path = $"/dev/input/event{i}";
                int fd = LinuxNative.Open(path, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
                if (fd < 0) continue;
                try
                {
                    Array.Clear(keys);
                    if (LinuxNative.Ioctl(fd, LinuxNative.EVIOCGBIT_KEY_96, keys) >= 0 &&
                        (keys[16 / 8] & (1 << (16 % 8))) != 0) return path; // KEY_Q
                }
                finally { LinuxNative.Close(fd); }
            }
            return null;
        }

        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            Stop();
        }
    }
}
