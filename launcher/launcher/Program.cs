using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ConversorLauncher // Nome corrigido
{
    class Program
    {
        static void Main(string[] args)
        {
            string zplPath = args.Length > 0 ? args[0] : "";

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            // PASTA CORRIGIDA: Agora ele cria C:\ProgramData\ConversorZPL
            string pastaSistema = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ConversorZPL");
            Directory.CreateDirectory(pastaSistema);

            string caminhoCore = Path.Combine(pastaSistema, "ConversorCore.exe");
            string urlControle = "https://fpvyjwhfqedozgepjcex.supabase.co/storage/v1/object/public/update/versao.json";

            string versaoLocal = "1.0.0";
            string arquivoVersaoLocal = Path.Combine(pastaSistema, "versao_atual.txt");
            if (File.Exists(arquivoVersaoLocal)) versaoLocal = File.ReadAllText(arquivoVersaoLocal);

            try
            {
                using (WebClient client = new WebClient())
                {
                    string jsonNuvem = client.DownloadString(urlControle);

                    string versaoNuvem = Regex.Match(jsonNuvem, @"""versao""\s*:\s*""([^""]+)""").Groups[1].Value;
                    string urlDownloadExe = Regex.Match(jsonNuvem, @"""url_download""\s*:\s*""([^""]+)""").Groups[1].Value;

                    if (!string.IsNullOrEmpty(versaoNuvem) && (versaoNuvem != versaoLocal || !File.Exists(caminhoCore)))
                    {
                        client.DownloadFile(urlDownloadExe, caminhoCore);
                        File.WriteAllText(arquivoVersaoLocal, versaoNuvem);
                    }
                }
            }
            catch
            {
                // Ignora erro de rede silenciosamente
            }

            if (File.Exists(caminhoCore))
            {
                string argumentos = string.IsNullOrEmpty(zplPath) ? "" : $"\"{zplPath}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = caminhoCore,
                    Arguments = argumentos,
                    UseShellExecute = false
                });
            }
            else
            {
                // Primeira execução sem internet: o núcleo ainda não foi baixado
                MessageBox.Show(
                    "Não foi possível inicializar o ConversorZPL.\n\nVerifique sua conexão com a internet e tente novamente.\n\nSe o problema persistir, entre em contato com o suporte.",
                    "ConversorZPL — Erro de Inicialização",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}