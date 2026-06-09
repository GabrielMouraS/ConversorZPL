using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace ConversorZPL
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. O GATILHO: Recebe o caminho do arquivo ZPL no duplo clique
            if (args.Length == 0) return;
            string zplPath = args[0];

            try
            {
                // 2. A SALVAÇÃO DO WIN 7: Força o TLS 1.2 para a internet conectar
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                // 3. IDENTIFICAÇÃO: Captura o MAC Address da placa de rede
                string macAddress = CapturarMacAddress();

                // 4. CONSULTA DE VALIDADE
                // Grava a data de instalação uma única vez em disco.
                // [FUTURO]: Substituir pela leitura real do banco de dados no Supabase.
                string pastaSistema = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ConversorZPL");
                Directory.CreateDirectory(pastaSistema);

                string arquivoInstalacao = Path.Combine(pastaSistema, "instalado_em.txt");
                DateTime dataInstalacao;
                if (!File.Exists(arquivoInstalacao))
                {
                    dataInstalacao = DateTime.Now;
                    File.WriteAllText(arquivoInstalacao, dataInstalacao.ToString("O"));
                }
                else
                {
                    dataInstalacao = DateTime.Parse(File.ReadAllText(arquivoInstalacao));
                }

                DateTime dataVencimento = dataInstalacao.AddDays(30);
                int diasRestantes = (dataVencimento - DateTime.Now).Days;

                // 5. A RÉGUA DE COBRANÇA (As 3 Fases)
                if (diasRestantes < 0)
                {
                    // FASE 3: VENCIDO - Bloqueia e manda pra Stripe
                    MessageBox.Show(
                        "O período de teste do Conversor de Etiquetas finalizou.\n\nClique em OK para acessar o portal de assinatura e reativar o sistema instantaneamente.",
                        "Licença Expirada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    // O Link da Stripe com o MAC Address embutido para o Webhook reconhecer quem pagou
                    string linkDePagamento = $"https://buy.stripe.com/SEU_LINK_AQUI?client_reference_id={macAddress}";
                    Process.Start(linkDePagamento);

                    return; // Encerra o programa. A etiqueta não é impressa.
                }
                else if (diasRestantes <= 5)
                {
                    // FASE 2: AVISO (Faltam 5 dias ou menos) - Avisa, mas deixa imprimir
                    MessageBox.Show(
                        $"O seu período de teste grátis encerra em {diasRestantes} dias.\n\nSua etiqueta será gerada normalmente agora.",
                        "Aviso de Renovação",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }

                // FASE 1: INVISÍVEL - Se faltarem mais de 5 dias, passa direto.

                // 6. INTERAÇÃO COM O USUÁRIO: Tela customizada com botões grandes
                string tamanhoApi = "";

                using (Form tela = new Form())
                {
                    tela.Text = "Selecionar Tamanho da Etiqueta";
                    tela.Size = new System.Drawing.Size(420, 220);
                    tela.StartPosition = FormStartPosition.CenterScreen;
                    tela.FormBorderStyle = FormBorderStyle.FixedDialog;
                    tela.MaximizeBox = false;
                    tela.MinimizeBox = false;

                    Label textoPergunta = new Label()
                    {
                        Text = "Qual etiqueta está na impressora neste momento?",
                        Location = new System.Drawing.Point(20, 20),
                        AutoSize = true,
                        Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold)
                    };

                    Button btnGrande = new Button()
                    {
                        Text = "GRANDE\n(100x150mm)",
                        Location = new System.Drawing.Point(20, 60),
                        Size = new System.Drawing.Size(170, 90),
                        Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    btnGrande.Click += delegate { tamanhoApi = "4x6"; tela.Close(); };

                    Button btnPequena = new Button()
                    {
                        Text = "PEQUENA\n(60x40mm)",
                        Location = new System.Drawing.Point(210, 60),
                        Size = new System.Drawing.Size(170, 90),
                        Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    btnPequena.Click += delegate { tamanhoApi = "2.36x1.57"; tela.Close(); };

                    tela.Controls.Add(textoPergunta);
                    tela.Controls.Add(btnGrande);
                    tela.Controls.Add(btnPequena);

                    tela.ShowDialog();
                }

                // Se fechou no X e não escolheu nada, aborta silenciosamente
                if (string.IsNullOrEmpty(tamanhoApi))
                {
                    return;
                }

                // 7. PROCESSAMENTO: Lê o ZPL e bate no Labelary com o tamanho dinâmico
                string zplContent = File.ReadAllText(zplPath);

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/pdf"));

                    var content = new StringContent(zplContent, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                    var response = await client.PostAsync($"http://api.labelary.com/v1/printers/8dpmm/labels/{tamanhoApi}/", content);

                    if (response.IsSuccessStatusCode)
                    {
                        byte[] pdfBytes = await response.Content.ReadAsByteArrayAsync();

                        // Nome com timestamp para evitar conflito quando o PDF anterior ainda está aberto
                        string pdfPath = Path.Combine(Path.GetTempPath(), $"etiqueta_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                        File.WriteAllBytes(pdfPath, pdfBytes);

                        Process.Start(pdfPath);
                    }
                    else
                    {
                        MessageBox.Show("Erro ao converter etiqueta. Verifique a conexão com a internet.", "Erro de Comunicação", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Salva log de erro silencioso em caso de falha grave
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log_erro_conversor.txt"), ex.Message);
            }
        }

        // Captura do MAC Address da máquina para validação e envio para a Stripe
        static string CapturarMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                return nic != null ? nic.GetPhysicalAddress().ToString() : "MAC_NAO_ENCONTRADO";
            }
            catch
            {
                return "ERRO_MAC";
            }
        }
    }
}