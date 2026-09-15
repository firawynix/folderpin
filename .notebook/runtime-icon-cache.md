# Runtime icon cache
> Window captions can keep an old icon when the executable path is reused

Entry: `build.cmd` icon/resource arguments

Flow:
- `firaw-taskbar.ico` enters each executable as Win32 icon and named resource `FirawTaskBarIcon`.
- `FolderPin.cs:Native.IconeDoAplicativo()` sets the default folder-window icon before an optional per-shortcut icon overrides it.
- `FolderPinStudio.cs:Sys.IconeDoAplicativo()` and `FolderPinSetup.cs:Amb.IconeDoAplicativo()` set their form icons explicitly.
- `FolderPinSetup.cs:Instalador.Instalar()` calls `SHChangeNotify` after replacing installed files and shortcuts.

Reason: relying only on `/win32icon` lets Windows reuse the cached icon for an unchanged installed path.

Updated: 2026-09-15
