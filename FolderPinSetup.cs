using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

static class Amb
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    public static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    public static extern void FlushMenuThemes();

    public const string Produto = "FolderPin";
    public const string Versao = "1.0.0";
    public const string ChaveDesinstalar =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FolderPin";

    public static bool PrefereEscuro()
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

    public static void BarraEscura(IntPtr hwnd)
    {
        int on = 1;
        try { DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); } catch { }
    }

    public static string Windows()
    {
        try
        {
            RegistryKey k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (k == null) return Environment.OSVersion.VersionString;

            string nome = (string)k.GetValue("ProductName", "Windows");
            string build = (string)k.GetValue("CurrentBuild", "");
            string versao = (string)k.GetValue("DisplayVersion", "");

            int b;
            // O registro segue dizendo "Windows 10" no 11; a build e que separa.
            if (int.TryParse(build, out b) && b >= 22000 && nome.Contains("Windows 10"))
                nome = nome.Replace("Windows 10", "Windows 11");

            string txt = nome;
            if (!string.IsNullOrEmpty(versao)) txt += " " + versao;
            txt += " (build " + build + ")";
            return txt;
        }
        catch { return Environment.OSVersion.VersionString; }
    }

    public static int BuildWindows()
    {
        try
        {
            RegistryKey k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            int b;
            if (k != null && int.TryParse((string)k.GetValue("CurrentBuild", "0"), out b)) return b;
        }
        catch { }
        return 0;
    }

    public static string DotNet(out int release)
    {
        release = 0;
        try
        {
            RegistryKey k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            if (k == null) return "4.0 ou anterior";

            object r = k.GetValue("Release");
            if (r == null) return "4.0";
            release = Convert.ToInt32(r);

            if (release >= 533320) return "4.8.1";
            if (release >= 528040) return "4.8";
            if (release >= 461808) return "4.7.2";
            if (release >= 460798) return "4.7";
            if (release >= 394802) return "4.6.2";
            if (release >= 393295) return "4.6.1";
            if (release >= 379893) return "4.5.2";
            return "4.5";
        }
        catch { return "desconhecido"; }
    }

    public static string FonteGlifos()
    {
        foreach (FontFamily f in FontFamily.Families)
        {
            if (f.Name == "Segoe Fluent Icons") return "Segoe Fluent Icons";
        }
        foreach (FontFamily f in FontFamily.Families)
        {
            if (f.Name == "Segoe MDL2 Assets") return "Segoe MDL2 Assets";
        }
        return null;
    }
}

class Instalador : Form
{
    static readonly Color Bg = Color.FromArgb(32, 32, 32);
    static readonly Color Campo = Color.FromArgb(45, 45, 45);
    static readonly Color Fg = Color.FromArgb(235, 235, 235);
    static readonly Color Accent = Color.FromArgb(0, 120, 212);
    static readonly Color Bom = Color.FromArgb(106, 200, 120);
    static readonly Color Ruim = Color.FromArgb(235, 130, 110);
    static readonly Color Aviso = Color.FromArgb(230, 190, 110);

    readonly bool escuro = Amb.PrefereEscuro();
    readonly bool jaInstalado;
    readonly string instalacaoAtual;

    TextBox txtLocal;
    CheckBox chkMesa, chkIniciar;
    Panel painelChecagem;
    Label lblStatus;
    Button btnInstalar, btnDesinstalar;

