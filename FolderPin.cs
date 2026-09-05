using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[StructLayout(LayoutKind.Sequential)]
struct RECT { public int left, top, right, bottom; }

[StructLayout(LayoutKind.Sequential)]
struct FOLDERSETTINGS { public uint ViewMode; public uint fFlags; }

static class Native
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindCtx,
        out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHGetNameFromIDList(
        IntPtr pidl, uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);

    [DllImport("shell32.dll")]
    public static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr bc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object item);

    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depoisDe, string classe, string titulo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool apaga);

    // Windows guarda o destaque em ABGR; o painel de controle chama de "cor de destaque".
    public static Color CorDeDestaque()
    {
        try
        {
            object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                "AccentColor", null);
            if (v == null) return Color.Empty;
            uint n = unchecked((uint)Convert.ToInt32(v));
            return Color.FromArgb((int)(n & 0xFF), (int)((n >> 8) & 0xFF), (int)((n >> 16) & 0xFF));
        }
        catch { return Color.Empty; }
    }

    // "Mostrar cor de destaque em barras de titulo e bordas de janela"
    public static bool DestaqueNasSuperficies()
    {
        try
        {
            object v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "ColorPrevalence", 0);
            return v != null && Convert.ToInt32(v) != 0;
        }
        catch { return false; }
    }

    // IShellItem a partir de caminho de disco ou local do shell ("shell:...", "::{CLSID}").
    public static IShellItem ItemDe(string caminho)
    {
        if (string.IsNullOrEmpty(caminho)) return null;
        try
        {
            Guid iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
            object o;
            SHCreateItemFromParsingName(caminho, IntPtr.Zero, ref iid, out o);
            return o as IShellItem;
        }
        catch { return null; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    public static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    public static extern void FlushMenuThemes();

    [DllImport("uxtheme.dll", EntryPoint = "#133")]
    public static extern bool AllowDarkModeForWindow(IntPtr hwnd, bool allow);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int PrivateExtractIcons(string file, int index, int cx, int cy,
        IntPtr[] icons, int[] ids, int count, int flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    // Aceita "arquivo.ico", "shell32.dll,15" e "imageres.dll,-109".
    public static void SeparaIcone(string spec, out string arquivo, out int indice)
    {
        arquivo = spec;
        indice = 0;
        int v = spec.LastIndexOf(',');
        if (v <= 0) return;

        int n;
        string cauda = spec.Substring(v + 1).Trim();
        if (int.TryParse(cauda, out n))
        {
            arquivo = spec.Substring(0, v).Trim();
            indice = n < 0 ? -n : n;   // indice negativo no registro e ID de recurso
        }
    }

    // O Icon do .NET nao decodifica quadro PNG dentro de .ico (devolve chuvisco);
    // o extrator do shell decodifica.
    public static Icon CarregaIcone(string spec, int tamanho)
    {
        string arquivo;
        int indice;
        SeparaIcone(spec, out arquivo, out indice);
        if (!System.IO.File.Exists(arquivo)) return null;

        IntPtr[] h = new IntPtr[1];
        int[] ids = new int[1];
        try
        {
            int n = PrivateExtractIcons(arquivo, indice, tamanho, tamanho, h, ids, 1, 0);
            if (n <= 0 || h[0] == IntPtr.Zero) return null;
            using (Icon tmp = Icon.FromHandle(h[0]))
                return (Icon)tmp.Clone();
        }
        catch { return null; }
        finally { if (h[0] != IntPtr.Zero) DestroyIcon(h[0]); }
    }

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int APPMODE_FORCE_DARK = 2;
    public const int APPMODE_FORCE_LIGHT = 3;

    public static bool SystemPrefersDark()
    {
        try
        {
            object v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v != null && Convert.ToInt32(v) == 0;
        }
        catch { return false; }
    }

    public static void DarkTitleBar(IntPtr hwnd, bool dark)
    {
        int on = dark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)); }
        catch { }
    }
}

[ComImport, Guid("361BBDC7-E6EE-4E13-BE58-58E2240C810F"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IExplorerBrowserEvents
{
    void OnNavigationPending(IntPtr pidlFolder);
    void OnViewCreated([MarshalAs(UnmanagedType.IUnknown)] object psv);
    void OnNavigationComplete(IntPtr pidlFolder);
    void OnNavigationFailed(IntPtr pidlFolder);
}

[ComImport, Guid("DFD3B6B5-C10C-4BE9-85F6-A66969F402F6"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IExplorerBrowser
{
    void Initialize(IntPtr hwndParent, ref RECT prc, ref FOLDERSETTINGS pfs);
    void Destroy();
    void SetRect(IntPtr phdwp, RECT rcBrowser);
    void SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string bag);
    void SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string text);
    void SetFolderSettings(ref FOLDERSETTINGS pfs);
    void Advise(IExplorerBrowserEvents psbe, out uint cookie);
    void Unadvise(uint cookie);
    void SetOptions(uint flags);
    void GetOptions(out uint flags);
    void BrowseToIDList(IntPtr pidl, uint uFlags);
    void BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object punk, uint uFlags);
    void FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, int flags);
    void RemoveAll();
    [return: MarshalAs(UnmanagedType.IUnknown)] object GetCurrentView(ref Guid riid);
}

