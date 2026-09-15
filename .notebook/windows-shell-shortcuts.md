# Windows Shell Shortcuts
> Hosted folder view handles file commands; Firaw frame handles navigation

Entry: `FolderPin.cs:FolderWindow.PreFilterMessage()`

Flow: WinForms message filter → frame shortcuts → `FolderWindow.TraduzAtalhoDaPasta()` → `IShellView.TranslateAccelerator()`
- Frame: back/forward/up, address focus, tabs, view modes
- Shell view: selection, clipboard, rename, delete, undo/redo, item context actions
- Forward only while focus belongs to current Shell view; address/rename text fields keep editing behavior

Simple mode: `FolderPin.cs:FolderWindow.MontaBarraSimples()` — back + forward only

Brand migration:
- User-facing name and binaries: Firaw - TaskBar
- Legacy install key/path, property bag, shortcut descriptions recognized for in-place upgrades

Updated: 2026-09-15
