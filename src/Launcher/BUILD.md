# Building the launcher layout

The .NET project builds normally without a launcher. To reproduce the clean
distribution layout, publish the application under `app/` and build/copy the
launcher beside it. Keep all published resources and libraries inside `app/`.
These commands are local builds, not substitutes for the maintainer's complete
dependency, edition and release audits.

Windows requires .NET SDK 10 and Zig **0.16.0** in PATH. From the source root:

```powershell
dotnet publish src/vWheelHub.Avalonia/vWheelHub.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -o output/win/app
zig cc -target x86_64-windows-gnu -O2 -ffreestanding -fno-builtin -fno-stack-protector -nostdlib '-Wl,--entry,WinMainCRTStartup' '-Wl,--subsystem,windows' '-Wl,--strip-all' src/Launcher/launcher.c -lkernel32 -luser32 -o output/win/vWheelHub.exe
Copy-Item src/Launcher/README.txt output/win/README.txt
./output/win/vWheelHub.exe --preview --smoke
```

Linux requires .NET SDK 10 and `/bin/sh`:

```sh
dotnet publish src/vWheelHub.Avalonia/vWheelHub.Avalonia.csproj -c Release -r linux-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -o output/linux/app
cp src/Launcher/vWheelHub.sh output/linux/vWheelHub
cp src/Launcher/README.txt output/linux/README.txt
chmod +x output/linux/vWheelHub output/linux/app/vWheelHub
./output/linux/vWheelHub --preview --smoke
```

Move the entire `output/win` or `output/linux` directory. User data paths are
independent of the launcher location. Compiler source and ABI declarations are
included here; no precompiled launcher is needed to build the source archive.
