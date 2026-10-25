using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RegraRra = CalculosTrabalhistasTributarios.Domain.Tributacao.CalculadoraRra;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class RraMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int TipoMensal = 0;
    private const int TipoDecimoTerceiro = 1;

    private readonly ISimularDemonstrativoUseCase<SimularRraRequest> _simulador;
    private readonly GradeMobile _parcelas;

    public override string Titulo => "IR sobre rendimentos recebidos acumuladamente (RRA)";
    public override string Descricao => "Estime o IRRF de pagamentos atrasados, separando os anos anteriores, tributados pela tabela acumulada, do ano do pagamento.";

    public RraMobileViewModel(ISimularDemonstrativoUseCase<SimularRraRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<RraMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("pagamentoSecao", "Pagamento");
        Adicionar("dataPagamento", "Data do pagamento (dd/mm/aaaa)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Dia do recebimento ou crédito. Define as tabelas e separa os anos anteriores do ano do pagamento.");
        Adicionar("origem", "Quem paga", TipoCampoMobile.Opcao,
            dica: "Empregador, INSS, Justiça do Trabalho ou Estadual usam a tabela mensal na parte do ano do pagamento; na Justiça Federal, essa parte tem retenção de 3%.",
            opcoes: ["Fonte pagadora ou Justiça do Trabalho", "Justiça Federal (precatório ou RPV)"]);
        Adicionar("reducao", "Redução de 2026 (Lei 15.270)", TipoCampoMobile.Opcao,
            dica: "A IN RFB 1.500/2014, art. 37, manda observar a tabela de redução no RRA, sem detalhar a multiplicação pelos meses. Confira o critério da fonte pagadora.",
            opcoes: ["Aplicar, proporcional aos meses", "Não aplicar"]);

        Secao("geradorSecao", "Gerar parcelas mensais");
        Adicionar("inicioParcelas", "Primeiro mês (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddYears(-1).ToString("01/yyyy", Cultura),
            "Primeira competência a que o pagamento se refere.");
        Adicionar("fimParcelas", "Último mês (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddYears(-1).ToString("12/yyyy", Cultura));
        Adicionar("valorMensal", "Valor por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor tributável sugerido nas competências novas; ajuste cada mês na grade.");

        Secao("deducoesSecao", "Acréscimos e deduções");
        Adicionar("correcao", "Correção monetária (R$)", TipoCampoMobile.Moeda, "0,00",
            "Atualização tributável paga sobre as parcelas, quando não está somada nelas. É rateada pelo valor das parcelas.");
        Adicionar("juros", "Juros de mora: não tributáveis (R$)", TipoCampoMobile.Moeda, "0,00",
            "Juros pelo atraso no pagamento de remuneração do trabalho (IN RFB 1.500/2014, art. 36, § 4º).");
        Adicionar("despesas", "Despesas com a ação (R$)", TipoCampoMobile.Moeda, "0,00",
            "Custas e honorários pagos por você, sem reembolso. Rateadas pelo total recebido; a parte dos juros não é dedutível.");
        Adicionar("inssAnteriores", "INSS dos anos anteriores (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contribuição oficial descontada sobre as parcelas de anos anteriores.");
        Adicionar("pensaoAnteriores", "Pensão dos anos anteriores (R$)", TipoCampoMobile.Moeda, "0,00",
            "Pensão judicial ou por escritura pública descontada sobre as parcelas de anos anteriores.");
        Adicionar("inssAno", "INSS do ano do pagamento (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contribuição descontada sobre as parcelas do ano do pagamento.");
        Adicionar("pensaoAno", "Pensão do ano do pagamento (R$)", TipoCampoMobile.Moeda, "0,00",
            "Pensão judicial descontada sobre as parcelas do ano do pagamento.");
        Adicionar("totalParcelado", "Total parcelado (opcional) (R$)", TipoCampoMobile.Moeda, "0,00",
            "Só se o mesmo RRA é pago em meses diferentes: soma das parcelas de anos anteriores, para dividir os meses pelo valor (art. 45, I). Deixe 0,00 se este é o único pagamento.");

        Secao("mesSecao", "Rendimentos normais do mês");
        Adicionar("outrosRendimentos", "Rendimentos normais do mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Salário ou benefício tributável pago pela mesma fonte no mês do pagamento, somado à parte do ano do pagamento.");
        Adicionar("inssOutros", "INSS dos rendimentos normais (R$)", TipoCampoMobile.Moeda, "0,00",
            "Contribuição do mês sobre os rendimentos normais.");
        Adicionar("dependentes", "Dependentes", TipoCampoMobile.Inteiro, "0",
            "Dependentes do IRRF mensal. Não se aplicam à parte de anos anteriores.");

        _parcelas = AdicionarGrade("parcelas", "Parcelas do RRA",
            "Uma linha por competência a que o pagamento se refere, com o valor tributável recebido. Acrescente uma linha de 13º salário "
            + "para cada ano em que ele foi pago; nela vale o ano da competência (por exemplo 12/2024).",
            "Parcela", RegraRra.LimiteParcelas,
            [
                new("tipo", "Tipo", TipoCampoMobile.Opcao, Opcoes: ["Mensal", "13º salário"]),
                new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia),
                new("valor", "Valor tributável (R$)", TipoCampoMobile.Moeda, "0,00")
            ], SugerirAnoDecimoTerceiro);
        _parcelas.GerarCommand = new RelayCommand(GerarMeses);
        _parcelas.RotuloGerar = "Gerar meses";
        AtualizarVisibilidade();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "origem") AtualizarVisibilidade();
    }

    // Os rendimentos normais e os dependentes só existem quando a parte do ano do pagamento usa a tabela mensal.
    private void AtualizarVisibilidade()
    {
        var fontePagadora = Campo("origem").Indice == 0;
        foreach (var chave in new[] { "mesSecao", "outrosRendimentos", "inssOutros", "dependentes" })
            Campo(chave).Visivel = fontePagadora;
    }

    private void SugerirAnoDecimoTerceiro(LinhaGradeMobile linha)
    {
        var competencia = linha.Campo("competencia");
        if (linha.Campo("tipo").Indice != TipoDecimoTerceiro || competencia.Valor.Trim().Length > 0) return;
        var ano = TentarCompetencia(Texto("fimParcelas"), out var fim) ? fim.Year : DateTime.Today.Year - 1;
        competencia.Valor = $"12/{ano}";
    }

    /// <summary>
    /// Gera as competências mensais do período sem perder dados: linhas mensais do período mantêm o valor, as de 13º e as
    /// mensais fora do período continuam na grade, e tudo é ordenado por ano, com o 13º depois de dezembro.
    /// </summary>
    private void GerarMeses()
    {
        try
        {
            var inicio = Competencia("inicioParcelas");
            var fim = Competencia("fimParcelas");
            if (inicio > fim)
                throw new EntradaInvalidaException("Último mês: informe um mês igual ou posterior ao primeiro.");
            var valor = Numero("valorMensal");
            if (valor < 0m)
                throw new EntradaInvalidaException("Valor por mês: informe um valor positivo ou zero, por exemplo 1.250,00.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > RegraRra.LimiteParcelas)
                throw new EntradaInvalidaException($"O cálculo aceita até {RegraRra.LimiteParcelas} linhas. Reduza o período.");

            var mensais = _parcelas.Linhas.Where(linha => linha.Campo("tipo").Indice == TipoMensal)
                .GroupBy(linha => linha.Campo("competencia").Valor.Trim()).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
            var itens = new List<(LinhaGradeMobile? Linha, string Competencia, int Tipo)>(quantidade + _parcelas.Linhas.Count);
            var aproveitadas = new HashSet<LinhaGradeMobile>();
            for (var indice = 0; indice < quantidade; indice++)
            {
                var chave = inicio.AddMonths(indice).ToString("MM/yyyy", Cultura);
                if (mensais.TryGetValue(chave, out var existente))
                {
                    aproveitadas.Add(existente);
                    itens.Add((existente, chave, TipoMensal));
                }
                else
                    itens.Add((null, chave, TipoMensal));
            }
            var demais = _parcelas.Linhas.Where(linha => !aproveitadas.Contains(linha)).ToList();
            if (itens.Count + demais.Count > RegraRra.LimiteParcelas)
                throw new EntradaInvalidaException($"Remova as linhas fora do período antes de gerar: a grade aceita até {RegraRra.LimiteParcelas} linhas.");
            itens.AddRange(demais.Select(linha => ((LinhaGradeMobile?)linha, linha.Campo("competencia").Valor.Trim(), linha.Campo("tipo").Indice)));
            var foraDoPeriodo = demais.Count(linha => linha.Campo("tipo").Indice == TipoMensal);

            var ordenados = itens.OrderBy(item => Ordem(item.Competencia, item.Tipo)).ToList();
            _parcelas.Remocao.Descartar();
            while (_parcelas.Linhas.Count > 0) _parcelas.Linhas.RemoveAt(_parcelas.Linhas.Count - 1);
            var novas = 0;
            foreach (var item in ordenados)
            {
                if (item.Linha is { } linha) _parcelas.Linhas.Add(linha);
                else
                {
                    _parcelas.AdicionarLinha(new Dictionary<string, string>
                    {
                        ["competencia"] = item.Competencia,
                        ["valor"] = valor.ToString("N2", Cultura)
                    });
                    novas++;
                }
            }
            _parcelas.Aviso = $"{quantidade} competência(s): {quantidade - novas} preservada(s), {novas} nova(s)."
                + (foraDoPeriodo > 0 ? $" {foraDoPeriodo} linha(s) mensal(is) fora do período foram mantidas; remova-as se não fizerem parte do pagamento." : "")
                + " Acrescente uma linha de 13º salário para cada ano em que ele foi pago.";
        }
        catch (EntradaInvalidaException erro) { _parcelas.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        if (_parcelas.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere os meses ou adicione as parcelas a que o pagamento se refere.");
        var parcelas = _parcelas.Ler(linha => new ParcelaRra(LeituraCampoMobile.Competencia(linha.Campo("competencia")),
            LeituraCampoMobile.Opcao(linha.Campo("tipo")) == TipoMensal ? TipoParcelaRra.Mensal : TipoParcelaRra.DecimoTerceiro,
            LeituraCampoMobile.Numero(linha.Campo("valor"))));
        var repetida = parcelas.GroupBy(item => (item.Tipo, Chave: item.Tipo == TipoParcelaRra.Mensal ? item.Competencia : new DateOnly(item.Competencia.Year, 1, 1)))
            .FirstOrDefault(grupo => grupo.Count() > 1);
        if (repetida is not null)
            throw new EntradaInvalidaException(repetida.Key.Tipo == TipoParcelaRra.Mensal
                ? $"A competência {repetida.Key.Chave.ToString("MM/yyyy", Cultura)} aparece mais de uma vez: some os valores do mês em uma única linha."
                : $"Há mais de uma linha de 13º salário de {repetida.Key.Chave.Year}: some os valores em uma única linha.");

        var origem = Opcao("origem") == 0 ? OrigemPagamentoRra.FontePagadora : OrigemPagamentoRra.JusticaFederal;
        var fontePagadora = origem == OrigemPagamentoRra.FontePagadora;
        var totalParcelado = Numero("totalParcelado");
        // Campos ocultos na Justiça Federal não participam do cálculo, mesmo que tenham valores de antes da troca.
        return _simulador.ExecutarAsync(new SimularRraRequest(new PagamentoRra(Data("dataPagamento"), origem, parcelas,
            Numero("correcao"), Numero("juros"), Numero("despesas"), Numero("inssAnteriores"), Numero("pensaoAnteriores"),
            Numero("inssAno"), Numero("pensaoAno"), totalParcelado > 0m ? totalParcelado : null, Opcao("reducao") == 0,
            fontePagadora ? Numero("outrosRendimentos") : 0m, fontePagadora ? Numero("inssOutros") : 0m,
            fontePagadora ? Inteiro("dependentes") : 0)), cancellationToken);
    }

    private static (int Ano, int Mes) Ordem(string competencia, int tipo) =>
        TentarCompetencia(competencia, out var data) ? (data.Year, tipo == TipoDecimoTerceiro ? 13 : data.Month) : (int.MaxValue, 0);

    private static bool TentarCompetencia(string texto, out DateOnly competencia)
    {
        var lido = DateOnly.TryParseExact("01/" + texto.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out competencia);
        return lido;
    }
}
