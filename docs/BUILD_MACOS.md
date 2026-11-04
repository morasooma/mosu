# Building Morasooma for macOS (Apple Silicon)

Unofficial. Verified by running 2026.1014.0 on an M1 (macOS 27.0.1).

## 1. Build

```sh
dotnet publish osu.Desktop -c Release -r osx-arm64 --self-contained -o publish
```

Target framework is net10.0 and `osx-arm64` is already listed in
`<RuntimeIdentifiers>` of `osu.Desktop.csproj`.

### Why `-r` is required

In `osu.Desktop.csproj`, `PlatformTarget` is derived from the
`RuntimeIdentifier`, and a default `RuntimeIdentifier` is set only on Linux
(`linux-x64`). On macOS without `-r` the identifier stays empty and
`PlatformTarget` falls back to `x64`. The resulting `Morasooma.dll` has
PE Machine = AMD64 and an arm64 process refuses to load it:

```
FileLoadException: Could not load file or assembly '.../Morasooma.dll'.
The assembly architecture is not compatible with the current process architecture.
```

With `-r osx-arm64` the `PlatformTarget` becomes `arm64`.

Suggested follow-up: default `RuntimeIdentifier` on macOS to `osx-arm64` /
`osx-x64` according to the host architecture, the way it is already done for
Linux (which also assumes x64 on arm64 hosts).

Do not reuse the linux-x64 output: the host assembly is AMD64-only.

## 2. Bundle

```
Morasooma.app/Contents/
Info.plist    CFBundleExecutable=Morasooma, own CFBundleIdentifier
Resources/    icon
MacOS/        contents of ./publish (native dylibs come from the osx-arm64 RID)
```

Do not ship Linux-only files (`libmosu_audio_bridge.so`, `UpdateNix`,
`sq.version`).

## 3. Sign

Ad-hoc, for local use:

```sh
codesign --force --deep -s - Morasooma.app
codesign --force -s - -o runtime --entitlements ent.plist Morasooma.app/Contents/MacOS/Morasooma
```

`ent.plist` needs `com.apple.security.cs.allow-jit` and
`com.apple.security.cs.allow-dyld-environment-variables` (same as official
osu!). For distribution: Developer ID signing plus notarization.

## 4. Updates

Velopack needs its own channel for macOS, e.g. `stable-osx`, next to `stable`
and `stable-linux`. Without it the macOS client resolves `stable` to the
Windows package: it downloaded `mosu-<ver>-stable-full.nupkg`
(os=win, rid=win-x64) into `~/Library/Caches/velopack/mosu`.

## Workaround without a native build

Take the official osu!lazer `osu.app.Apple.Silicon` bundle, copy the managed
Morasooma/osu.Game/osu.Framework assemblies and resources from the linux-x64
AppImage into it, switch the Machine field of `Morasooma.dll` to ARM64,
repoint the apphost to `Morasooma.dll`, remove the updater, and sign.

## 5. First launch of an unsigned / ad-hoc signed build

Gatekeeper blocks apps that are not notarized. For users of a release build,
there are three options.

Option A: open the app once (macOS refuses), then go to
System Settings -> Privacy & Security, scroll down to the "Morasooma was
blocked" message and click **Open Anyway**.

Option B: remove the quarantine flag in the terminal:
`xattr -cr /Applications/Morasooma.app`

Option C: disable signature checks system-wide with
`sudo spctl --master-disable`. This works, but is not recommended: it turns
off Gatekeeper for all apps.

This should be explained on the download page. Developer ID signing plus
notarization removes the problem entirely.
