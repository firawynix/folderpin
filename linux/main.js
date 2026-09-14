const { app, BrowserWindow, dialog, ipcMain, shell } = require('electron');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { safePath, visibleEntry } = require('./core');

app.setDesktopName('br.com.firawynix.folderpin.desktop');

function initialFolder() {
  const candidate = process.argv.slice(1).find((arg) => !arg.startsWith('-') && path.isAbsolute(arg));
  try { return candidate && require('node:fs').statSync(candidate).isDirectory() ? safePath(candidate) : os.homedir(); }
  catch { return os.homedir(); }
}

async function listFolder(folder) {
  const target = safePath(folder);
  const stat = await fs.stat(target);
  if (!stat.isDirectory()) throw new Error('Este caminho não é uma pasta.');
  const names = await fs.readdir(target, { withFileTypes: true });
  const items = await Promise.all(names.filter((x) => visibleEntry(x.name)).map(async (entry) => {
    const fullPath = path.join(target, entry.name);
    let info = null;
    try { info = await fs.stat(fullPath); } catch { /* item pode desaparecer durante a leitura */ }
    return {
      name: entry.name,
      path: fullPath,
      directory: entry.isDirectory(),
      symbolicLink: entry.isSymbolicLink(),
      size: info?.size ?? null,
      modified: info?.mtime?.toISOString() ?? null,
    };
  }));
  items.sort((a, b) => Number(b.directory) - Number(a.directory) || a.name.localeCompare(b.name, 'pt-BR', { numeric: true }));
  return { folder: target, parent: path.dirname(target) === target ? null : path.dirname(target), items };
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1120,
    height: 760,
    minWidth: 720,
    minHeight: 480,
    title: 'FolderPin',
    backgroundColor: '#071116',
    autoHideMenuBar: true,
    webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true, sandbox: true },
  });
  win.loadFile('index.html', { query: { folder: initialFolder() } });
}

ipcMain.handle('folder:list', (_event, folder) => listFolder(folder));
ipcMain.handle('folder:choose', async () => {
  const result = await dialog.showOpenDialog({ title: 'Escolha uma pasta', properties: ['openDirectory', 'createDirectory'] });
  return result.canceled ? null : result.filePaths[0];
});
ipcMain.handle('folder:open', async (_event, target) => {
  const error = await shell.openPath(safePath(target));
  if (error) throw new Error(error);
  return true;
});
ipcMain.handle('folder:show', (_event, target) => shell.showItemInFolder(safePath(target)));
ipcMain.handle('folder:create', async (_event, folder, name) => {
  const clean = String(name || '').trim();
  if (!clean || clean === '.' || clean === '..' || /[\/\0]/.test(clean)) throw new Error('Nome de pasta inválido.');
  await fs.mkdir(path.join(safePath(folder), clean));
  return true;
});
ipcMain.handle('folder:rename', async (_event, target, name) => {
  const clean = String(name || '').trim();
  if (!clean || clean === '.' || clean === '..' || /[\/\0]/.test(clean)) throw new Error('Nome inválido.');
  const source = safePath(target);
  await fs.rename(source, path.join(path.dirname(source), clean));
  return true;
});
ipcMain.handle('folder:trash', async (_event, target) => shell.trashItem(safePath(target)));

app.whenReady().then(createWindow);
app.on('window-all-closed', () => app.quit());
