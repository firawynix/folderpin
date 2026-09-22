using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

sealed class PacoteAtualizacao
{
    public string url { get; set; }
    public string sha256 { get; set; }
    public long size { get; set; }
}

sealed class ManifestoAtualizacao
{
    public string version { get; set; }
    public Dictionary<string, PacoteAtualizacao> architectures { get; set; }
}

static class AutoUpdate
{
    const string Base = "https://jogos.firawynix.com.br/api/games/";

    public static void Verificar(string slug, string versaoAtual)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                using (Mutex trava = new Mutex(false, @"Local\Firawynix.Update." + slug))
                {
                    if (!trava.WaitOne(0)) return;
                    VerificarAgora(slug, versaoAtual);
                }
            }
            catch { }
        });
    }

    static void VerificarAgora(string slug, string versaoAtual)
    {
        string raiz = Base + slug + "/windows/";
        ManifestoAtualizacao manifesto;
        using (WebClient web = new WebClient())
        {
            web.Headers[HttpRequestHeader.UserAgent] = "Firawynix-AutoUpdate/1.0";
            manifesto = new JavaScriptSerializer().Deserialize<ManifestoAtualizacao>(
                web.DownloadString(raiz + "atualizacao.json"));
        }
        Version nova, atual;
        if (manifesto == null || !Version.TryParse(manifesto.version, out nova)
            || !Version.TryParse(versaoAtual, out atual) || nova <= atual
            || manifesto.architectures == null) return;
        string arquitetura = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        PacoteAtualizacao pacote;
        if (!manifesto.architectures.TryGetValue(arquitetura, out pacote)
            || pacote == null || !pacote.url.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) return;

        string pasta = Path.Combine(Path.GetTempPath(), "FirawynixUpdates", slug, manifesto.version);
        Directory.CreateDirectory(pasta);
        string instalador = Path.Combine(pasta, slug + "-" + arquitetura + ".exe");
        using (WebClient web = new WebClient())
        {
            web.Headers[HttpRequestHeader.UserAgent] = "Firawynix-AutoUpdate/1.0";
            web.DownloadFile(pacote.url, instalador);
        }
        FileInfo info = new FileInfo(instalador);
        if (!info.Exists || info.Length != pacote.size || !Hash(instalador).Equals(pacote.sha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(instalador); } catch { }
            return;
        }
        string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FirawAutoUpdate.exe");
        if (!File.Exists(helper)) return;
        string copia = Path.Combine(pasta, "FirawAutoUpdate-" + Process.GetCurrentProcess().Id + ".exe");
        File.Copy(helper, copia, true);
        ProcessStartInfo psi = new ProcessStartInfo(copia, Process.GetCurrentProcess().Id + " \"" + instalador + "\"");
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(psi);
    }

    static string Hash(string caminho)
    {
        using (FileStream arquivo = File.OpenRead(caminho))
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(arquivo)).Replace("-", "").ToLowerInvariant();
    }
}
