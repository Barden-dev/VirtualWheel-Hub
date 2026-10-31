# vWheel Hub

Mouse and keyboard steering for racing games on Windows and Linux, with a shared
Avalonia desktop interface.

Free is the no-cost edition distributed through [GitHub](https://github.com/Barden-dev/VirtualWheel-Hub).
Use and distribution are subject to [LICENSE](LICENSE).

## Features

- Steering, throttle and brake response, calibration, curves and inversion.
- Synthetic Trail Braking: steering-based brake limit, intensity and release time.
- Key bindings and manual cursor locking. Startup leaves the hub stopped by default.
  Global shortcuts on Linux remain available while the hub is stopped.
- Profiles with game, class and car inheritance, import/export, search and undo.
- Windows vJoy wheel output; Linux uinput output.
- Game process detection and a live output monitor.
- English, Russian, German, Spanish, French and Brazilian Portuguese interface.

## Run

The 2.0 candidates include Windows and Linux packages. Actual Free input/game
acceptance is separate from PRO and from automated checks.
Windows and Linux archives include .NET; no separate runtime installation is needed.
Windows output uses vJoy. Linux source builds require write access to `/dev/uinput`,
read access to the mouse/keyboard event devices under `/dev/input`, and
the native libraries required by Avalonia (X11/Wayland and font rendering).

Extract the entire archive. Start `vWheelHub.exe` on Windows. On Linux, open a
terminal in the extracted folder and run as your normal user (without `sudo`):

```sh
chmod +x ./vWheelHub ./app/vWheelHub
./vWheelHub
```

Keep the `app/` directory beside the launcher. Native system libraries and device
permissions are still required. Free contains the basic controls and device
tools listed above.

Open **Dashboard → Open device diagnostics** to check or prepare the virtual wheel.
Select your input devices in **Controls → Input devices** and check your key bindings, then start the
hub explicitly, or enable starting the hub when the app opens in Settings.
Assign axes and buttons inside the game manually; the hub monitor only proves
hub output. The application does not enable cursor locking at startup.
Steering, throttle and brake settings are under **Controls → Axis controls**.
Losing an input device stops the hub, resets the output and releases the cursor.
After reconnecting the device, start the hub again explicitly.

Settings → Startup and window also offers starting minimized, minimizing to tray
and closing to tray. The tray menu restores the window, starts/stops the hub and
exits with the usual unsaved-profile confirmation. If no system tray is available,
the window stays on the taskbar and the close button exits normally. On Linux,
tray hiding requires a StatusNotifier host and the `dbus-send` utility; otherwise
the taskbar fallback is used. These options do not register application startup
with Windows or Linux.
