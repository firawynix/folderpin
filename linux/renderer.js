const api = window.folderpin;
const query = new URLSearchParams(location.search);
const state = { tabs: [{ path: query.get('folder') || '/', history: [] }], active: 0, data: null, grid: false, selected: null };
const $ = (id) => document.getElementById(id);

function tab() { return state.tabs[state.active]; }
function shortName(value) { return value === '/' ? '/' : value.split('/').filter(Boolean).pop(); }
function setStatus(text, bad = false) { $('status').textContent = text; $('status').classList.toggle('bad', bad); }

function renderTabs() {
  $('tabs').replaceChildren(...state.tabs.map((item, index) => {
    const button = document.createElement('button');
    button.className = index === state.active ? 'active' : '';
    button.textContent = shortName(item.path);
    button.title = item.path;
    button.onclick = () => { state.active = index; load(item.path, false); };
    if (state.tabs.length > 1) {
      const close = document.createElement('i'); close.textContent = '×'; close.onclick = (event) => {
        event.stopPropagation(); state.tabs.splice(index, 1); state.active = Math.min(state.active, state.tabs.length - 1); load(tab().path, false);
      }; button.append(close);
    }
    return button;
  }));
}

function renderCrumbs(folder) {
  const parts = folder.split('/').filter(Boolean); let current = '';
  const nodes = [crumb('/', '/')];
  for (const part of parts) { current += `/${part}`; nodes.push(crumb(part, current)); }
  $('crumbs').replaceChildren(...nodes);
}
function crumb(label, target) { const b = document.createElement('button'); b.textContent = label; b.onclick = () => navigate(target); return b; }

function renderItems() {
  const term = $('search').value.trim().toLocaleLowerCase('pt-BR');
  const items = (state.data?.items || []).filter((x) => x.name.toLocaleLowerCase('pt-BR').includes(term));
  $('items').className = state.grid ? 'grid' : 'list';
  $('empty').hidden = items.length !== 0;
  $('items').replaceChildren(...items.map((item) => {
    const row = document.createElement('button'); row.className = 'item'; row.dataset.path = item.path;
    const icon = document.createElement('span'); icon.className = 'icon'; icon.textContent = item.directory ? '▰' : '▤';
    const name = document.createElement('strong'); name.textContent = item.name;
    const info = document.createElement('small'); info.textContent = item.directory ? 'Pasta' : formatBytes(item.size);
    const date = document.createElement('time'); date.textContent = item.modified ? new Date(item.modified).toLocaleString('pt-BR') : '';
    row.append(icon, name, info, date);
    row.ondblclick = () => item.directory ? navigate(item.path) : api.open(item.path).catch(showError);
    row.oncontextmenu = (event) => { event.preventDefault(); state.selected = item; showMenu(event.clientX, event.clientY); };
    return row;
  }));
  setStatus(`${items.length} item(ns) · ${state.data?.folder || ''}`);
}
function formatBytes(value) { if (value == null) return ''; if (value < 1024) return `${value} B`; const u=['KB','MB','GB','TB']; let n=value/1024,i=0; while(n>=1024&&i<u.length-1){n/=1024;i++;} return `${n<10?n.toFixed(1):Math.round(n)} ${u[i]}`.replace('.',','); }

async function load(folder, remember = true) {
  setStatus('Carregando…');
  try {
    const data = await api.list(folder); const current = tab();
    if (remember && current.path !== data.folder) current.history.push(current.path);
    current.path = data.folder; state.data = data; document.title = `${shortName(data.folder)} — FolderPin`;
    renderTabs(); renderCrumbs(data.folder); renderItems(); $('up').disabled = !data.parent;
  } catch (error) { showError(error); }
}
function navigate(folder) { return load(folder, true); }
function showError(error) { setStatus(error?.message || String(error), true); }
function showMenu(x, y) { const menu=$('menu'); menu.style.left=`${x}px`; menu.style.top=`${y}px`; menu.setAttribute('open',''); }
function closeMenu(){ $('menu').removeAttribute('open'); }

$('choose').onclick = async () => { const folder = await api.choose(); if (folder) navigate(folder); };
$('newTab').onclick = () => { state.tabs.push({ path: tab().path, history: [] }); state.active = state.tabs.length - 1; load(tab().path, false); };
$('back').onclick = () => { const previous = tab().history.pop(); if (previous) load(previous, false); };
$('up').onclick = () => state.data?.parent && navigate(state.data.parent);
$('view').onclick = () => { state.grid = !state.grid; renderItems(); };
$('search').oninput = renderItems;
$('newFolder').onclick = async () => { const name=prompt('Nome da nova pasta:'); if(name) try{await api.create(tab().path,name); await load(tab().path,false);}catch(e){showError(e);} };
$('menu').onclick = async (event) => {
  const action=event.target.dataset.action,item=state.selected; if(!action||!item)return; closeMenu();
  try {
    if(action==='open') item.directory?await navigate(item.path):await api.open(item.path);
    if(action==='show') await api.show(item.path);
    if(action==='rename'){const name=prompt('Novo nome:',item.name);if(name){await api.rename(item.path,name);await load(tab().path,false);}}
    if(action==='trash'&&confirm(`Mover “${item.name}” para a lixeira?`)){await api.trash(item.path);await load(tab().path,false);}
  } catch(e){showError(e);}
};
document.addEventListener('click', closeMenu);
load(tab().path, false);