[ComImport, Guid("000214E3-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellView
{
    void GetWindow(out IntPtr phwnd);
    void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
    [PreserveSig] int TranslateAccelerator(IntPtr pmsg);
    [PreserveSig] int EnableModeless([MarshalAs(UnmanagedType.Bool)] bool fEnable);
    void UIActivate(uint uState);
    void Refresh();
    void CreateViewWindow(IShellView psvPrevious, IntPtr pfs, IntPtr psb, ref RECT prcView, out IntPtr phWnd);
    void DestroyViewWindow();
    void GetCurrentInfo(IntPtr pfs);
    void AddPropertySheetPages(uint dwReserved, IntPtr pfn, IntPtr lparam);
    void SaveViewState();
    void SelectItem(IntPtr pidlItem, uint uFlags);
    void GetItemObject(uint uItem, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItem
{
    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItemArray
{
    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
    void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
    void GetAttributes(int dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
    void GetCount(out uint pdwNumItems);
    void GetItemAt(uint dwIndex, out IShellItem ppsi);
    void EnumItems(out IntPtr ppenumShellItems);
}

[ComImport, Guid("00000114-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IOleWindow
{
    void GetWindow(out IntPtr phwnd);
    void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
}

// Mesmo controle de arvore que o Explorer usa, mas com raiz propria:
// AppendRoot decide onde a arvore comeca, e nada acima dela aparece.
[ComImport, Guid("028212A3-B627-47E9-8856-C14265554E4F"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface INameSpaceTreeControl
{
    void Initialize(IntPtr hwndParent, ref RECT prc, uint nstcsFlags);
    void TreeAdvise(IntPtr punk, out uint pdwCookie);
    void TreeUnadvise(uint dwCookie);
    void AppendRoot(IShellItem psiRoot, uint grfEnumFlags, uint grfRootStyle, IntPtr pif);
    void InsertRoot(int iIndex, IShellItem psiRoot, uint grfEnumFlags, uint grfRootStyle, IntPtr pif);
    void RemoveRoot(IShellItem psiRoot);
    void RemoveAllRoots();
    void GetRootItems(out IShellItemArray ppsiaRootItems);
    void SetItemState(IShellItem psi, uint nstcisMask, uint nstcisFlags);
    void GetItemState(IShellItem psi, uint nstcisMask, out uint pnstcisFlags);
    void GetSelectedItems(out IShellItemArray psiaItems);
    void GetItemCustomState(IShellItem psi, out int piStateNumber);
    void SetItemCustomState(IShellItem psi, int iStateNumber);
    void EnsureItemVisible(IShellItem psi);
    void SetTheme([MarshalAs(UnmanagedType.LPWStr)] string pszTheme);
    void GetNextItem(IShellItem psi, uint nstcgi, out IShellItem ppsiNext);
    void HitTest(IntPtr ppt, out IShellItem ppsiOut);
    void GetItemRect(IShellItem psi, out RECT prect);
    void CollapseAll();
}

[ComVisible(true)]
class BrowserEvents : IExplorerBrowserEvents
{
    readonly Action<IntPtr> onDone;
    public BrowserEvents(Action<IntPtr> cb) { onDone = cb; }
    public void OnNavigationPending(IntPtr pidl) { }
    public void OnViewCreated(object psv) { }
    public void OnNavigationComplete(IntPtr pidl) { onDone(pidl); }
    public void OnNavigationFailed(IntPtr pidl) { }
}

class Aba
{
    public Panel Host;
    public IExplorerBrowser Browser;
    public BrowserEvents Eventos;
    public uint Cookie;
    public string Titulo = "";
    public string Caminho = "";
    public Rectangle Rect;
    public Rectangle Fechar;
}

class Faixa : Panel
{
    public Faixa()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }
}

class FolderWindow : Form, IMessageFilter
{
    const uint EBO_SHOWFRAMES = 0x2;
    const uint SHCONTF_FOLDERS = 0x20;
    const uint SHCONTF_NONFOLDERS = 0x40;
    const uint SFGAO_FOLDER = 0x20000000;
    const uint NSTCRS_EXPANDED = 0x2;
    const uint NSTCIS_SELECTED = 0x1;
    const uint NSTCS_HASEXPANDOS = 0x1;
    const uint NSTCS_FULLROWSELECT = 0x8;
    const uint NSTCS_ROOTHASEXPANDO = 0x40;
    const uint NSTCS_SHOWSELECTIONALWAYS = 0x80;
    const uint NSTCS_TABSTOP = 0x20000;
    const uint NSTCS_AUTOHSCROLL = 0x100000;
    const uint NSTCS_FADEINOUTEXPANDOS = 0x200000;
    const int LarguraDivisor = 5;
    const uint NSTCIS_EXPANDED = 0x2;
    const int TVM_SETBKCOLOR = 0x111D;
    const int TVM_SETTEXTCOLOR = 0x111E;
    const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320;
    const int WM_THEMECHANGED = 0x31A;
    const uint EBO_NOBORDER = 0x40;
    const uint FVM_DETAILS = 4;
    const uint SBSP_PARENT = 0x2000;
    const uint SBSP_NAVIGATEBACK = 0x4000;
    const uint SBSP_NAVIGATEFORWARD = 0x8000;
    const uint SIGDN_NORMALDISPLAY = 0;
    const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
    const uint SIGDN_DESKTOPABSOLUTEEDITING = 0x8004C000;

    static readonly Color BgDark = Color.FromArgb(32, 32, 32);
    static readonly Color FieldDark = Color.FromArgb(45, 45, 45);
    static readonly Color TextDark = Color.FromArgb(235, 235, 235);
    static readonly Color HoverDark = Color.FromArgb(60, 60, 60);

    readonly List<Aba> abas = new List<Aba>();
    int ativa = -1;
    int sobre = -1;
    int sobreFechar = -1;
    bool sobreMais;
    Rectangle rectMais;

    readonly string startPath;
    readonly bool tree;
    readonly bool arvoreRaiz;
    readonly bool arvoreArquivos;
    readonly bool dark;
    readonly bool comAbas;

    Faixa faixa;
    Panel painelArvore, divisor;
    INameSpaceTreeControl nstc;
    IntPtr hwndArvore = IntPtr.Zero;
    int larguraArvore = 240;
    System.Windows.Forms.Timer relogioArvore;
    string ultimaSelecao = "";
    bool sincronizando, arrastando;
    Panel bar;
    Button btnBack, btnFwd, btnUp;
    TextBox txtPath;
    Font fonteAba, fonteGlifo;

    Color Bg { get { return dark ? BgDark : SystemColors.Control; } }
    Color Field { get { return dark ? FieldDark : SystemColors.Window; } }
    Color Fg { get { return dark ? TextDark : SystemColors.ControlText; } }
    Color Hover { get { return dark ? HoverDark : SystemColors.ControlLight; } }

    readonly bool simples;

    public FolderWindow(string path, string iconPath, bool showTree, bool useDark, bool maximized,
        bool tabs, bool janelaSimples, bool soEstaPasta, bool comArquivos)
    {
        startPath = path;
        simples = janelaSimples;
        // arvore propria: raiz na pasta escolhida, sem o resto do computador em cima
        arvoreRaiz = showTree && soEstaPasta && !simples;
        arvoreArquivos = arvoreRaiz && comArquivos;
        tree = showTree && !simples && !arvoreRaiz;
        dark = useDark;
        comAbas = tabs && !simples;
        if (maximized) WindowState = FormWindowState.Maximized;

        Log("ctor: campos ok");
        bool localDoShell = path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || path.StartsWith("::");
        Text = localDoShell ? "FolderPin"
             : System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        Log("ctor: janela dimensionada");

        if (!string.IsNullOrEmpty(iconPath))
        {
            Icon ic = Native.CarregaIcone(iconPath, 32);
            if (ic == null && System.IO.File.Exists(iconPath))
            {
                try { ic = new Icon(iconPath); } catch { }
            }
            if (ic != null) Icon = ic;
        }
        Log("ctor: icone ok");

        fonteAba = new Font("Segoe UI", 9f);
        Log("ctor: fonteAba ok");
        string gf = GlyphFont();
        Log("ctor: GlyphFont=" + gf);
        fonteGlifo = new Font(gf, 8f);

        Log("ctor: fontes ok");
        if (!simples) MontaBarra();     // janela crua: so a lista, nada de barra nem abas
        Log("ctor: barra ok");
        if (comAbas) MontaFaixa();
        Log("ctor: faixa ok");
        Application.AddMessageFilter(this);
        Log("ctor: fim");
    }

    static bool glifosOk = true;

    static string GlyphFont()
    {
        bool mdl2 = false;
        foreach (FontFamily f in FontFamily.Families)
        {
            if (f.Name == "Segoe Fluent Icons") { glifosOk = true; return "Segoe Fluent Icons"; }
            if (f.Name == "Segoe MDL2 Assets") mdl2 = true;
        }
        if (mdl2) { glifosOk = true; return "Segoe MDL2 Assets"; }
        glifosOk = false;   // Windows sem as fontes de glifo: cai em texto comum
        return "Segoe UI";
    }

    static string G(string glifo, string alternativa)
    {
        return glifosOk ? glifo : alternativa;
    }

    // ---------- barra de navegacao ----------

    Button MakeButton(string glyph, string tip, EventHandler onClick)
    {
        Button b = new Button();
        b.Text = glyph;
        b.Font = new Font(GlyphFont(), 11f);
        b.Size = new Size(38, 30);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Hover;
        b.BackColor = Bg;
        b.ForeColor = Fg;
        b.TabStop = false;
        b.Click += onClick;
        new ToolTip().SetToolTip(b, tip);
        return b;
    }

    void MontaBarra()
    {
        bar = new Panel();
        bar.Dock = DockStyle.Top;
        bar.Height = 40;
        bar.BackColor = Bg;

        btnBack = MakeButton(G("\uE72B", "\u2190"), "Voltar (Alt+Esquerda)", delegate { Go(SBSP_NAVIGATEBACK); });
        btnFwd = MakeButton(G("\uE72A", "\u2192"), "Avancar (Alt+Direita)", delegate { Go(SBSP_NAVIGATEFORWARD); });
        btnUp = MakeButton(G("\uE74A", "\u2191"), "Acima (Alt+Cima)", delegate { Go(SBSP_PARENT); });

        btnBack.Location = new Point(6, 5);
        btnFwd.Location = new Point(46, 5);
        btnUp.Location = new Point(86, 5);

        txtPath = new TextBox();
        txtPath.Location = new Point(130, 8);
        txtPath.Height = 24;
        txtPath.BorderStyle = BorderStyle.FixedSingle;
        txtPath.BackColor = Field;
        txtPath.ForeColor = Fg;
        txtPath.Font = fonteAba;
        txtPath.TabStop = false;
        txtPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtPath.KeyDown += delegate (object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Navega(Atual(), txtPath.Text.Trim());
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                FocaLista();
            }
        };

        bar.Controls.Add(btnBack);
        bar.Controls.Add(btnFwd);
        bar.Controls.Add(btnUp);
        bar.Controls.Add(txtPath);
        Controls.Add(bar);

        bar.Resize += delegate { txtPath.Width = Math.Max(120, bar.Width - 140); };
        txtPath.Width = Math.Max(120, ClientSize.Width - 140);
    }

    // ---------- faixa de abas ----------

    void MontaFaixa()
    {
        faixa = new Faixa();
        faixa.Dock = DockStyle.Top;
        faixa.Height = 34;
        faixa.BackColor = Bg;
        faixa.Paint += DesenhaFaixa;
        faixa.MouseDown += FaixaClique;
        faixa.MouseMove += FaixaMove;
        faixa.MouseLeave += delegate
        {
            sobre = -1; sobreFechar = -1; sobreMais = false; faixa.Invalidate();
        };
        faixa.MouseDoubleClick += delegate (object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Achar(e.Location) < 0 && !rectMais.Contains(e.Location))
                NovaAba(null);
        };
        Controls.Add(faixa);
        faixa.BringToFront();
        bar.BringToFront();
    }

    void CalculaAbas()
    {
        int x = 4;
        int disponivel = faixa.Width - 44;
        int largura = abas.Count > 0 ? Math.Min(210, Math.Max(90, disponivel / abas.Count)) : 0;

        for (int i = 0; i < abas.Count; i++)
        {
            abas[i].Rect = new Rectangle(x, 4, largura - 2, faixa.Height - 4);
            abas[i].Fechar = new Rectangle(abas[i].Rect.Right - 24, abas[i].Rect.Top + 7, 18, 18);
            x += largura;
        }
        rectMais = new Rectangle(x + 2, 8, 26, 20);
    }

    void DesenhaFaixa(object s, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Bg);
        CalculaAbas();

        for (int i = 0; i < abas.Count; i++)
        {
            Aba a = abas[i];
            Color fundo = i == ativa ? Field : (i == sobre ? Hover : Bg);

            using (GraphicsPath p = TopoArredondado(a.Rect, 6))
            using (SolidBrush b = new SolidBrush(fundo))
                g.FillPath(b, p);

            Rectangle texto = new Rectangle(a.Rect.X + 10, a.Rect.Y,
                a.Rect.Width - 36, a.Rect.Height);
            TextRenderer.DrawText(g, a.Titulo, fonteAba, texto,
                i == ativa ? Fg : Color.FromArgb(170, 170, 170),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            bool mostraX = abas.Count > 1 && (i == ativa || i == sobre);
            if (mostraX)
            {
                if (i == sobreFechar)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 90, 90)))
                        g.FillEllipse(b, a.Fechar);
                }
                TextRenderer.DrawText(g, G("\uE711", "\u2715"), fonteGlifo, a.Fechar,
                    i == ativa ? Fg : Color.FromArgb(190, 190, 190),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        if (sobreMais)
        {
            using (SolidBrush b = new SolidBrush(Hover))
                g.FillRectangle(b, rectMais);
        }
        TextRenderer.DrawText(g, G("\uE710", "+"), fonteGlifo, rectMais, Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    static GraphicsPath TopoArredondado(Rectangle r, int raio)
    {
        GraphicsPath p = new GraphicsPath();
        int d = raio * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        p.CloseFigure();
        return p;
    }

    int Achar(Point p)
    {
        for (int i = 0; i < abas.Count; i++)
        {
            if (abas[i].Rect.Contains(p)) return i;
        }
        return -1;
    }

    void FaixaMove(object s, MouseEventArgs e)
    {
        int i = Achar(e.Location);
        int f = (i >= 0 && abas[i].Fechar.Contains(e.Location)) ? i : -1;
        bool mais = rectMais.Contains(e.Location);
        if (i != sobre || f != sobreFechar || mais != sobreMais)
        {
            sobre = i; sobreFechar = f; sobreMais = mais;
            faixa.Invalidate();
        }
    }

    void FaixaClique(object s, MouseEventArgs e)
    {
        if (rectMais.Contains(e.Location) && e.Button == MouseButtons.Left)
        {
            NovaAba(null);
            return;
        }

        int i = Achar(e.Location);
        if (i < 0) return;

        if (e.Button == MouseButtons.Middle) { FechaAba(i); return; }
        if (e.Button != MouseButtons.Left) return;

        if (abas.Count > 1 && abas[i].Fechar.Contains(e.Location)) { FechaAba(i); return; }
        Ativa(i);
    }

    // ---------- abas ----------

    Aba Atual()
    {
        return (ativa >= 0 && ativa < abas.Count) ? abas[ativa] : null;
    }

    int Topo()
    {
        return (bar != null ? bar.Height : 0) + (faixa != null ? faixa.Height : 0);
    }

    Rectangle AreaConteudo()
    {
        int topo = Topo();
        int esq = (painelArvore != null) ? larguraArvore + LarguraDivisor : 0;
        return new Rectangle(esq, topo, Math.Max(0, ClientSize.Width - esq),
            Math.Max(0, ClientSize.Height - topo));
    }

    // ---------- arvore com raiz na pasta ----------

    void MontaArvore()
    {
        int topo = Topo();
        int alt = Math.Max(0, ClientSize.Height - topo);

        painelArvore = new Panel();
        painelArvore.BackColor = dark ? BgDark : SystemColors.Window;
        painelArvore.Bounds = new Rectangle(0, topo, larguraArvore, alt);
        Controls.Add(painelArvore);
        painelArvore.CreateControl();

        divisor = new Panel();
        divisor.BackColor = dark ? Color.FromArgb(56, 56, 56) : SystemColors.ControlDark;
        divisor.Cursor = Cursors.VSplit;
        divisor.Bounds = new Rectangle(larguraArvore, topo, LarguraDivisor, alt);
        divisor.MouseDown += delegate { arrastando = true; };
        divisor.MouseUp += delegate { arrastando = false; };
        divisor.MouseMove += delegate (object s, MouseEventArgs ev)
        {
            if (!arrastando) return;
            int x = divisor.Left + ev.X;
            larguraArvore = Math.Max(120, Math.Min(x, ClientSize.Width - 220));
            Reposiciona();
        };
        Controls.Add(divisor);

        try
        {
            Type t = Type.GetTypeFromCLSID(new Guid("AE054212-3535-4430-83ED-D501AA6680E6"));
            nstc = (INameSpaceTreeControl)Activator.CreateInstance(t);

            RECT rc = new RECT();
            rc.left = 0; rc.top = 0;
            rc.right = painelArvore.ClientSize.Width; rc.bottom = painelArvore.ClientSize.Height;
            nstc.Initialize(painelArvore.Handle, ref rc,
                NSTCS_HASEXPANDOS | NSTCS_ROOTHASEXPANDO | NSTCS_FULLROWSELECT |
                NSTCS_FADEINOUTEXPANDOS | NSTCS_SHOWSELECTIONALWAYS | NSTCS_TABSTOP |
                NSTCS_AUTOHSCROLL);
            Log("nstc initialize ok");

            IOleWindow ow = nstc as IOleWindow;
            if (ow != null) { try { ow.GetWindow(out hwndArvore); } catch { } }

            try { nstc.SetTheme("Explorer"); } catch { }
            if (dark && hwndArvore != IntPtr.Zero)
            {
                try { Native.AllowDarkModeForWindow(hwndArvore, true); } catch { }
                try { Native.SetWindowTheme(hwndArvore, "DarkMode_Explorer", null); } catch { }
            }
            AplicaCorArvore();

            IShellItem raiz = Native.ItemDe(startPath);
            uint conteudo = SHCONTF_FOLDERS | (arvoreArquivos ? SHCONTF_NONFOLDERS : 0);
            if (raiz != null) nstc.AppendRoot(raiz, conteudo, NSTCRS_EXPANDED, IntPtr.Zero);
            Log("nstc raiz ok");

            relogioArvore = new System.Windows.Forms.Timer();
            relogioArvore.Interval = 250;
            relogioArvore.Tick += VigiaArvore;
            relogioArvore.Start();
        }
        catch (Exception ex)
        {
            Log("arvore falhou: " + ex.Message);
            nstc = null;
        }
    }

    // A arvore fica da cor do Windows: destaque quando a pessoa mandou pintar as
    // superficies, senao o mesmo fundo da lista, para nao aparecer emenda.
    Color CorLateral()
    {
        Color fundo = dark ? Color.FromArgb(25, 25, 25) : SystemColors.Window;
        if (!Native.DestaqueNasSuperficies()) return fundo;

        Color d = Native.CorDeDestaque();
        if (d.IsEmpty) return fundo;

        double peso = dark ? 0.45 : 0.25;
        return Color.FromArgb(
            (int)(fundo.R + (d.R - fundo.R) * peso),
            (int)(fundo.G + (d.G - fundo.G) * peso),
            (int)(fundo.B + (d.B - fundo.B) * peso));
    }

    static int Ref(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

    IntPtr HwndLista()
    {
        if (hwndArvore == IntPtr.Zero) return IntPtr.Zero;
        IntPtr tv = Native.FindWindowEx(hwndArvore, IntPtr.Zero, "SysTreeView32", null);
        return tv != IntPtr.Zero ? tv : hwndArvore;
    }

    void AplicaCorArvore()
    {
        if (painelArvore == null) return;

        Color c = CorLateral();
        painelArvore.BackColor = c;
        if (divisor != null)
            divisor.BackColor = dark ? Color.FromArgb(56, 56, 56) : SystemColors.ControlDark;

        IntPtr tv = HwndLista();
        if (tv == IntPtr.Zero) return;

        double luz = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        Color texto = luz < 0.55 ? Color.FromArgb(240, 240, 240) : Color.FromArgb(20, 20, 20);
        try
        {
            Native.SendMessage(tv, TVM_SETBKCOLOR, IntPtr.Zero, (IntPtr)Ref(c));
            Native.SendMessage(tv, TVM_SETTEXTCOLOR, IntPtr.Zero, (IntPtr)Ref(texto));
            Native.InvalidateRect(tv, IntPtr.Zero, true);
        }
        catch { }
    }

    void Reposiciona()
    {
        if (painelArvore != null)
        {
            int topo = Topo();
            int alt = Math.Max(0, ClientSize.Height - topo);
            if (larguraArvore > ClientSize.Width - 220)
                larguraArvore = Math.Max(120, ClientSize.Width - 220);
            painelArvore.Bounds = new Rectangle(0, topo, larguraArvore, alt);
            divisor.Bounds = new Rectangle(larguraArvore, topo, LarguraDivisor, alt);
            if (hwndArvore != IntPtr.Zero)
                Native.MoveWindow(hwndArvore, 0, 0,
                    painelArvore.ClientSize.Width, painelArvore.ClientSize.Height, true);
        }

        Rectangle area = AreaConteudo();
        foreach (Aba a in abas)
            if (a.Host != null) a.Host.Bounds = area;
        AjustaBrowser(Atual());
    }

    // O controle so avisa por interface de 18 metodos; ler a selecao de tempos
    // em tempos custa quase nada e nao arrisca errar a tabela de metodos.
    void VigiaArvore(object s, EventArgs e)
    {
        if (nstc == null || sincronizando) return;

        bool ehPasta;
        string sel = SelecaoArvore(out ehPasta);
        if (string.IsNullOrEmpty(sel) || sel == ultimaSelecao) return;
        ultimaSelecao = sel;

        Aba a = Atual();
        if (a == null) return;

        // Arquivo na arvore nao e destino de navegacao: a lista vai para a pasta dele.
        string destino = ehPasta ? sel : PastaDe(sel);
        if (string.IsNullOrEmpty(destino)) return;
        if (string.Equals(destino, a.Caminho, StringComparison.OrdinalIgnoreCase)) return;
        Navega(a, destino);
    }

    string PastaDe(string caminho)
    {
        IShellItem si = Native.ItemDe(caminho);
        if (si == null) return null;
        try
        {
            IShellItem pai;
            si.GetParent(out pai);
            if (pai == null) return null;
            string nome;
            pai.GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, out nome);
            return nome;
        }
        catch { return null; }
    }

    string SelecaoArvore(out bool ehPasta)
    {
        ehPasta = true;
        IShellItemArray arr = null;
        IShellItem it = null;
        try
        {
            nstc.GetSelectedItems(out arr);
            if (arr == null) return null;
            uint n;
            arr.GetCount(out n);
            if (n == 0) return null;
            arr.GetItemAt(0, out it);
            string nome;
            it.GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, out nome);
            try
            {
                uint atrib;
                it.GetAttributes(SFGAO_FOLDER, out atrib);
                ehPasta = (atrib & SFGAO_FOLDER) != 0;
            }
            catch { }
            return nome;
        }
        catch { ehPasta = true; return null; }
        finally
        {
            if (it != null) { try { Marshal.ReleaseComObject(it); } catch { } }
            if (arr != null) { try { Marshal.ReleaseComObject(arr); } catch { } }
        }
    }

    // Lista navegou: a arvore acompanha. Fora da raiz nada acontece e a selecao
    // antiga continua valendo, entao os dois lados nao ficam brigando.
    void SincronizaArvore(string caminho)
    {
        if (nstc == null || string.IsNullOrEmpty(caminho)) return;
        if (string.Equals(caminho, ultimaSelecao, StringComparison.OrdinalIgnoreCase)) return;

        sincronizando = true;
        try
        {
            IShellItem si = Native.ItemDe(caminho);
            if (si == null) return;

            // EnsureItemVisible abre os pais ate o item; o estado marca e abre o proprio.
            try { nstc.EnsureItemVisible(si); } catch { }
            try
            {
                nstc.SetItemState(si, NSTCIS_SELECTED | NSTCIS_EXPANDED,
                                      NSTCIS_SELECTED | NSTCIS_EXPANDED);
                ultimaSelecao = caminho;
            }
            catch { }
            try { nstc.EnsureItemVisible(si); } catch { }
        }
        catch { }
        finally { sincronizando = false; }
    }

    public static bool Depurar;
    public static void Log(string s)
    {
        if (!Depurar) return;
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "folderpin.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + Environment.NewLine);
        }
        catch { }
    }

    void NovaAba(string caminho)
    {
        Log("NovaAba inicio: " + caminho);
        if (string.IsNullOrEmpty(caminho))
        {
            Aba atual = Atual();
            caminho = atual != null && !string.IsNullOrEmpty(atual.Caminho) ? atual.Caminho : startPath;
        }

        Aba a = new Aba();
        a.Host = new Panel();
        a.Host.Bounds = AreaConteudo();
        a.Host.BackColor = Bg;
        a.Host.Visible = true;
        Controls.Add(a.Host);
        a.Host.CreateControl();
        Log("painel criado hwnd=" + a.Host.Handle + " " + a.Host.Bounds);

        Type t = Type.GetTypeFromCLSID(new Guid("71F96385-DDD6-48D3-A0C1-AE06E8B055FB"));
        a.Browser = (IExplorerBrowser)Activator.CreateInstance(t);
        Log("browser criado");
        a.Browser.SetOptions(EBO_NOBORDER | (tree ? EBO_SHOWFRAMES : 0));
        Log("SetOptions ok");

        RECT rc = new RECT();
        rc.left = 0; rc.top = 0;
        rc.right = a.Host.ClientSize.Width; rc.bottom = a.Host.ClientSize.Height;
        FOLDERSETTINGS fs = new FOLDERSETTINGS();
        fs.ViewMode = FVM_DETAILS;
        fs.fFlags = 0;
        a.Browser.Initialize(a.Host.Handle, ref rc, ref fs);
        Log("Initialize ok");

        Aba capturada = a;
        a.Eventos = new BrowserEvents(delegate (IntPtr pidl) { Navegou(capturada, pidl); });
        a.Browser.Advise(a.Eventos, out a.Cookie);
        Log("Advise ok cookie=" + a.Cookie);

        abas.Add(a);
        Ativa(abas.Count - 1);
        Log("Ativa ok");
        Navega(a, caminho);
        Log("Navega ok");
        if (faixa != null) faixa.Invalidate();
    }

    void Ativa(int i)
    {
        if (i < 0 || i >= abas.Count) return;
        ativa = i;
        for (int k = 0; k < abas.Count; k++) abas[k].Host.Visible = (k == i);

        Aba a = abas[i];
        a.Host.Bounds = AreaConteudo();
        AjustaBrowser(a);
        a.Host.BringToFront();

        if (!string.IsNullOrEmpty(a.Titulo)) Text = a.Titulo;
        if (txtPath != null && !txtPath.Focused) txtPath.Text = a.Caminho;
        if (faixa != null) faixa.Invalidate();
        BeginInvoke((MethodInvoker)FocaLista);
    }

    void FechaAba(int i)
    {
        if (i < 0 || i >= abas.Count) return;
        if (abas.Count == 1) { Close(); return; }

        Aba a = abas[i];
        try { a.Browser.Unadvise(a.Cookie); } catch { }
        try { a.Browser.Destroy(); } catch { }
        try { Marshal.FinalReleaseComObject(a.Browser); } catch { }
        Controls.Remove(a.Host);
        a.Host.Dispose();
        abas.RemoveAt(i);

        Ativa(Math.Min(i, abas.Count - 1));
    }

    void AjustaBrowser(Aba a)
    {
        if (a == null || a.Browser == null) return;
        RECT rc = new RECT();
        rc.left = 0; rc.top = 0;
        rc.right = a.Host.ClientSize.Width; rc.bottom = a.Host.ClientSize.Height;
        try { a.Browser.SetRect(IntPtr.Zero, rc); } catch { }
    }

    void Navega(Aba a, string caminho)
    {
        if (a == null || a.Browser == null || string.IsNullOrEmpty(caminho)) return;
        IntPtr pidl = IntPtr.Zero;
        try
        {
            uint attrs;
            Native.SHParseDisplayName(caminho, IntPtr.Zero, out pidl, 0, out attrs);
            a.Browser.BrowseToIDList(pidl, 0);
        }
        catch
        {
            MessageBox.Show(this, "Caminho invalido:" + Environment.NewLine + caminho, "FolderPin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (pidl != IntPtr.Zero) Native.ILFree(pidl);
        }
    }

    void Navegou(Aba a, IntPtr pidl)
    {
        try
        {
            string nome;
            Native.SHGetNameFromIDList(pidl, SIGDN_NORMALDISPLAY, out nome);
            if (!string.IsNullOrEmpty(nome)) a.Titulo = nome;
        }
        catch { }

        try
        {
            string full;
            Native.SHGetNameFromIDList(pidl, SIGDN_DESKTOPABSOLUTEPARSING, out full);

            // Local do shell devolve "::{CLSID}" no nome de analise; mostra o nome legivel.
            if (!string.IsNullOrEmpty(full) && full.StartsWith("::"))
            {
                try
                {
                    string amigavel;
                    Native.SHGetNameFromIDList(pidl, SIGDN_DESKTOPABSOLUTEEDITING, out amigavel);
                    if (!string.IsNullOrEmpty(amigavel)) full = amigavel;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(full)) a.Caminho = full;
        }
        catch { }

        if (Atual() == a)
        {
            if (!string.IsNullOrEmpty(a.Titulo)) Text = a.Titulo;
            if (txtPath != null && !txtPath.Focused) txtPath.Text = a.Caminho;
            if (nstc != null) SincronizaArvore(a.Caminho);
        }
        if (faixa != null) faixa.Invalidate();
    }

    // ---------- ciclo de vida ----------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        Log("OnHandleCreated entrou");
        if (dark)
        {
            Native.DarkTitleBar(Handle, true);
            try { Native.AllowDarkModeForWindow(Handle, true); } catch { }
            try { Native.SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
        }
        Log("tema aplicado");

        if (arvoreRaiz) MontaArvore();
        Log("arvore ok");

        NovaAba(startPath);
    }

    protected override void WndProc(ref Message m)
    {
        if (nstc != null && (m.Msg == WM_DWMCOLORIZATIONCOLORCHANGED || m.Msg == WM_THEMECHANGED))
        {
            try { BeginInvoke((MethodInvoker)AplicaCorArvore); } catch { }
        }
        base.WndProc(ref m);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (abas.Count == 0 && painelArvore == null) return;
        Reposiciona();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(this);
        if (relogioArvore != null) { relogioArvore.Stop(); relogioArvore.Dispose(); relogioArvore = null; }
        if (nstc != null)
        {
            try { nstc.RemoveAllRoots(); } catch { }
            try { Marshal.FinalReleaseComObject(nstc); } catch { }
            nstc = null;
        }
        foreach (Aba a in abas)
        {
            try { a.Browser.Unadvise(a.Cookie); } catch { }
            try { a.Browser.Destroy(); } catch { }
            try { Marshal.FinalReleaseComObject(a.Browser); } catch { }
        }
        abas.Clear();
        base.OnFormClosed(e);
    }

    // ---------- comandos ----------

    IShellView ViewAtual()
    {
        Aba a = Atual();
        if (a == null || a.Browser == null) return null;
        try
        {
            Guid iid = new Guid("000214E3-0000-0000-C000-000000000046");
            object v = a.Browser.GetCurrentView(ref iid);
            return v as IShellView;
        }
        catch { return null; }
    }

    void FocaLista()
    {
        IShellView v = ViewAtual();
        if (v == null) return;
        try { v.UIActivate(1); } catch { }
    }

    void AtualizaLista()
    {
        IShellView v = ViewAtual();
        if (v == null) return;
        try { v.Refresh(); } catch { }
    }

    void Go(uint flag)
    {
        Aba a = Atual();
        if (a == null || a.Browser == null) return;
        try { a.Browser.BrowseToIDList(IntPtr.Zero, flag); } catch { }
    }

    void CicloAba(int passo)
    {
        if (abas.Count < 2) return;
        int i = (ativa + passo + abas.Count) % abas.Count;
        Ativa(i);
    }

    public bool PreFilterMessage(ref Message m)
    {
        const int WM_KEYDOWN = 0x100;
        const int WM_SYSKEYDOWN = 0x104;
        const int WM_XBUTTONDOWN = 0x20B;

        if (m.Msg == WM_SYSKEYDOWN)
        {
            Keys k = (Keys)m.WParam.ToInt32();
            if (k == Keys.Left) { Go(SBSP_NAVIGATEBACK); return true; }
            if (k == Keys.Right) { Go(SBSP_NAVIGATEFORWARD); return true; }
            if (k == Keys.Up) { Go(SBSP_PARENT); return true; }
        }
        else if (m.Msg == WM_KEYDOWN)
        {
            Keys k = (Keys)m.WParam.ToInt32();
            bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (k == Keys.BrowserBack) { Go(SBSP_NAVIGATEBACK); return true; }
            if (k == Keys.F5) { AtualizaLista(); return true; }
            if (ctrl && k == Keys.L && txtPath != null) { txtPath.Focus(); txtPath.SelectAll(); return true; }

            if (comAbas)
            {
                if (ctrl && k == Keys.T) { NovaAba(null); return true; }
                if (ctrl && k == Keys.W) { FechaAba(ativa); return true; }
                if (ctrl && k == Keys.Tab) { CicloAba(shift ? -1 : 1); return true; }
                if (ctrl && k >= Keys.D1 && k <= Keys.D9)
                {
                    int i = k - Keys.D1;
                    if (i < abas.Count) Ativa(i);
                    return true;
                }
            }
        }
        else if (m.Msg == WM_XBUTTONDOWN)
        {
            int btn = (m.WParam.ToInt32() >> 16) & 0xFFFF;
            if (btn == 1) { Go(SBSP_NAVIGATEBACK); return true; }
            if (btn == 2) { Go(SBSP_NAVIGATEFORWARD); return true; }
        }
        return false;
    }
}

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        FolderWindow.Log("Main entrou, args=" + args.Length);
        string path = null;
        string icon = null;
        string aumid = null;
        bool tree = true;
        bool treeRoot = false;
        bool treeFiles = false;
        bool tabs = true;
        bool maximized = false;
        bool simples = false;
        bool? darkOverride = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--icon" && i + 1 < args.Length) icon = args[++i];
            else if (args[i] == "--aumid" && i + 1 < args.Length) aumid = args[++i];
            else if (args[i] == "--tree") tree = true;
            else if (args[i] == "--no-tree") tree = false;
            else if (args[i] == "--tree-root") { tree = true; treeRoot = true; }
            else if (args[i] == "--tree-files") { tree = true; treeRoot = true; treeFiles = true; }
            else if (args[i] == "--tabs") tabs = true;
            else if (args[i] == "--no-tabs") tabs = false;
            else if (args[i] == "--dark") darkOverride = true;
            else if (args[i] == "--light") darkOverride = false;
            else if (args[i] == "--max") maximized = true;
            else if (args[i] == "--simples") simples = true;
            else if (args[i] == "--debug") FolderWindow.Depurar = true;
            else if (path == null) path = args[i];
        }

        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show(
                "Uso: FolderPin.exe \"C:\\pasta\" [--icon arquivo.ico] [--aumid ID] [--no-tree|--tree-root [--tree-files]] [--no-tabs] [--simples] [--dark|--light] [--max]",
                "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        // Nem todo destino e pasta de disco: "shell:MyComputerFolder" e "::{CLSID}"
        // sao locais do shell (Este Computador, Rede, Lixeira) e nao passam por Directory.Exists.
        if (!System.IO.Directory.Exists(path))
        {
            IntPtr teste = IntPtr.Zero;
            try
            {
                uint attrs;
                Native.SHParseDisplayName(path, IntPtr.Zero, out teste, 0, out attrs);
            }
            catch
            {
                MessageBox.Show("Local nao encontrado:" + Environment.NewLine + path, "FolderPin",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }
            finally
            {
                if (teste != IntPtr.Zero) Native.ILFree(teste);
            }
        }

        bool dark = darkOverride.HasValue ? darkOverride.Value : Native.SystemPrefersDark();
        FolderWindow.Log("dark=" + dark);

        try
        {
            Native.SetPreferredAppMode(dark ? Native.APPMODE_FORCE_DARK : Native.APPMODE_FORCE_LIGHT);
            FolderWindow.Log("SetPreferredAppMode ok");
            Native.FlushMenuThemes();
            FolderWindow.Log("FlushMenuThemes ok");
        }
        catch (Exception ex) { FolderWindow.Log("appmode falhou: " + ex.Message); }

        if (!string.IsNullOrEmpty(aumid))
        {
            try { Native.SetCurrentProcessExplicitAppUserModelID(aumid); } catch { }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        FolderWindow.Log("antes de construir a janela");
        FolderWindow janela = new FolderWindow(path, icon, tree, dark, maximized, tabs, simples, treeRoot, treeFiles);
        FolderWindow.Log("janela construida, Run");
        Application.Run(janela);
        return 0;
    }
}


