using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Views;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class PreviewQuitacaoTemporariaTests
{
    [Fact]
    public void RenderizarBloco()
    {
        Exception? erro = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                foreach (var (largura, tema) in new[] { (1180, "claro"), (900, "minimo") })
                {
                    var campo = new CampoQuitacaoBancoHorasViewModel();
                    campo.Importar(CampoQuitacaoBancoHorasViewModel.CriarImportacao(new QuitacaoBancoHoras(
                        new DateOnly(2026, 10, 31), SituacaoBancoHoras.Fechamento, 123456.78m, 220m,
                        [new(50m, 30, 420.88m), new(100m, 60, 1122.33m)])));
                    var janela = new CalculadoraWindow(null!) { Width = largura, Height = 620, Left = -3000, Top = -3000, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                    var conteudo = new ContentControl { Content = campo, Margin = new Thickness(20) };
                    janela.Content = conteudo;
                    janela.Show();
                    janela.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(largura, 380, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(conteudo);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var arquivo = File.Create($"C:\\Programacao\\CalculosTrabalhistasTributarios\\docs\\preview-quitacao-{tema}.png");
                    encoder.Save(arquivo);
                    janela.Close();
                }
                app.Shutdown();
            }
            catch (Exception exception) { erro = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (erro is not null) throw erro;
    }
}
