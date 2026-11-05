# Building the distribution layout

The .NET project builds normally with its own apphost. Windows packages use that
kind of host directly at the root, bound to `app/vWheelHub.dll`; they do not run a
second hub executable. Linux keeps the small shell entry beside `app/`.
These commands are local builds, not substitutes for the maintainer's complete
dependency, edition and release audits.

Windows uses .NET SDK **10.0.401** and host pack **10.0.12**. First publish:

```powershell
dotnet publish src/vWheelHub.Avalonia/vWheelHub.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -o output/win/app
```

Save this temporary build project as `output/win/RootHost.proj`. The SDK task
copies the original host's icon, version and manifest, and binds the root host
to the managed entry without an intermediate process:

```xml
<Project>
  <UsingTask TaskName="CreateAppHost" AssemblyFile="$(MSBuildToolsPath)/Sdks/Microsoft.NET.Sdk/tools/net10.0/Microsoft.NET.Build.Tasks.dll" />
  <Target Name="CreateRootHost">
    <CreateAppHost
      AppHostSourcePath="$(MSBuildToolsPath)/../../packs/Microsoft.NETCore.App.Host.win-x64/10.0.12/runtimes/win-x64/native/apphost.exe"
      AppHostDestinationPath="$(MSBuildThisFileDirectory)vWheelHub.exe"
      AppBinaryName="app/vWheelHub.dll"
      IntermediateAssembly="$(MSBuildThisFileDirectory)app/vWheelHub.exe"
      WindowsGraphicalUserInterface="true" />
  </Target>
</Project>
```

```powershell
dotnet msbuild output/win/RootHost.proj -t:CreateRootHost
Remove-Item -LiteralPath output/win/app/vWheelHub.exe,output/win/RootHost.proj
./output/win/vWheelHub.exe --preview --smoke
```

The maintainer's `scripts/distribution_layout.py` also checks the SDK version and
host-template hash. The old C wrapper is no longer built for Windows packages.

Linux requires .NET SDK 10 and `/bin/sh`:

```sh
dotnet publish src/vWheelHub.Avalonia/vWheelHub.Avalonia.csproj -c Release -r linux-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -o output/linux/app
cp src/Launcher/vWheelHub.sh output/linux/vWheelHub
chmod +x output/linux/vWheelHub output/linux/app/vWheelHub
./output/linux/vWheelHub --preview --smoke
```

Move the entire `output/win` or `output/linux` directory. User data paths are
independent of the entry-point location. No precompiled application launcher is
needed to build from the source archive.
