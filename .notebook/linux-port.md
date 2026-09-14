# Linux port

The original C# shell-host implementation remains the Windows edition. The
native Linux edition lives under `linux/` and is an Electron file manager with
independent folder windows, tabs, breadcrumbs, search, grid/list views and safe
create, rename, open and trash operations.

Run `npm test` in `linux/`; package with `npm run build:linux`. Published builds
use `FolderPin-<version>-x86_64.AppImage`. The stable Center URL is
`https://jogos.firawynix.com.br/api/games/folderpin/linux/arquivo`.