    static string LocalPadrao
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderPin");
        }
    }

    static string Mesa { get { return Environment.GetFolderPath(Environment.SpecialFolder.Desktop); } }

    static string MenuIniciar
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
        }
    }

    public Instalador()
    {
        RegistryKey k = Registry.CurrentUser.OpenSubKey(Amb.ChaveDesinstalar);
        if (k != null)
        {
            instalacaoAtual = (string)k.GetValue("InstallLocation", null);
            jaInstalado = !string.IsNullOrEmpty(instalacaoAtual) && Directory.Exists(instalacaoAtual);
        }

        Text = "Instalar " + Amb.Produto + " " + Amb.Versao;
        ClientSize = new Size(660, 520);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = escuro ? Bg : SystemColors.Control;
        ForeColor = escuro ? Fg : SystemColors.ControlText;
        Font = new Font("Segoe UI", 9f);

        Monta();
        Checar();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (escuro) Amb.BarraEscura(Handle);
    }

    // ---------- interface ----------

    Label Rotulo(string t, int x, int y, bool forte)
    {
        Label l = new Label();
        l.Text = t;
        l.Location = new Point(x, y);
        l.AutoSize = true;
        l.ForeColor = escuro ? Fg : SystemColors.ControlText;
        if (forte) l.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        return l;
    }

    Button Botao(string t, int x, int y, int w, EventHandler h, bool destaque)
    {
        Button b = new Button();
        b.Text = t;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 32);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = destaque ? 0 : 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        b.BackColor = destaque ? Accent : (escuro ? Color.FromArgb(55, 55, 55) : SystemColors.Control);
        b.ForeColor = destaque ? Color.White : (escuro ? Fg : SystemColors.ControlText);
        b.Click += h;
        return b;
    }

    void Monta()
    {
        int y = 16;

        Controls.Add(Rotulo("FolderPin " + Amb.Versao, 20, y, true));
        y += 26;
        Label sub = Rotulo("Abre pastas em janela propria, com botao proprio na barra de tarefas.", 20, y, false);
        sub.ForeColor = Color.FromArgb(160, 160, 160);
        Controls.Add(sub);
        y += 30;

        Controls.Add(Rotulo("Verificacao do sistema:", 20, y, false));
        y += 22;

        painelChecagem = new Panel();
        painelChecagem.Location = new Point(20, y);
        painelChecagem.Size = new Size(616, 140);
        painelChecagem.BackColor = escuro ? Campo : SystemColors.Window;
        Controls.Add(painelChecagem);
        y += 154;

        Controls.Add(Rotulo("Instalar em:", 20, y, false));
        y += 22;

        txtLocal = new TextBox();
        txtLocal.Location = new Point(20, y);
        txtLocal.Width = 500;
        txtLocal.BorderStyle = BorderStyle.FixedSingle;
        txtLocal.BackColor = escuro ? Campo : SystemColors.Window;
        txtLocal.ForeColor = escuro ? Fg : SystemColors.WindowText;
        string inicial = jaInstalado ? instalacaoAtual : LocalPadrao;
        txtLocal.Text = inicial;
        Controls.Add(txtLocal);
        if (string.IsNullOrEmpty(inicial)) txtLocal.Text = LocalPadrao;

        Controls.Add(Botao("Procurar", 528, y - 3, 108, delegate
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Onde instalar o FolderPin";
                if (d.ShowDialog(this) == DialogResult.OK)
                    txtLocal.Text = Path.Combine(d.SelectedPath, "FolderPin");
            }
        }, false));
        y += 42;

        chkMesa = new CheckBox();
        chkMesa.Text = "Criar atalho na area de trabalho";
        chkMesa.Location = new Point(20, y);
        chkMesa.AutoSize = true;
        chkMesa.Checked = true;
        chkMesa.ForeColor = escuro ? Fg : SystemColors.ControlText;
        Controls.Add(chkMesa);

        chkIniciar = new CheckBox();
        chkIniciar.Text = "Adicionar ao menu Iniciar";
        chkIniciar.Location = new Point(300, y);
        chkIniciar.AutoSize = true;
        chkIniciar.Checked = true;
        chkIniciar.ForeColor = escuro ? Fg : SystemColors.ControlText;
        Controls.Add(chkIniciar);
        y += 40;

        btnInstalar = Botao(jaInstalado ? "Atualizar" : "Instalar", 20, y, 180, Instalar, true);
        Controls.Add(btnInstalar);

        btnDesinstalar = Botao("Desinstalar", 212, y, 140, delegate { Desinstalar(txtLocal.Text.Trim(), true); }, false);
        btnDesinstalar.Visible = jaInstalado;
        Controls.Add(btnDesinstalar);

        Controls.Add(Botao("Fechar", 536, y, 100, delegate { Close(); }, false));
        y += 44;

        lblStatus = new Label();
        lblStatus.Location = new Point(20, y);
        lblStatus.Size = new Size(616, 40);
        lblStatus.ForeColor = Color.FromArgb(160, 160, 160);
        Controls.Add(lblStatus);
    }

    void LinhaChecagem(int i, string titulo, string valor, Color cor)
    {
        Label a = new Label();
        a.Text = titulo;
        a.Location = new Point(12, 10 + i * 26);
        a.Size = new Size(190, 20);
        a.ForeColor = escuro ? Fg : SystemColors.ControlText;
        painelChecagem.Controls.Add(a);

        Label b = new Label();
        b.Text = valor;
        b.Location = new Point(205, 10 + i * 26);
        b.Size = new Size(400, 20);
        b.ForeColor = cor;
        painelChecagem.Controls.Add(b);
    }

    void Checar()
    {
        painelChecagem.Controls.Clear();
        int build = Amb.BuildWindows();

        LinhaChecagem(0, "Windows", Amb.Windows(), build >= 17763 ? Bom : Aviso);

        LinhaChecagem(1, "Arquitetura",
            (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits") +
            "  -  o programa e AnyCPU, roda nas duas", Bom);

        int release;
        string net = Amb.DotNet(out release);
        LinhaChecagem(2, ".NET Framework", net + (release >= 528040 ? "" : "  -  funciona, mas o recomendado e 4.8"),
            release >= 528040 ? Bom : Aviso);

        string fonte = Amb.FonteGlifos();
        LinhaChecagem(3, "Fonte dos icones",
            fonte != null ? fonte : "nenhuma  -  os botoes usam setas de texto",
            fonte != null ? Bom : Aviso);

        LinhaChecagem(4, "Modo escuro do shell",
            build >= 17763 ? "disponivel" : "indisponivel nesta versao  -  cai no tema claro",
            build >= 17763 ? Bom : Aviso);

        lblStatus.Text = jaInstalado
            ? "Ja instalado em " + instalacaoAtual + ". Atualizar troca os programas e mantem seus atalhos."
            : "Nada precisa ser baixado: o instalador traz tudo dentro dele. Destino: [" + txtLocal.Text + "]";
    }

    // ---------- instalacao ----------

    static void Extrai(string recurso, string destino)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso))
        {
            if (s == null) throw new Exception("Recurso ausente no instalador: " + recurso);
            byte[] buf = new byte[s.Length];
            int lidos = 0;
            while (lidos < buf.Length)
            {
                int n = s.Read(buf, lidos, buf.Length - lidos);
                if (n <= 0) break;
                lidos += n;
            }
            File.WriteAllBytes(destino, buf);
        }
    }

    static void CriaAtalho(string lnk, string alvo, string args, string dir, string icone, string desc)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        object a = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        Type ta = a.GetType();
        ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, a, new object[] { alvo });
        ta.InvokeMember("Arguments", BindingFlags.SetProperty, null, a, new object[] { args });
        ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, a, new object[] { dir });
        ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, a, new object[] { icone });
        ta.InvokeMember("Description", BindingFlags.SetProperty, null, a, new object[] { desc });
        ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, a, null);
    }

    void Instalar(object s, EventArgs e)
    {
        string local = txtLocal.Text.Trim();
        if (string.IsNullOrEmpty(local))
        {
            MessageBox.Show(this, "Escolha onde instalar.", Amb.Produto,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnInstalar.Enabled = false;
        try
        {
            Directory.CreateDirectory(local);
            Directory.CreateDirectory(Path.Combine(local, "icones"));

            string studio = Path.Combine(local, "FolderPin Studio.exe");
            string motor = Path.Combine(local, "FolderPin.exe");
            string desinst = Path.Combine(local, "Desinstalar.exe");

            lblStatus.Text = "Copiando arquivos...";
            Application.DoEvents();

            try
            {
                Extrai("FolderPin.exe", motor);
                Extrai("FolderPin Studio.exe", studio);
            }
            catch (IOException)
            {
                MessageBox.Show(this,
                    "Um dos programas esta aberto agora. Feche as janelas do FolderPin e tente de novo.",
                    Amb.Produto, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            File.Copy(Assembly.GetExecutingAssembly().Location, desinst, true);

            lblStatus.Text = "Criando atalhos...";
            Application.DoEvents();

            if (chkMesa.Checked)
                CriaAtalho(Path.Combine(Mesa, "FolderPin.lnk"), studio, "", local,
                    studio + ",0", "Criar atalhos de pasta para a barra de tarefas");

            if (chkIniciar.Checked)
                CriaAtalho(Path.Combine(MenuIniciar, "FolderPin.lnk"), studio, "", local,
                    studio + ",0", "Criar atalhos de pasta para a barra de tarefas");

            long tamanho = 0;
            foreach (string f in Directory.GetFiles(local, "*", SearchOption.AllDirectories))
            {
                try { tamanho += new FileInfo(f).Length; } catch { }
            }

            RegistryKey k = Registry.CurrentUser.CreateSubKey(Amb.ChaveDesinstalar);
            k.SetValue("DisplayName", Amb.Produto);
            k.SetValue("DisplayVersion", Amb.Versao);
            k.SetValue("DisplayIcon", studio);
            k.SetValue("Publisher", "FolderPin");
            k.SetValue("InstallLocation", local);
            k.SetValue("UninstallString", "\"" + desinst + "\" --uninstall");
            k.SetValue("EstimatedSize", (int)(tamanho / 1024), RegistryValueKind.DWord);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.Close();

            lblStatus.Text = "Instalado em " + local;

            if (MessageBox.Show(this,
                "FolderPin instalado." + Environment.NewLine + Environment.NewLine +
                "Abrir agora para criar seu primeiro atalho?",
                Amb.Produto, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(studio);
                Close();
            }
            else
            {
                btnDesinstalar.Visible = true;
                btnInstalar.Text = "Atualizar";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Falhou: " + ex.Message, Amb.Produto,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblStatus.Text = "Falhou.";
        }
        finally { btnInstalar.Enabled = true; }
    }

    // ---------- desinstalacao ----------

    public static List<string> AtalhosDoFolderPin(string local)
    {
        List<string> achados = new List<string>();
        string[] pastas = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
            MenuIniciarEstatico
        };

        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);

        foreach (string dir in pastas)
        {
            if (!Directory.Exists(dir)) continue;
            string[] arquivos;
            try { arquivos = Directory.GetFiles(dir, "*.lnk"); }
            catch { continue; }

            foreach (string lnk in arquivos)
            {
                try
                {
                    object a = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh,
                        new object[] { lnk });
                    Type ta = a.GetType();
                    string alvo = (string)ta.InvokeMember("TargetPath", BindingFlags.GetProperty, null, a, null);
                    string desc = (string)ta.InvokeMember("Description", BindingFlags.GetProperty, null, a, null);

                    bool nosso = !string.IsNullOrEmpty(alvo) && !string.IsNullOrEmpty(local) &&
                                 alvo.StartsWith(local, StringComparison.OrdinalIgnoreCase);
                    bool etiquetado = !string.IsNullOrEmpty(desc) &&
                                      desc.StartsWith("FolderPin", StringComparison.OrdinalIgnoreCase);
                    if (nosso || etiquetado) achados.Add(lnk);
                }
                catch { }
            }
        }
        return achados;
    }

    static string MenuIniciarEstatico
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
        }
    }

    public static void Desinstalar(string local, bool comJanela)
    {
        if (string.IsNullOrEmpty(local))
        {
            RegistryKey k = Registry.CurrentUser.OpenSubKey(Amb.ChaveDesinstalar);
            if (k != null) local = (string)k.GetValue("InstallLocation", null);
        }

        List<string> atalhos = AtalhosDoFolderPin(local);
        bool temFixado = false;
        foreach (string a in atalhos)
        {
            if (a.IndexOf("User Pinned", StringComparison.OrdinalIgnoreCase) >= 0) temFixado = true;
        }

        string aviso = "Remover o FolderPin?" + Environment.NewLine + Environment.NewLine +
            "Serao apagados: os programas em " + local + " e " + atalhos.Count + " atalho(s) criado(s).";
        if (temFixado)
            aviso += Environment.NewLine + Environment.NewLine +
                "ATENCAO: ha atalho fixado na barra de tarefas. Desafixe antes (botao direito no icone > " +
                "Desafixar da barra de tarefas), senao sobra um icone morto que so some reiniciando o Explorador.";

        if (MessageBox.Show(aviso, Amb.Produto, MessageBoxButtons.YesNo,
            temFixado ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes) return;

        foreach (string a in atalhos)
        {
            try { File.Delete(a); } catch { }
        }

        string eu = Assembly.GetExecutingAssembly().Location;
        int presos = 0;
        if (!string.IsNullOrEmpty(local) && Directory.Exists(local))
        {
            foreach (string f in Directory.GetFiles(local, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(f, eu, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(f); } catch { presos++; }
            }
        }

        try { Registry.CurrentUser.DeleteSubKeyTree(Amb.ChaveDesinstalar, false); } catch { }

        string recado = "FolderPin removido.";
        if (presos > 0)
            recado += Environment.NewLine + presos +
                " arquivo(s) estavam abertos e ficaram para tras em " + local + ".";
        if (temFixado)
            recado += Environment.NewLine + Environment.NewLine +
                "Nao esqueca de desafixar o icone da barra de tarefas.";

        MessageBox.Show(recado, Amb.Produto, MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Apaga a si mesmo e a pasta depois que este processo sair.
        try
        {
            string cmd = "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + eu + "\" & rd /s /q \"" + local + "\"";
            System.Diagnostics.ProcessStartInfo psi =
                new System.Diagnostics.ProcessStartInfo("cmd.exe", cmd);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        bool desinstalar = false;
        bool doTemp = false;
        foreach (string a in args)
        {
            if (a == "--uninstall" || a == "/uninstall") desinstalar = true;
            if (a == "--from-temp") doTemp = true;
        }

        try
        {
            Amb.SetPreferredAppMode(Amb.PrefereEscuro() ? 2 : 3);
            Amb.FlushMenuThemes();
        }
        catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (desinstalar)
        {
            string eu = Assembly.GetExecutingAssembly().Location;
            string local = null;
            RegistryKey k = Registry.CurrentUser.OpenSubKey(Amb.ChaveDesinstalar);
            if (k != null) local = (string)k.GetValue("InstallLocation", null);

            // Rodando de dentro da pasta que vai sumir: continua a partir do temporario.
            if (!doTemp && !string.IsNullOrEmpty(local) &&
                eu.StartsWith(local, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string copia = Path.Combine(Path.GetTempPath(), "folderpin-desinstalar.exe");
                    File.Copy(eu, copia, true);
                    System.Diagnostics.Process.Start(copia, "--uninstall --from-temp");
                    return;
                }
                catch { }
            }

            Desinstalar(local, true);
            return;
        }

        Application.Run(new Instalador());
    }
}
