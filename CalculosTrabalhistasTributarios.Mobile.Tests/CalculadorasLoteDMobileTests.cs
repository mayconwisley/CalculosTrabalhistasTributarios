using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteDMobileTests
{
    [Fact]
    public async Task Inss_multiplos_vinculos_preserva_ordem_categorias_e_identificacao()
    {
        var captura = new Captura<SimularMultiplosVinculosInssRequest>();
        var vm = new InssMultiplosVinculosMobileViewModel(captura, new PdfTeste(), NullLogger<InssMultiplosVinculosMobileViewModel>.Instance);
        var grade = Grade(vm, "vinculos");
        Assert.Equal(2, grade.Linhas.Count);
        Alterar(vm, "competencia", "03/2026");
        grade.Linhas[0].Campo("remuneracao").Valor = "2.000,00";
        grade.Linhas[0].Campo("identificacao").Valor = "Empresa A";
        grade.Linhas[1].Campo("categoria").Indice = 3;
        grade.Linhas[1].Campo("remuneracao").Valor = "1.500,50";
        grade.AdicionarCommand.Execute(null);
        var terceira = grade.Linhas[2];
        terceira.Campo("categoria").Indice = 4;

        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Vínculo 3: Remuneração (R$)", vm.Mensagem);

        terceira.Campo("remuneracao").Valor = "800,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 3, 1), captura.Pedido?.Competencia);
        Assert.Equal(
            new[]
            {
                new VinculoInss("Empresa A", TipoVinculoInss.Empregado, 2000m),
                new VinculoInss("Vínculo 2", TipoVinculoInss.ContribuinteIndividual, 1500.50m),
                new VinculoInss("Vínculo 3", TipoVinculoInss.ContribuinteIndividualEbas, 800m)
            },
            captura.Pedido?.Vinculos);

        captura.Limpar();
        grade.RemoverCommand.Execute(grade.Linhas[2]);
        grade.RemoverCommand.Execute(grade.Linhas[1]);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("pelo menos dois vínculos", vm.Mensagem);
    }

    [Fact]
    public async Task Reajuste_retroativo_gera_meses_sem_perder_valores_e_le_reflexos()
    {
        var captura = new Captura<SimularReajusteRetroativoRequest>();
        var vm = new ReajusteRetroativoMobileViewModel(captura, new PdfTeste(), NullLogger<ReajusteRetroativoMobileViewModel>.Instance);
        var meses = Grade(vm, "meses");
        var reflexos = Grade(vm, "reflexos");
        Alterar(vm, "inicio", "01/2026");
        Alterar(vm, "fim", "03/2026");
        Alterar(vm, "salarioAnterior", "3.000,00");
        Gerar(meses);
        Assert.Empty(meses.Linhas);
        Assert.Contains("reajuste maior que zero", meses.Aviso);

        Alterar(vm, "percentual", "5");
        Gerar(meses);
        Assert.Equal(new[] { "01/2026", "02/2026", "03/2026" }, meses.Linhas.Select(linha => linha.Campo("competencia").Valor));
        Assert.All(meses.Linhas, linha => Assert.Equal("3.150,00", linha.Campo("devido").Valor));
        Assert.Contains("criada", meses.Aviso);
        meses.Linhas[1].Campo("pago").Valor = "3.100,00";
        Alterar(vm, "fim", "04/2026");
        Gerar(meses);
        Assert.Equal(4, meses.Linhas.Count);
        Assert.Equal("3.100,00", meses.Linhas[1].Campo("pago").Valor);
        Assert.Equal("3.000,00", meses.Linhas[3].Campo("pago").Valor);

        reflexos.AdicionarCommand.Execute(null);
        var reflexo = reflexos.Linhas[0];
        Assert.Equal("04/2026", reflexo.Campo("competencia").Valor);
        Assert.Equal("12", reflexo.Campo("quantidade").Valor);
        reflexo.Campo("tipo").Indice = 1;
        Assert.Equal("30", reflexo.Campo("quantidade").Valor);
        reflexo.Campo("pago").Valor = "-1,00";
        reflexo.Campo("devido").Valor = "3.150,00";
        reflexo.Campo("quantidade").Valor = "10";

        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Reflexo 1: O campo \"Base paga (R$)\"", vm.Mensagem);

        reflexo.Campo("pago").Valor = "3.000,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        var parcelas = captura.Pedido!.Parcelas;
        Assert.Equal(5, parcelas.Count);
        Assert.All(parcelas.Take(4), parcela => Assert.Equal((TipoParcelaReajuste.Salario, 1), (parcela.Tipo, parcela.Quantidade)));
        Assert.Equal(new ParcelaReajusteRetroativo(new DateOnly(2026, 2, 1), TipoParcelaReajuste.Salario, 3100m, 3150m, 1), parcelas[1]);
        Assert.Equal(new ParcelaReajusteRetroativo(new DateOnly(2026, 4, 1), TipoParcelaReajuste.FeriasGozadas, 3000m, 3150m, 10), parcelas[4]);
    }

    [Fact]
    public async Task Resgate_previdencia_confirma_substituicao_e_le_valor_parcial_somente_quando_visivel()
    {
        var captura = new Captura<SimularResgatePrevidenciaRequest>();
        var vm = new ResgatePrevidenciaMobileViewModel(captura, new PdfTeste(), NullLogger<ResgatePrevidenciaMobileViewModel>.Instance);
        var aportes = Grade(vm, "aportes");
        Assert.False(CampoDe(vm, "valor").Visivel);
        Opcao(vm, "resgate", 1);
        Assert.True(CampoDe(vm, "valor").Visivel);

        Alterar(vm, "dataResgate", "15/06/2026");
        Alterar(vm, "primeiroAporte", "15/01/2026");
        Alterar(vm, "quantidade", "3");
        Alterar(vm, "valorAporte", "500,00");
        Alterar(vm, "rentabilidade", "0");
        Gerar(aportes);
        Assert.Equal(new[] { "15/01/2026", "15/02/2026", "15/03/2026" }, aportes.Linhas.Select(linha => linha.Campo("data").Valor));
        Assert.All(aportes.Linhas, linha => Assert.Equal("500,00", linha.Campo("aportado").Valor));

        aportes.Linhas[0].Campo("saldo").Valor = "650,00";
        Gerar(aportes);
        Assert.Contains("serão substituídos", aportes.Aviso);
        Assert.Equal("650,00", aportes.Linhas[0].Campo("saldo").Valor);
        Gerar(aportes);
        Assert.Equal(3, aportes.Linhas.Count);
        Assert.NotEqual("650,00", aportes.Linhas[0].Campo("saldo").Valor);

        aportes.AdicionarCommand.Execute(null);
        var quarto = aportes.Linhas[3];
        Assert.Equal("500,00", quarto.Campo("aportado").Valor);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Aporte 4: O campo \"Data (dd/mm/aaaa)\"", vm.Mensagem);

        quarto.Campo("data").Valor = "15/04/2026";
        quarto.Campo("saldo").Valor = "520,00";
        Opcao(vm, "tipo", 1);
        Opcao(vm, "regime", 1);
        Alterar(vm, "valor", "1.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var entrada = captura.Pedido!.Entrada;
        Assert.Equal(TipoPlanoPrevidencia.Vgbl, entrada.Tipo);
        Assert.Equal(RegimeTributacaoPrevidencia.Progressivo, entrada.Regime);
        Assert.Equal(new DateOnly(2026, 6, 15), entrada.DataResgate);
        Assert.Equal(1000m, entrada.ValorResgate);
        Assert.Equal(4, entrada.Aportes.Count);
        Assert.Equal(new AportePrevidencia(new DateOnly(2026, 4, 15), 500m, 520m), entrada.Aportes[3]);

        Opcao(vm, "resgate", 0);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido!.Entrada.ValorResgate);
    }

    [Fact]
    public async Task Rra_ordena_parcelas_preserva_decimo_terceiro_e_ignora_campos_ocultos()
    {
        var captura = new Captura<SimularRraRequest>();
        var vm = new RraMobileViewModel(captura, new PdfTeste(), NullLogger<RraMobileViewModel>.Instance);
        var parcelas = Grade(vm, "parcelas");
        Assert.True(CampoDe(vm, "dependentes").Visivel);
        Alterar(vm, "inicioParcelas", "11/2024");
        Alterar(vm, "fimParcelas", "02/2025");
        Alterar(vm, "valorMensal", "1.000,00");
        Gerar(parcelas);
        Assert.Equal(4, parcelas.Linhas.Count);
        Assert.All(parcelas.Linhas, linha => Assert.Equal("1.000,00", linha.Campo("valor").Valor));

        parcelas.AdicionarCommand.Execute(null);
        var decimo = parcelas.Linhas[4];
        decimo.Campo("tipo").Indice = 1;
        Assert.Equal("12/2025", decimo.Campo("competencia").Valor);
        decimo.Campo("competencia").Valor = "12/2024";
        decimo.Campo("valor").Valor = "800,00";
        parcelas.Linhas[1].Campo("valor").Valor = "1.200,00";

        Alterar(vm, "inicioParcelas", "12/2024");
        Gerar(parcelas);
        Assert.Equal(new[] { "11/2024", "12/2024", "12/2024", "01/2025", "02/2025" }, parcelas.Linhas.Select(linha => linha.Campo("competencia").Valor));
        Assert.Equal(new[] { 0, 0, 1, 0, 0 }, parcelas.Linhas.Select(linha => linha.Campo("tipo").Indice));
        Assert.Equal("1.200,00", parcelas.Linhas[1].Campo("valor").Valor);
        Assert.Contains("fora do período", parcelas.Aviso);
        parcelas.RemoverCommand.Execute(parcelas.Linhas[0]);

        parcelas.Linhas[0].Campo("valor").Valor = "abc";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Parcela 1: O campo \"Valor tributável (R$)\"", vm.Mensagem);
        parcelas.Linhas[0].Campo("valor").Valor = "1.200,00";

        Opcao(vm, "origem", 1);
        Assert.False(CampoDe(vm, "dependentes").Visivel);
        Assert.False(CampoDe(vm, "mesSecao").Visivel);
        Alterar(vm, "dataPagamento", "10/03/2026");
        Alterar(vm, "dependentes", "2");
        Alterar(vm, "outrosRendimentos", "500,00");
        Alterar(vm, "juros", "300,00");
        Opcao(vm, "reducao", 1);
        await vm.CalcularCommand.ExecuteAsync(null);
        var pagamento = captura.Pedido!.Pagamento;
        Assert.Equal(new DateOnly(2026, 3, 10), pagamento.DataPagamento);
        Assert.Equal(OrigemPagamentoRra.JusticaFederal, pagamento.Origem);
        Assert.Equal(
            new[]
            {
                new ParcelaRra(new DateOnly(2024, 12, 1), TipoParcelaRra.Mensal, 1200m),
                new ParcelaRra(new DateOnly(2024, 12, 1), TipoParcelaRra.DecimoTerceiro, 800m),
                new ParcelaRra(new DateOnly(2025, 1, 1), TipoParcelaRra.Mensal, 1000m),
                new ParcelaRra(new DateOnly(2025, 2, 1), TipoParcelaRra.Mensal, 1000m)
            },
            pagamento.Parcelas);
        Assert.Equal(300m, pagamento.JurosDeMora);
        Assert.Null(pagamento.TotalParcelasAnosAnteriores);
        Assert.False(pagamento.AplicarReducao);
        Assert.Equal(0m, pagamento.OutrosRendimentosMes);
        Assert.Equal(0, pagamento.Dependentes);

        captura.Limpar();
        parcelas.Linhas[2].Campo("competencia").Valor = "12/2024";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("12/2024 aparece mais de uma vez", vm.Mensagem);
    }

    [Fact]
    public async Task Simples_nacional_gera_janela_e_le_campos_conforme_atividade()
    {
        var captura = new Captura<SimularSimplesNacionalRequest>();
        var vm = new SimplesNacionalMobileViewModel(captura, new PdfTeste(), NullLogger<SimplesNacionalMobileViewModel>.Instance);
        var meses = Grade(vm, "meses");
        Assert.True(CampoDe(vm, "proLabore").Visivel);
        Assert.False(CampoDe(vm, "remuneracoes").Visivel);

        Alterar(vm, "periodo", "06/2026");
        Alterar(vm, "receitaMensal", "20.000,00");
        Alterar(vm, "folhaMensal", "5.000,00");
        Gerar(meses);
        var esperadas = CalculadoraSimplesNacional.Janela(new DateOnly(2026, 6, 1), null).Meses.Select(mes => mes.ToString("MM/yyyy", CultureInfo.InvariantCulture)).ToArray();
        Assert.NotEmpty(esperadas);
        Assert.Equal(esperadas, meses.Linhas.Select(linha => linha.Campo("competencia").Valor));
        Assert.All(meses.Linhas, linha => Assert.Equal("5.000,00", linha.Campo("folha").Valor));
        meses.Linhas[0].Campo("receita").Valor = "25.000,00";
        Gerar(meses);
        Assert.Equal("25.000,00", meses.Linhas[0].Campo("receita").Valor);
        Assert.Contains($"{esperadas.Length} preservada(s)", meses.Aviso);

        meses.Linhas[2].Campo("folha").Valor = "x";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Mês 3: O campo \"Folha com encargos (R$)\"", vm.Mensagem);
        meses.Linhas[2].Campo("folha").Valor = "5.000,00";

        Alterar(vm, "proLabore", "3.000,00");
        Alterar(vm, "dependentes", "1");
        Opcao(vm, "atividade", 2);
        Assert.False(CampoDe(vm, "proLabore").Visivel);
        Assert.True(CampoDe(vm, "remuneracoes").Visivel);
        Alterar(vm, "receitaMes", "30.000,00");
        Alterar(vm, "remuneracoes", "8.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido!;
        Assert.Equal(AtividadeSimples.ServicosAnexoIV, pedido.Entrada.Atividade);
        Assert.Equal(new DateOnly(2026, 6, 1), pedido.Entrada.PeriodoApuracao);
        Assert.Equal(30000m, pedido.Entrada.ReceitaMes);
        Assert.Equal(8000m, pedido.Entrada.RemuneracoesMes);
        Assert.Null(pedido.Entrada.InicioAtividade);
        Assert.Equal(esperadas.Length, pedido.Entrada.Meses.Count);
        Assert.Equal(25000m, pedido.Entrada.Meses[0].Receita);
        Assert.Equal(0m, pedido.ProLaboreAtual);
        Assert.Equal(0, pedido.Dependentes);

        Opcao(vm, "atividade", 0);
        Alterar(vm, "inicio", "01/2026");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(AtividadeSimples.ServicosFatorR, captura.Pedido!.Entrada.Atividade);
        Assert.Equal(new DateOnly(2026, 1, 1), captura.Pedido.Entrada.InicioAtividade);
        Assert.Equal(3000m, captura.Pedido.ProLaboreAtual);
        Assert.Equal(1, captura.Pedido.Dependentes);
        Assert.Equal(0m, captura.Pedido.Entrada.RemuneracoesMes);
    }

    private static GradeMobile Grade(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Grades, grade => grade.Chave == chave);

    private static void Gerar(GradeMobile grade) =>
        (grade.GerarCommand ?? throw new InvalidOperationException($"A grade {grade.Chave} não tem a ação de gerar.")).Execute(null);

    private static CampoMobile CampoDe(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave);

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) => CampoDe(vm, chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) => CampoDe(vm, chave).Indice = indice;

    private sealed class Captura<T> : ISimularDemonstrativoUseCase<T>
    {
        public T? Pedido { get; private set; }
        public void Limpar() => Pedido = default;
        public Task<Result<DemonstrativoDto>> ExecutarAsync(T request, CancellationToken cancellationToken)
        {
            Pedido = request;
            return Task.FromResult<Result<DemonstrativoDto>>(Erro.Validacao("Capturado para teste."));
        }
    }

    private sealed class PdfTeste : ICompartilhamentoPdf
    {
        public Task CompartilharResumoAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
