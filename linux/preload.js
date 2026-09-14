const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('folderpin', {
  list: (folder) => ipcRenderer.invoke('folder:list', folder),
  choose: () => ipcRenderer.invoke('folder:choose'),
  open: (target) => ipcRenderer.invoke('folder:open', target),
  show: (target) => ipcRenderer.invoke('folder:show', target),
  create: (folder, name) => ipcRenderer.invoke('folder:create', folder, name),
  rename: (target, name) => ipcRenderer.invoke('folder:rename', target, name),
  trash: (target) => ipcRenderer.invoke('folder:trash', target),
});
