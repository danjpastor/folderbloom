# FolderBloom

Windows desktop folders that expand from colorable icons. This source baseline is version 0.7.1.0, the compact-layout candidate approved on 2026-10-15. It supersedes the older 0.7 root source.

## Build and test

Requires Windows 10/11 x64 with .NET Framework 4.8 and its C# compiler/WPF assemblies. No package installation is needed.

```powershell
pwsh -NoProfile -File ./build.ps1
./FolderBloom.exe --self-test "$PWD/test-output"
```

Use a disposable test directory. `--data-dir <directory>` selects separate app settings. Building and self-tests do not require installing the app. Normal app use may change desktop icon visibility and, when explicitly selected in app settings, register Explorer commands or sign-in startup.

## Baseline and validation limits

Imported from the versioned `collapsed-layout-20261015-candidate/Source` snapshot. No Git history existed in that snapshot; the unmodified source was backed up privately before this repository was prepared. The installed executable and personal settings are excluded.

The baseline record reports 174 self-tests and 17 grid checks. Those are historical results; see SOURCE-VALIDATION.md for checks run during repository preparation. Live pointer hover/drag and mixed-monitor DPI acceptance remain pending. This is a development build, not a signed or production-certified installer.

## License and artwork

The existing LICENSE.txt applies to the original FolderBloom code. The source contains no DeskRipple code or bundled artwork. Windows system artwork is obtained at runtime and remains subject to Microsoft's terms.
