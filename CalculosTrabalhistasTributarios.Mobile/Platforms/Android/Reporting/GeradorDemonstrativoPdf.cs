using Android.Graphics;
using Android.Graphics.Pdf;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using System.Globalization;
using Cor = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;

namespace CalculosTrabalhistasTributarios.Mobile.Platforms.Android.Reporting;

/// <summary>PDF A4 nativo do Android. Consome a apuração pronta, sem recalcular regras de negócio.</summary>
internal static class GeradorDemonstrativoPdf
{
    public static void Gerar(DemonstrativoDto dto, string caminho, CancellationToken cancellationToken)
    {
        using var documento = new PdfDocument();
        using var escritor = new EscritorPaginado(documento, cancellationToken);
        escritor.Texto("CÁLCULOS TRABALHISTAS E TRIBUTÁRIOS", 10, true);
        escritor.Texto(dto.Titulo, 22, true);
        escritor.Texto(dto.Referencia);
        escritor.Texto($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
        escritor.Secao(dto.RotuloResultado);
        escritor.Texto(Moeda(dto.Resultado), 25, true);
        foreach (var destaque in dto.Destaques)
        {
            escritor.Texto($"{destaque.Rotulo}: {destaque.Valor}", 11, true);
            escritor.Texto(destaque.Complemento);
        }
        if (dto.Comparativo is { } comparativo)
        {
            escritor.Secao(comparativo.Titulo);
            foreach (var linha in comparativo.Linhas)
            {
                escritor.Texto(linha.Descricao, 11, true);
                foreach (var (coluna, valor) in comparativo.Colunas.Zip(linha.Valores))
                    escritor.Texto($"{coluna}: {valor}");
            }
        }
        Verbas(escritor, dto.RotuloProventos, dto.Proventos);
        escritor.Texto($"Total: {Moeda(dto.TotalProventos)}", 11, true);
        Verbas(escritor, "Descontos", dto.Descontos);
        escritor.Texto($"Total de descontos: {Moeda(dto.TotalDescontos)}", 11, true);
        if (dto.Informativos.Count > 0)
            Verbas(escritor, "Informativos (fora do resultado)", dto.Informativos);
        escritor.Secao("Memória de cálculo");
        foreach (var grupo in dto.Memoria)
        {
            escritor.Texto(grupo.Titulo, 12, true);
            escritor.Texto(grupo.Destaque);
            foreach (var formula in grupo.Formulas)
            {
                escritor.Texto(formula.Titulo, 10, true);
                escritor.Texto(formula.Formula);
            }
            if (grupo.Quadro is { } quadro)
            {
                foreach (var faixa in quadro.Faixas)
                {
                    escritor.Texto($"Faixa {faixa.Numero}: {Moeda(faixa.De)} a {(faixa.Ate is { } ate ? Moeda(ate) : "sem limite")}", 10, true);
                    escritor.Texto($"Base na faixa: {Moeda(faixa.ParteDaBase)} | Alíquota: {faixa.Aliquota.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))}%");
                    escritor.Texto($"Valor exato: {Exato(faixa.ValorExato)} | Considerado: {Exato(faixa.Valor)}");
                }
                escritor.Texto($"Base total: {Moeda(quadro.BaseCalculo)} | Soma exata: {Exato(quadro.ValorExato)} | Considerado: {Moeda(quadro.Valor)}", 10, true);
            }
        }
        escritor.Secao("Premissas e limites");
        foreach (var observacao in dto.Observacoes)
            escritor.Texto(observacao);
        escritor.Concluir();
        cancellationToken.ThrowIfCancellationRequested();
        using var arquivo = File.Create(caminho);
        documento.WriteTo(arquivo);
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
    private static string Exato(decimal valor) => "R$ " + valor.ToString("#,##0.00####", CultureInfo.GetCultureInfo("pt-BR"));

    private static void Verbas(EscritorPaginado escritor, string titulo, IReadOnlyList<VerbaDto> verbas)
    {
        escritor.Secao(titulo);
        if (verbas.Count == 0)
            escritor.Texto("Nenhum valor informado.");
        foreach (var verba in verbas)
        {
            escritor.Texto($"{verba.Descricao}: {Moeda(verba.Valor)}", 11, true);
            if (!string.IsNullOrWhiteSpace(verba.Referencia))
                escritor.Texto(verba.Referencia);
        }
    }

    private sealed class EscritorPaginado : IDisposable
    {
        private const int Largura = 595;
        private const int Altura = 842;
        private const float Margem = 36;
        private const float LimiteConteudo = 720;
        private readonly PdfDocument _documento;
        private readonly CancellationToken _cancellationToken;
        private readonly Paint _pincel = new(PaintFlags.AntiAlias);
        private PdfDocument.Page? _pagina;
        private int _numero;
        private float _y;

        public EscritorPaginado(PdfDocument documento, CancellationToken cancellationToken)
        {
            _documento = documento;
            _cancellationToken = cancellationToken;
        }

        public void Secao(string titulo)
        {
            GarantirEspaco(54);
            _y += 12;
            Texto(titulo, 14, true);
        }

        public void Texto(string texto, float tamanho = 10, bool negrito = false)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (negrito)
                GarantirEspaco(tamanho * 3 + 12);
            _pincel.TextSize = tamanho;
            _pincel.Color = negrito ? Cor.Rgb(23, 74, 103) : Cor.Rgb(43, 55, 68);
            _pincel.SetTypeface(negrito ? Typeface.DefaultBold : Typeface.Default);
            foreach (var paragrafo in texto.Replace("\r", string.Empty).Split('\n'))
            {
                var restante = paragrafo.Trim();
                while (restante.Length > 0)
                {
                    GarantirEspaco(tamanho * 1.5f);
                    // O rodapé usa o mesmo Paint; restaure o estilo após eventual troca de página.
                    _pincel.TextSize = tamanho;
                    _pincel.Color = negrito ? Cor.Rgb(23, 74, 103) : Cor.Rgb(43, 55, 68);
                    _pincel.SetTypeface(negrito ? Typeface.DefaultBold : Typeface.Default);
                    var quantidade = Math.Max(1, _pincel.BreakText(restante, true, Largura - 2 * Margem, null));
                    if (quantidade < restante.Length)
                    {
                        var espaco = restante.LastIndexOf(' ', quantidade - 1, quantidade);
                        if (espaco > 0)
                            quantidade = espaco;
                    }
                    var canvas = _pagina?.Canvas ?? throw new InvalidOperationException("Página PDF não iniciada.");
                    _y += tamanho * 1.4f;
                    canvas.DrawText(restante[..quantidade], Margem, _y, _pincel);
                    restante = restante[quantidade..].TrimStart();
                }
                _y += 6;
            }
        }

        private void GarantirEspaco(float altura)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_pagina is not null && _y + altura <= LimiteConteudo)
                return;
            Concluir();
            using var info = new PdfDocument.PageInfo.Builder(Largura, Altura, ++_numero).Create();
            _pagina = _documento.StartPage(info);
            _y = Margem;
        }

