using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

static class Sys
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    public static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    public static extern void FlushMenuThemes();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int PrivateExtractIcons(string file, int index, int cx, int cy,
        IntPtr[] icons, int[] ids, int count, int flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindCtx,
        out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll")]
    public static extern void ILFree(IntPtr pidl);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll")]
    public static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attrs,
        ref SHFILEINFO psfi, uint cb, uint flags);

    [DllImport("shell32.dll", PreserveSig = false)]
    public static extern void SHGetImageList(int lista, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IImageList ppv);

    public const uint SHGFI_PIDL = 0x8;
    public const uint SHGFI_SYSICONINDEX = 0x4000;
    public const uint SHGFI_ICON = 0x100;
    public const uint SHGFI_LARGEICON = 0x0;
    public const int SHIL_EXTRALARGE = 2;
    public const int SHIL_JUMBO = 4;

    public static bool LocalValido(string caminho)
    {
        if (string.IsNullOrEmpty(caminho)) return false;
        if (System.IO.Directory.Exists(caminho)) return true;

        IntPtr pidl = IntPtr.Zero;
        try
        {
            uint attrs;
            SHParseDisplayName(caminho, IntPtr.Zero, out pidl, 0, out attrs);
            return pidl != IntPtr.Zero;
        }
        catch { return false; }
        finally { if (pidl != IntPtr.Zero) ILFree(pidl); }
    }

    public static bool PrefersDark()
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

    public static void DarkTitleBar(IntPtr hwnd)
    {
        int on = 1;
        try { DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); } catch { }
    }
}

[ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IImageList
{
    [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
    [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
    [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
    [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
    [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
    [PreserveSig] int Draw(IntPtr pimldp);
    [PreserveSig] int Remove(int i);
    [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
}

class LocalEspecial
{
    public string Nome;
    public string Caminho;
    public LocalEspecial(string n, string c) { Nome = n; Caminho = c; }
    public override string ToString() { return Nome + "   -   " + Caminho; }

    public static LocalEspecial[] Todos()
    {
        return new LocalEspecial[]
        {
            new LocalEspecial("Este Computador", "shell:MyComputerFolder"),
            new LocalEspecial("Rede", "shell:NetworkPlacesFolder"),
            new LocalEspecial("Inicio / Acesso rapido", "shell:::{679F85CB-0220-4080-B29B-5540CC05AAB6}"),
            new LocalEspecial("Lixeira", "shell:RecycleBinFolder"),
            new LocalEspecial("Pasta do usuario", "shell:UsersFilesFolder"),
            new LocalEspecial("Area de Trabalho", "shell:Desktop"),
            new LocalEspecial("Downloads", "shell:Downloads"),
            new LocalEspecial("Documentos", "shell:Personal"),
            new LocalEspecial("Imagens", "shell:My Pictures"),
            new LocalEspecial("Videos", "shell:My Video"),
            new LocalEspecial("Musicas", "shell:My Music"),
            new LocalEspecial("OneDrive", "shell:OneDrive"),
            new LocalEspecial("Painel de Controle", "shell:ControlPanelFolder"),
            new LocalEspecial("Impressoras", "shell:PrintersFolder"),
            new LocalEspecial("Conexoes de Rede", "shell:ConnectionsFolder"),
            new LocalEspecial("Programas instalados", "shell:AppsFolder"),
            new LocalEspecial("Fontes", "shell:Fonts"),
            new LocalEspecial("Inicializar", "shell:Startup")
        };
    }
}

class Pin
{
    public string Nome;
    public string Exe;
    public string Pasta;
    public string LnkMesa;
    public string LnkBarra;

    public bool Fixado { get { return !string.IsNullOrEmpty(LnkBarra); } }

    public override string ToString()
    {
        string onde = Fixado ? "fixado na barra" : "so na area de trabalho";
        return Nome + "   [" + onde + "]   " + Pasta;
    }
}

class Studio : Form
{
    static readonly Color Bg = Color.FromArgb(32, 32, 32);
    static readonly Color Field = Color.FromArgb(45, 45, 45);
    static readonly Color Fg = Color.FromArgb(235, 235, 235);
    static readonly Color Accent = Color.FromArgb(0, 120, 212);

    readonly bool dark = Sys.PrefersDark();
    readonly string instalacao = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderPin");

    TextBox txtPasta, txtNome, txtIcone;
    CheckBox chkArvore, chkArvoreRaiz, chkArvoreArquivos, chkAbas, chkMax, chkSimples;
    ComboBox cmbTema;
    PictureBox picIcone;
    ListBox lst;
    Label lblStatus;
    Button btnCriar, btnCancelar;
    Pin emEdicao;

    string BaseExe { get { return Path.Combine(instalacao, "FolderPin.exe"); } }
    string PastaIcones { get { return Path.Combine(instalacao, "icones"); } }
    string Mesa { get { return Environment.GetFolderPath(Environment.SpecialFolder.Desktop); } }
    string BarraFixados
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
        }
    }

    public Studio()
    {
        Text = "FolderPin - atalhos de pasta na barra de tarefas";
        ClientSize = new Size(760, 646);
        MinimumSize = new Size(700, 586);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = dark ? Bg : SystemColors.Control;
        ForeColor = dark ? Fg : SystemColors.ControlText;
        Font = new Font("Segoe UI", 9f);

        Monta();
        Extrai();
        Recarrega();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (dark) Sys.DarkTitleBar(Handle);
    }

    // ---------- interface ----------

    Label Rotulo(string txt, int x, int y)
    {
        Label l = new Label();
        l.Text = txt;
        l.Location = new Point(x, y);
        l.AutoSize = true;
        l.ForeColor = dark ? Fg : SystemColors.ControlText;
        return l;
    }

    TextBox Campo(int x, int y, int w)
    {
        TextBox t = new TextBox();
        t.Location = new Point(x, y);
        t.Width = w;
        t.BorderStyle = BorderStyle.FixedSingle;
        t.BackColor = dark ? Field : SystemColors.Window;
        t.ForeColor = dark ? Fg : SystemColors.WindowText;
        t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        return t;
    }

    Button Botao(string txt, int x, int y, int w, EventHandler h, bool destaque)
    {
        Button b = new Button();
        b.Text = txt;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 28);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = destaque ? 0 : 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        b.BackColor = destaque ? Accent : (dark ? Color.FromArgb(55, 55, 55) : SystemColors.Control);
        b.ForeColor = destaque ? Color.White : (dark ? Fg : SystemColors.ControlText);
        b.Click += h;
        return b;
    }

    void Monta()
    {
        int y = 14;

        Controls.Add(Rotulo("Pasta ou local que o atalho vai abrir:", 16, y));
        y += 20;
        txtPasta = Campo(16, y, 500);
        Controls.Add(txtPasta);
        Button bp = Botao("Procurar", 524, y - 2, 96, EscolhePasta, false);
        bp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(bp);
        Button bl = Botao("Locais...", 628, y - 2, 112, EscolheLocal, false);
        bl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(bl);
        y += 40;

        Controls.Add(Rotulo("Nome do atalho:", 16, y));
        y += 20;
        txtNome = Campo(16, y, 620);
        Controls.Add(txtNome);
        y += 40;

        Controls.Add(Rotulo("Icone (.ico, ou .exe/.dll para extrair) - opcional:", 16, y));
        y += 20;
        txtIcone = Campo(16, y, 560);
        txtIcone.Width = 560;
        Controls.Add(txtIcone);
        Button bi = Botao("Procurar", 584, y - 2, 96, EscolheIcone, false);
        bi.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(bi);

        picIcone = new PictureBox();
        picIcone.Location = new Point(690, y - 8);
        picIcone.Size = new Size(48, 48);
        picIcone.SizeMode = PictureBoxSizeMode.Zoom;
        picIcone.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(picIcone);
        y += 52;

        chkArvore = new CheckBox();
        chkArvore.Text = "Arvore de pastas";
        chkArvore.Location = new Point(16, y);
        chkArvore.AutoSize = true;
        chkArvore.Checked = true;
        chkArvore.ForeColor = dark ? Fg : SystemColors.ControlText;
        Controls.Add(chkArvore);

        chkAbas = new CheckBox();
        chkAbas.Text = "Abas";
        chkAbas.Location = new Point(160, y);
        chkAbas.AutoSize = true;
        chkAbas.Checked = true;
        chkAbas.ForeColor = dark ? Fg : SystemColors.ControlText;
        Controls.Add(chkAbas);

        chkMax = new CheckBox();
        chkMax.Text = "Abrir maximizado";
        chkMax.Location = new Point(250, y);
        chkMax.AutoSize = true;
        chkMax.ForeColor = dark ? Fg : SystemColors.ControlText;
        Controls.Add(chkMax);

        chkArvoreRaiz = new CheckBox();
        chkArvoreRaiz.Text = "Arvore so desta pasta";
        chkArvoreRaiz.Location = new Point(16, y + 26);
        chkArvoreRaiz.AutoSize = true;
        chkArvoreRaiz.ForeColor = dark ? Fg : SystemColors.ControlText;
        Controls.Add(chkArvoreRaiz);
        new ToolTip().SetToolTip(chkArvoreRaiz,
            "A arvore comeca na pasta escolhida e mostra so o que esta dentro dela.");

        chkSimples = new CheckBox();
        chkSimples.Text = "Janela simples (so a lista)";
        chkSimples.Location = new Point(190, y + 26);
        chkSimples.AutoSize = true;
        chkSimples.ForeColor = dark ? Fg : SystemColors.ControlText;
        Controls.Add(chkSimples);

        chkArvoreArquivos = new CheckBox();
        chkArvoreArquivos.Text = "Mostrar tambem os arquivos na arvore";
        chkArvoreArquivos.Location = new Point(16, y + 52);
        chkArvoreArquivos.AutoSize = true;
        chkArvoreArquivos.Checked = true;
        chkArvoreArquivos.ForeColor = dark ? Fg : SystemColors.ControlText;
        Controls.Add(chkArvoreArquivos);
        new ToolTip().SetToolTip(chkArvoreArquivos,
            "Alem das pastas, a arvore lista os arquivos soltos. Clicar num arquivo leva a lista ate a pasta dele.");

        EventHandler estadoArvore = delegate
        {
            bool s = chkSimples.Checked;
            chkArvore.Enabled = !s;
            chkAbas.Enabled = !s;
            chkArvoreRaiz.Enabled = !s && chkArvore.Checked;
            chkArvoreArquivos.Enabled = chkArvoreRaiz.Enabled && chkArvoreRaiz.Checked;
        };
        chkSimples.CheckedChanged += estadoArvore;
        chkArvore.CheckedChanged += estadoArvore;
        chkArvoreRaiz.CheckedChanged += estadoArvore;
        estadoArvore(null, EventArgs.Empty);

        Controls.Add(Rotulo("Tema:", 410, y + 2));
        cmbTema = new ComboBox();
        cmbTema.Location = new Point(456, y - 2);
        cmbTema.Width = 150;
        cmbTema.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTema.FlatStyle = FlatStyle.Flat;
        cmbTema.BackColor = dark ? Field : SystemColors.Window;
        cmbTema.ForeColor = dark ? Fg : SystemColors.WindowText;
        cmbTema.Items.AddRange(new object[] { "Seguir o Windows", "Sempre escuro", "Sempre claro" });
        cmbTema.SelectedIndex = 0;
        Controls.Add(cmbTema);
        y += 92;

        btnCriar = Botao("Criar atalho", 16, y, 160, Cria, true);
        Controls.Add(btnCriar);
        btnCancelar = Botao("Cancelar edicao", 186, y, 140, delegate { SaiEdicao(); }, false);
        btnCancelar.Visible = false;
        Controls.Add(btnCancelar);
        Controls.Add(Botao("Como fixar na barra", 336, y, 170, Ajuda, false));
        y += 46;

        Controls.Add(Rotulo("Atalhos ja criados (2 cliques para editar):", 16, y));
        y += 20;

        lst = new ListBox();
        lst.Location = new Point(16, y);
        lst.Size = new Size(724, 190);
        lst.BorderStyle = BorderStyle.FixedSingle;
        lst.BackColor = dark ? Field : SystemColors.Window;
        lst.ForeColor = dark ? Fg : SystemColors.WindowText;
        lst.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        lst.IntegralHeight = false;
        lst.DoubleClick += Editar;
        Controls.Add(lst);
        y += 200;

        Button b1 = Botao("Editar", 16, y, 100, Editar, false);
        Button b2 = Botao("Abrir a pasta do atalho", 126, y, 170, Localiza, false);
        Button b3 = Botao("Testar", 306, y, 100, Testa, false);
        Button b4 = Botao("Remover", 416, y, 100, Remove, false);
        foreach (Button b in new Button[] { b1, b2, b3, b4 })
        {
            b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(b);
        }
        y += 36;

        lblStatus = new Label();
        lblStatus.Location = new Point(16, y);
        lblStatus.Size = new Size(724, 34);
        lblStatus.ForeColor = Color.FromArgb(150, 150, 150);
        lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblStatus.Text = "Criar o atalho e o primeiro passo. Fixar na barra e manual: o Windows 11 bloqueia isso para programas.";
        Controls.Add(lblStatus);
    }

    // ---------- instalacao ----------

    void Extrai()
    {
        try
        {
            Directory.CreateDirectory(instalacao);
            Directory.CreateDirectory(PastaIcones);

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FolderPin.exe"))
            {
                if (s == null) return;
                byte[] buf = new byte[s.Length];
                int lidos = 0;
                while (lidos < buf.Length)
                {
                    int n = s.Read(buf, lidos, buf.Length - lidos);
                    if (n <= 0) break;
                    lidos += n;
                }

                bool precisa = true;
                if (File.Exists(BaseExe))
                {
                    try { precisa = new FileInfo(BaseExe).Length != buf.Length; }
                    catch { precisa = true; }
                }
                if (precisa) File.WriteAllBytes(BaseExe, buf);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Nao consegui preparar a instalacao:" + Environment.NewLine + ex.Message,
                "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---------- acoes ----------

    void EscolhePasta(object s, EventArgs e)
    {
        using (FolderBrowserDialog d = new FolderBrowserDialog())
        {
            d.Description = "Escolha a pasta que o atalho vai abrir";
            d.ShowNewFolderButton = false;
            if (!string.IsNullOrEmpty(txtPasta.Text) && Directory.Exists(txtPasta.Text))
                d.SelectedPath = txtPasta.Text;
            if (d.ShowDialog(this) != DialogResult.OK) return;

            txtPasta.Text = d.SelectedPath;
            if (string.IsNullOrEmpty(txtNome.Text))
                txtNome.Text = new DirectoryInfo(d.SelectedPath).Name;
        }
    }

    void EscolheLocal(object s, EventArgs e)
    {
        using (Form d = new Form())
        {
            d.Text = "Locais do Windows";
            d.ClientSize = new Size(520, 420);
            d.FormBorderStyle = FormBorderStyle.FixedDialog;
            d.MaximizeBox = false;
            d.MinimizeBox = false;
            d.StartPosition = FormStartPosition.CenterParent;
            d.BackColor = dark ? Bg : SystemColors.Control;
            d.Font = Font;

            Label ajuda = new Label();
            ajuda.Text = "Locais que nao tem caminho de disco. Tambem da para digitar " +
                         "shell:NomeDoLocal ou ::{CLSID} direto no campo.";
            ajuda.Location = new Point(12, 10);
            ajuda.Size = new Size(496, 34);
            ajuda.ForeColor = Color.FromArgb(160, 160, 160);
            d.Controls.Add(ajuda);

            ListBox lista = new ListBox();
            lista.Location = new Point(12, 50);
            lista.Size = new Size(496, 320);
            lista.BorderStyle = BorderStyle.FixedSingle;
            lista.BackColor = dark ? Field : SystemColors.Window;
            lista.ForeColor = dark ? Fg : SystemColors.WindowText;
            lista.IntegralHeight = false;
            lista.Items.AddRange(LocalEspecial.Todos());
            d.Controls.Add(lista);

            Button ok = Botao("Usar este", 300, 380, 100, delegate
            {
                d.DialogResult = DialogResult.OK;
                d.Close();
            }, true);
            d.Controls.Add(ok);
            d.Controls.Add(Botao("Cancelar", 408, 380, 100, delegate { d.Close(); }, false));

            lista.DoubleClick += delegate { d.DialogResult = DialogResult.OK; d.Close(); };
            d.AcceptButton = ok;

            if (d.ShowDialog(this) != DialogResult.OK) return;

            LocalEspecial esc = lista.SelectedItem as LocalEspecial;
            if (esc == null) return;

            txtPasta.Text = esc.Caminho;
            if (string.IsNullOrEmpty(txtNome.Text)) txtNome.Text = esc.Nome;
        }
    }

    void EscolheIcone(object s, EventArgs e)
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = "Escolha o icone";
            d.Filter = "Icones e programas|*.ico;*.exe;*.dll|Icone (*.ico)|*.ico|Todos|*.*";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            txtIcone.Text = d.FileName;
            MostraPreview(d.FileName);
        }
    }

    void MostraPreview(string arquivo)
    {
        // PrivateExtractIcons em vez de new Icon(): o Icon do .NET nao decodifica
        // quadro PNG dentro de .ico e devolve chuvisco.
        IntPtr[] h = new IntPtr[1];
        int[] ids = new int[1];
        try
        {
            int n = Sys.PrivateExtractIcons(arquivo, 0, 48, 48, h, ids, 1, 0);
            if (n <= 0 || h[0] == IntPtr.Zero) { picIcone.Image = null; return; }

            Bitmap bmp = new Bitmap(48, 48);
            using (Icon i = Icon.FromHandle(h[0]))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawIcon(i, new Rectangle(0, 0, 48, 48));
            }

            Image antiga = picIcone.Image;
            picIcone.Image = bmp;
            if (antiga != null) antiga.Dispose();
        }
        catch { picIcone.Image = null; }
        finally { if (h[0] != IntPtr.Zero) Sys.DestroyIcon(h[0]); }
    }

    // Aceita "arquivo.ico", "shell32.dll,15" e "imageres.dll,-109".
    static void SeparaSpec(string spec, out string arquivo, out int indice)
    {
        arquivo = spec;
        indice = 0;
        int v = spec.LastIndexOf(',');
        if (v <= 0) return;

        int n;
        string cauda = spec.Substring(v + 1).Trim();
        string cabeca = spec.Substring(0, v).Trim();
        if (int.TryParse(cauda, out n) && File.Exists(cabeca))
        {
            arquivo = cabeca;
            indice = n < 0 ? -n : n;
        }
    }

    string ExtraiIcone(string origem, string destino)
    {
        return ExtraiIcone(origem, 0, destino);
    }

    string ExtraiIcone(string origem, int indice, string destino)
    {
        IntPtr[] h = new IntPtr[1];
        int[] ids = new int[1];
        int n = Sys.PrivateExtractIcons(origem, indice, 256, 256, h, ids, 1, 0);
        if (n <= 0 || h[0] == IntPtr.Zero)
        {
            n = Sys.PrivateExtractIcons(origem, indice, 48, 48, h, ids, 1, 0);
            if (n <= 0 || h[0] == IntPtr.Zero) return null;
        }
        try
        {
            using (Icon i = Icon.FromHandle(h[0]))
            using (FileStream fs = new FileStream(destino, FileMode.Create, FileAccess.Write))
            {
                i.Save(fs);
            }
            return destino;
        }
        finally { Sys.DestroyIcon(h[0]); }
    }

    static string Limpa(string nome)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in nome)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) < 0) sb.Append(c);
        }
        string r = sb.ToString().Trim();
        return string.IsNullOrEmpty(r) ? "Pasta" : r;
    }

    // Locais do shell (Este Computador, Rede) nao tem arquivo de icone em disco:
    // pega o icone que o proprio Windows usa para aquele item.
    string IconeAutomatico(string nome, string caminho)
    {
        IntPtr pidl = IntPtr.Zero;
        try
        {
            uint attrs;
            Sys.SHParseDisplayName(caminho, IntPtr.Zero, out pidl, 0, out attrs);

            Sys.SHFILEINFO fi = new Sys.SHFILEINFO();
            IntPtr r = Sys.SHGetFileInfo(pidl, 0, ref fi, (uint)Marshal.SizeOf(fi),
                Sys.SHGFI_PIDL | Sys.SHGFI_SYSICONINDEX);
            if (r == IntPtr.Zero) return null;

            Guid iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
            foreach (int nivel in new int[] { Sys.SHIL_EXTRALARGE, Sys.SHIL_JUMBO })
            {
                IntPtr hIcon = IntPtr.Zero;
                try
                {
                    IImageList il;
                    Sys.SHGetImageList(nivel, ref iid, out il);
                    if (il.GetIcon(fi.iIcon, 1, out hIcon) != 0 || hIcon == IntPtr.Zero) continue;

                    using (Icon ic = Icon.FromHandle(hIcon))
                    using (Bitmap bmp = ic.ToBitmap())
                    {
                        string destino = Path.Combine(PastaIcones, nome + ".ico");
                        Directory.CreateDirectory(PastaIcones);
                        SalvaIco(bmp, destino);
                        return destino;
                    }
                }
                catch { }
                finally { if (hIcon != IntPtr.Zero) Sys.DestroyIcon(hIcon); }
            }
            return null;
        }
        catch { return null; }
        finally { if (pidl != IntPtr.Zero) Sys.ILFree(pidl); }
    }

    // .ico com um quadro PNG dentro (formato aceito desde o Vista).
    static void SalvaIco(Bitmap bmp, string destino)
    {
        byte[] png;
        using (MemoryStream ms = new MemoryStream())
        {
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            png = ms.ToArray();
        }

        using (FileStream fs = new FileStream(destino, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((short)0);                                   // reservado
            w.Write((short)1);                                   // tipo: icone
            w.Write((short)1);                                   // 1 imagem
            w.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));   // 0 significa 256
            w.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
            w.Write((byte)0);                                    // cores da paleta
            w.Write((byte)0);                                    // reservado
            w.Write((short)1);                                   // planos
            w.Write((short)32);                                  // bits por pixel
            w.Write(png.Length);
            w.Write(22);                                         // deslocamento dos dados
            w.Write(png);
        }
    }

    string ResolveIcone(string nome)
    {
        string spec = txtIcone.Text.Trim().Trim('"');
        if (string.IsNullOrEmpty(spec)) return null;

        string icone;
        int indice;
        SeparaSpec(spec, out icone, out indice);
        if (!File.Exists(icone)) return null;

        string destino = Path.Combine(PastaIcones, nome + ".ico");

        // Guarda copia propria: se o .ico original sair do lugar (pendrive, pasta
        // renomeada), o atalho continua com icone.
        if (indice == 0 && icone.ToLowerInvariant().EndsWith(".ico"))
        {
            if (string.Equals(icone, destino, StringComparison.OrdinalIgnoreCase)) return destino;
            try
            {
                Directory.CreateDirectory(PastaIcones);
                File.Copy(icone, destino, true);
                return destino;
            }
            catch { return icone; }
        }

        return ExtraiIcone(icone, indice, destino);
    }

    string MontaArgs(string pasta, string icoFinal)
    {
        StringBuilder args = new StringBuilder();
        args.Append('"').Append(pasta).Append('"');
        if (icoFinal != null) args.Append(" --icon \"").Append(icoFinal).Append('"');
        if (chkSimples.Checked) args.Append(" --simples");
        if (!chkArvore.Checked) args.Append(" --no-tree");
        else if (chkArvoreRaiz.Checked)
            args.Append(chkArvoreArquivos.Checked ? " --tree-files" : " --tree-root");
        if (!chkAbas.Checked) args.Append(" --no-tabs");
        if (chkMax.Checked) args.Append(" --max");
        if (cmbTema.SelectedIndex == 1) args.Append(" --dark");
        else if (cmbTema.SelectedIndex == 2) args.Append(" --light");
        return args.ToString();
    }

    void Cria(object s, EventArgs e)
    {
        if (emEdicao != null) { Salva(); return; }

        string pasta = txtPasta.Text.Trim().Trim('"');
        string nome = Limpa(txtNome.Text.Trim());

        if (!Sys.LocalValido(pasta))
        {
            MessageBox.Show(this, "Escolha uma pasta que exista, ou um local do Windows (botao Locais...).",
                "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrEmpty(txtNome.Text.Trim()))
        {
            MessageBox.Show(this, "De um nome ao atalho.", "FolderPin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!File.Exists(BaseExe))
        {
            MessageBox.Show(this, "O programa base nao foi instalado.", "FolderPin",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string exe = Path.Combine(instalacao, nome + ".exe");
        string lnk = Path.Combine(Mesa, nome + ".lnk");

        if (File.Exists(exe) || File.Exists(lnk))
        {
            if (MessageBox.Show(this, "Ja existe um atalho com esse nome. Substituir?", "FolderPin",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        }

        try
        {
            File.Copy(BaseExe, exe, true);
        }
        catch (IOException)
        {
            MessageBox.Show(this, "Esse atalho esta aberto agora. Feche a janela dele e tente de novo.",
                "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string icoFinal = ResolveIcone(nome);
        if (icoFinal == null) icoFinal = IconeAutomatico(nome, pasta);
        string args = MontaArgs(pasta, icoFinal);

        CriaAtalho(lnk, exe, args, instalacao,
            icoFinal != null ? icoFinal + ",0" : exe + ",0", Etiqueta + pasta);

        Recarrega();
        lblStatus.Text = "Criado: " + lnk;

        if (MessageBox.Show(this,
            "Atalho criado na area de trabalho." + Environment.NewLine + Environment.NewLine +
            "Para fixar na barra: botao direito no atalho > Mostrar mais opcoes > Fixar na barra de tarefas." +
            Environment.NewLine + Environment.NewLine + "Abrir a area de trabalho no atalho agora?",
            "FolderPin", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
        {
            Selecionar(lnk);
        }
    }

    static void CriaAtalho(string lnk, string alvo, string args, string dir, string icone, string desc)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        object atalho = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        Type ta = atalho.GetType();
        ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, atalho, new object[] { alvo });
        ta.InvokeMember("Arguments", BindingFlags.SetProperty, null, atalho, new object[] { args });
        ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, atalho, new object[] { dir });
        ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, atalho, new object[] { icone });
        ta.InvokeMember("Description", BindingFlags.SetProperty, null, atalho, new object[] { desc });
        ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, atalho, null);
    }

    public const string Etiqueta = "FolderPin: ";

    static void LeAtalho(string lnk, out string alvo, out string args, out string desc)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        object atalho = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        Type ta = atalho.GetType();
        alvo = (string)ta.InvokeMember("TargetPath", BindingFlags.GetProperty, null, atalho, null);
        args = (string)ta.InvokeMember("Arguments", BindingFlags.GetProperty, null, atalho, null);
        desc = (string)ta.InvokeMember("Description", BindingFlags.GetProperty, null, atalho, null);
    }

    static void Selecionar(string arquivo)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + arquivo + "\""); }
        catch { }
    }

    void Recarrega()
    {
        lst.Items.Clear();
        Dictionary<string, Pin> achados = new Dictionary<string, Pin>(StringComparer.OrdinalIgnoreCase);

        foreach (string dir in new string[] { Mesa, BarraFixados })
        {
            if (!Directory.Exists(dir)) continue;
            string[] arquivos;
            try { arquivos = Directory.GetFiles(dir, "*.lnk"); }
            catch { continue; }

            foreach (string lnk in arquivos)
            {
                string alvo, args, desc;
                try { LeAtalho(lnk, out alvo, out args, out desc); }
                catch { continue; }

                if (string.IsNullOrEmpty(alvo)) continue;

                bool naInstalacao = alvo.StartsWith(instalacao, StringComparison.OrdinalIgnoreCase);
                bool etiquetado = !string.IsNullOrEmpty(desc) &&
                    desc.StartsWith("FolderPin", StringComparison.OrdinalIgnoreCase);
                if (!naInstalacao && !etiquetado) continue;
                if (alvo.EndsWith("FolderPin.exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (alvo.EndsWith("FolderPin Studio.exe", StringComparison.OrdinalIgnoreCase)) continue;

                string chave = Path.GetFileNameWithoutExtension(alvo);
                Pin p;
                if (!achados.TryGetValue(chave, out p))
                {
                    p = new Pin();
                    p.Nome = chave;
                    p.Exe = alvo;
                    p.Pasta = PrimeiroArg(args);
                    achados[chave] = p;
                }
                if (dir == Mesa) p.LnkMesa = lnk; else p.LnkBarra = lnk;
            }
        }

        foreach (Pin p in achados.Values) lst.Items.Add(p);
        lblStatus.Text = achados.Count + " atalho(s) do FolderPin encontrado(s). Instalacao: " + instalacao;
    }

    static string PrimeiroArg(string args)
    {
        if (string.IsNullOrEmpty(args)) return "";
        args = args.Trim();
        if (args.StartsWith("\""))
        {
            int fim = args.IndexOf('"', 1);
            if (fim > 1) return args.Substring(1, fim - 1);
        }
        int esp = args.IndexOf(' ');
        return esp > 0 ? args.Substring(0, esp) : args;
    }

    Pin Selecionado()
    {
        Pin p = lst.SelectedItem as Pin;
        if (p == null)
        {
            MessageBox.Show(this, "Escolha um atalho na lista.", "FolderPin",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return p;
    }

    void Localiza(object s, EventArgs e)
    {
        Pin p = Selecionado();
        if (p == null) return;
        Selecionar(p.LnkMesa != null ? p.LnkMesa : p.Exe);
    }

    void Testa(object s, EventArgs e)
    {
        Pin p = Selecionado();
        if (p == null) return;
        try
        {
            string lnk = p.LnkMesa != null ? p.LnkMesa : p.LnkBarra;
            System.Diagnostics.Process.Start(lnk != null ? lnk : p.Exe);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void Remove(object s, EventArgs e)
    {
        Pin p = Selecionado();
        if (p == null) return;

        string aviso = "Remover o atalho \"" + p.Nome + "\"?";
        if (p.Fixado)
            aviso += Environment.NewLine + Environment.NewLine +
                "Ele esta fixado na barra. Desafixe primeiro (botao direito no icone > Desafixar), senao sobra um icone morto.";

        if (MessageBox.Show(this, aviso, "FolderPin", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            if (p.LnkMesa != null && File.Exists(p.LnkMesa)) File.Delete(p.LnkMesa);
            if (File.Exists(p.Exe)) File.Delete(p.Exe);
            string ico = Path.Combine(PastaIcones, p.Nome + ".ico");
            if (File.Exists(ico)) File.Delete(ico);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Nao deu para remover tudo: " + ex.Message + Environment.NewLine +
                "Se a janela dele estiver aberta, feche e tente de novo.",
                "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Recarrega();
    }

    static string ArgIcone(string args)
    {
        if (string.IsNullOrEmpty(args)) return "";
        int i = args.IndexOf("--icon", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        int aspa = args.IndexOf('"', i);
        if (aspa < 0) return "";
        int fim = args.IndexOf('"', aspa + 1);
        if (fim < 0) return "";
        return args.Substring(aspa + 1, fim - aspa - 1);
    }

    void CarregaOpcoes(string args)
    {
        if (args == null) args = "";
        chkSimples.Checked = args.IndexOf("--simples", StringComparison.OrdinalIgnoreCase) >= 0;
        chkArvore.Checked = args.IndexOf("--no-tree", StringComparison.OrdinalIgnoreCase) < 0;
        chkArvoreArquivos.Checked = args.IndexOf("--tree-files", StringComparison.OrdinalIgnoreCase) >= 0;
        chkArvoreRaiz.Checked = chkArvoreArquivos.Checked ||
            args.IndexOf("--tree-root", StringComparison.OrdinalIgnoreCase) >= 0;
        chkArvoreRaiz.Enabled = chkArvore.Checked && !chkSimples.Checked;
        chkArvoreArquivos.Enabled = chkArvoreRaiz.Enabled && chkArvoreRaiz.Checked;
        chkAbas.Checked = args.IndexOf("--no-tabs", StringComparison.OrdinalIgnoreCase) < 0;
        chkMax.Checked = args.IndexOf("--max", StringComparison.OrdinalIgnoreCase) >= 0;

        if (args.IndexOf("--dark", StringComparison.OrdinalIgnoreCase) >= 0) cmbTema.SelectedIndex = 1;
        else if (args.IndexOf("--light", StringComparison.OrdinalIgnoreCase) >= 0) cmbTema.SelectedIndex = 2;
        else cmbTema.SelectedIndex = 0;

        string ico = ArgIcone(args);
        txtIcone.Text = ico;
        picIcone.Image = null;
        if (!string.IsNullOrEmpty(ico) && File.Exists(ico)) MostraPreview(ico);
    }

    void Editar(object s, EventArgs e)
    {
        Pin p = Selecionado();
        if (p == null) return;

        string lnk = p.LnkMesa != null ? p.LnkMesa : p.LnkBarra;
        if (lnk == null) return;

        string alvo, args, desc;
        try { LeAtalho(lnk, out alvo, out args, out desc); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Nao consegui ler o atalho: " + ex.Message, "FolderPin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        txtPasta.Text = p.Pasta;
        txtNome.Text = p.Nome;
        CarregaOpcoes(args);

        emEdicao = p;
        txtNome.ReadOnly = true;
        txtNome.BackColor = dark ? Color.FromArgb(38, 38, 38) : SystemColors.Control;
        btnCriar.Text = "Salvar alteracoes";
        btnCancelar.Visible = true;
        lblStatus.Text = "Editando \"" + p.Nome + "\". O nome nao muda aqui: e o programa dele que da o botao proprio na barra.";
    }

    void SaiEdicao()
    {
        emEdicao = null;
        txtNome.ReadOnly = false;
        txtNome.BackColor = dark ? Field : SystemColors.Window;
        btnCriar.Text = "Criar atalho";
        btnCancelar.Visible = false;
        txtPasta.Text = "";
        txtNome.Text = "";
        txtIcone.Text = "";
        picIcone.Image = null;
        chkArvore.Checked = true;
        chkArvoreRaiz.Checked = false;
        chkArvoreRaiz.Enabled = true;
        chkArvoreArquivos.Checked = true;
        chkArvoreArquivos.Enabled = false;
        chkAbas.Checked = true;
        chkMax.Checked = false;
        chkSimples.Checked = false;
        cmbTema.SelectedIndex = 0;
        Recarrega();
    }

    void Salva()
    {
        Pin p = emEdicao;
        if (p == null) return;

        string pasta = txtPasta.Text.Trim().Trim('"');
        if (!Sys.LocalValido(pasta))
        {
            MessageBox.Show(this, "Escolha uma pasta que exista, ou um local do Windows (botao Locais...).",
                "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string icoFinal = ResolveIcone(p.Nome);
        if (icoFinal == null) icoFinal = IconeAutomatico(p.Nome, pasta);
        string args = MontaArgs(pasta, icoFinal);
        string icone = icoFinal != null ? icoFinal + ",0" : p.Exe + ",0";

        int atualizados = 0;
        foreach (string lnk in new string[] { p.LnkMesa, p.LnkBarra })
        {
            if (lnk == null) continue;
            try
            {
                CriaAtalho(lnk, p.Exe, args, instalacao, icone, Etiqueta + pasta);
                atualizados++;
            }
            catch { }
        }

        bool programaPreso = false;
        try { File.Copy(BaseExe, p.Exe, true); }
        catch { programaPreso = true; }

        string aviso = "Atalho \"" + p.Nome + "\" atualizado (" + atualizados + " atalho(s)).";
        if (p.Fixado)
            aviso += Environment.NewLine + Environment.NewLine +
                "Se a janela dele estiver aberta, feche e abra de novo para ver a mudanca." +
                Environment.NewLine +
                "Trocou o icone? O icone fixado so troca depois de reiniciar o Explorador de Arquivos.";
        if (programaPreso)
            aviso += Environment.NewLine + Environment.NewLine +
                "O programa dele estava aberto, entao nao consegui atualizar a versao dele - as opcoes foram salvas do mesmo jeito.";

        SaiEdicao();
        MessageBox.Show(this, aviso, "FolderPin", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void Ajuda(object s, EventArgs e)
    {
        MessageBox.Show(this,
            "1) Crie o atalho aqui - ele nasce na area de trabalho." + Environment.NewLine +
            "2) Botao direito no atalho." + Environment.NewLine +
            "3) Mostrar mais opcoes (ou Shift+F10)." + Environment.NewLine +
            "4) Fixar na barra de tarefas." + Environment.NewLine + Environment.NewLine +
            "Por que nao fixa sozinho: desde o Windows 10 1903 a Microsoft bloqueou o comando de fixar para programas. " +
            "Nenhum aplicativo consegue fazer isso por voce - so o clique manual." + Environment.NewLine + Environment.NewLine +
            "Cada atalho ganha um programa proprio dentro da pasta de instalacao. E isso que da a ele um botao " +
            "separado na barra, em vez de cair todo mundo no botao do Explorador de Arquivos.",
            "Como fixar na barra de tarefas", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    [STAThread]
    static void Main()
    {
        try
        {
            Sys.SetPreferredAppMode(Sys.PrefersDark() ? 2 : 3);
            Sys.FlushMenuThemes();
        }
        catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Studio());
    }
}