        public void Concluir()
        {
            if (_pagina is null)
                return;
            var canvas = _pagina.Canvas ?? throw new InvalidOperationException("Página PDF sem canvas.");
            _pincel.Color = Cor.Rgb(23, 74, 103);
            canvas.DrawLine(Margem, 748, Largura - Margem, 748, _pincel);
            _pincel.TextSize = 9;
            _pincel.SetTypeface(Typeface.DefaultBold);
            canvas.DrawText(AvisoSimulacao.Titulo, Margem, 767, _pincel);
            _pincel.SetTypeface(Typeface.Default);
            var restante = AvisoSimulacao.Orientacao;
            var linha = 782f;
            while (restante.Length > 0)
            {
                var quantidade = Math.Max(1, _pincel.BreakText(restante, true, Largura - 2 * Margem, null));
                if (quantidade < restante.Length)
                {
                    var espaco = restante.LastIndexOf(' ', quantidade - 1, quantidade);
                    if (espaco > 0)
                        quantidade = espaco;
                }
                canvas.DrawText(restante[..quantidade], Margem, linha, _pincel);
                restante = restante[quantidade..].TrimStart();
                linha += 11;
            }
            canvas.DrawText($"Processado localmente | Página {_numero}", Margem, 816, _pincel);
            _documento.FinishPage(_pagina);
            _pagina.Dispose();
            _pagina = null;
        }

        public void Dispose()
        {
            // PdfDocument não pode ser fechado com uma página em aberto, inclusive após cancelamento.
            Concluir();
            _pincel.Dispose();
        }
    }
}
