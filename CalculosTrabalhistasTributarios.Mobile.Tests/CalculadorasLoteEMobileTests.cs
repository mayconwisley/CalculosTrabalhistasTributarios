using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteEMobileTests
{
    [Fact]
    public async Task Aposentadoria_le_grade_de_salarios_e_media_informada_conforme_a_origem()
    {
        var captura = new Captura<SimularAposentadoriaRequest>();
        var vm = new AposentadoriaMobileViewModel(captura, new PdfTeste(), NullLogger<AposentadoriaMobileViewModel>.Instance);
        Opcao(vm, "sexo", 1);
        Alterar(vm, "nascimento", "10/03/1970");
        Alterar(vm, "dataSimulacao", "15/06/2025");
        Alterar(vm, "anos", "30");
        Alterar(vm, "meses", "5");
        Alterar(vm, "anosReforma", "25");
        Alterar(vm, "mesesReforma", "3");
        Marcar(vm, "continua", false);
        Alterar(vm, "expectativa", "20,5");
        var grade = Assert.Single(vm.Grades, item => item.Chave == "salarios");
        Assert.True(grade.Visivel);
        Assert.False(Campo(vm, "mediaInformada").Visivel);

        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Equal("Adicione os períodos de salário de contribuição desde 07/1994, ou escolha informar a média pronta.", vm.Mensagem);

        grade.AdicionarCommand.Execute(null);
        Assert.Equal("07/1994", grade.Linhas[0].Campo("inicio").Valor);
        grade.Linhas[0].Campo("fim").Valor = "12/1999";
        grade.Linhas[0].Campo("salario").Valor = "1.000,00";
        grade.AdicionarCommand.Execute(null);
        Assert.Equal("01/2000", grade.Linhas[1].Campo("inicio").Valor);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Equal("Período 2: informe o início e o fim no formato mm/aaaa; para um só mês, repita a competência.", vm.Mensagem);

        grade.Linhas[1].Campo("fim").Valor = "05/2025";
        grade.Linhas[1].Campo("salario").Valor = "3.500,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = Assert.IsType<SimularAposentadoriaRequest>(captura.Pedido);
        Assert.Equal(SexoSegurado.Feminino, pedido.Sexo);
        Assert.Equal(new DateOnly(1970, 3, 10), pedido.Nascimento);
        Assert.Equal(new DateOnly(2025, 6, 15), pedido.DataSimulacao);
        Assert.Equal(365, pedido.MesesContribuicao);
        Assert.Equal(303, pedido.MesesNaReforma);
        Assert.False(pedido.ContinuaContribuindo);
        Assert.Equal(0m, pedido.Media);
        Assert.Equal(20.5m, pedido.ExpectativaSobrevida);
        Assert.NotNull(pedido.Salarios);
        Assert.Equal(new[]
        {
            new PeriodoSalarioContribuicao(new(1994, 7, 1), new(1999, 12, 1), 1000m),
            new PeriodoSalarioContribuicao(new(2000, 1, 1), new(2025, 5, 1), 3500m)
        }, pedido.Salarios.ToArray());

        Opcao(vm, "origemMedia", 1);
        Assert.False(grade.Visivel);
        Assert.True(Campo(vm, "mediaInformada").Visivel);
        Alterar(vm, "mediaInformada", "4.200,00");
        Alterar(vm, "expectativa", "");
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        pedido = Assert.IsType<SimularAposentadoriaRequest>(captura.Pedido);
        Assert.Equal(4200m, pedido.Media);
        Assert.Null(pedido.Salarios);
        Assert.Null(pedido.ExpectativaSobrevida);

        Alterar(vm, "mesesReforma", "12");
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("aceita de 0 a 11 meses", vm.Mensagem);
    }

    [Fact]
    public async Task Pensao_por_morte_usa_aposentadoria_ou_media_e_oculta_dados_do_conjuge()
    {
        var captura = new Captura<SimularPensaoMorteRequest>();
        var vm = new PensaoMorteMobileViewModel(captura, new PdfTeste(), NullLogger<PensaoMorteMobileViewModel>.Instance);
        var grade = Assert.Single(vm.Grades, item => item.Chave == "salarios");
        Assert.True(Campo(vm, "aposentadoria").Visivel);
        Assert.False(Campo(vm, "origemMedia").Visivel);
        Assert.False(Campo(vm, "mediaInformada").Visivel);
        Assert.False(grade.Visivel);
        Assert.False(Campo(vm, "anos").Visivel);

        Alterar(vm, "dataObito", "20/04/2025");
        Alterar(vm, "aposentadoria", "3.000,00");
        Opcao(vm, "causa", 2);
        Marcar(vm, "conjuge", false);
        Assert.False(Campo(vm, "idadeConjuge").Visivel);
        Assert.False(Campo(vm, "uniao").Visivel);
        Assert.False(Campo(vm, "beneficioConjuge").Visivel);
        Alterar(vm, "beneficioConjuge", "texto inválido");
        Alterar(vm, "outros", "2");
        Marcar(vm, "invalido", true);
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = Assert.IsType<SimularPensaoMorteRequest>(captura.Pedido);
        Assert.Equal(new DateOnly(2025, 4, 20), pedido.DataObito);
        Assert.Equal(SituacaoInstituidor.Aposentado, pedido.Situacao);
        Assert.Equal(3000m, pedido.ValorAposentadoria);
        Assert.Equal(0m, pedido.Media);
        Assert.Equal(180, pedido.MesesContribuicao);
        Assert.Equal(CausaObito.AcidenteTrabalho, pedido.Causa);
        Assert.False(pedido.HaConjuge);
        Assert.Equal(0, pedido.IdadeConjuge);
        Assert.False(pedido.UniaoDoisAnos);
        Assert.Equal(0m, pedido.BeneficioConjuge);
        Assert.Equal(2, pedido.OutrosDependentes);
        Assert.True(pedido.DependenteInvalido);
        Assert.Null(pedido.Salarios);

        Opcao(vm, "situacao", 1);
        Marcar(vm, "conjuge", true);
        Alterar(vm, "beneficioConjuge", "1.200,00");
        Alterar(vm, "idadeConjuge", "50");
        Opcao(vm, "sexo", 0);
        Alterar(vm, "anos", "18");
        Alterar(vm, "meses", "2");
        Assert.False(Campo(vm, "aposentadoria").Visivel);
        Assert.True(grade.Visivel);
        grade.AdicionarCommand.Execute(null);
        grade.Linhas[0].Campo("fim").Valor = "03/2025";
        grade.Linhas[0].Campo("salario").Valor = "abc";
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Equal("Período 1: Salário (R$): informe um número, por exemplo 3.500,00.", vm.Mensagem);

        grade.Linhas[0].Campo("salario").Valor = "2.800,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        pedido = Assert.IsType<SimularPensaoMorteRequest>(captura.Pedido);
        Assert.Equal(SituacaoInstituidor.NaoAposentado, pedido.Situacao);
        Assert.Equal(0m, pedido.ValorAposentadoria);
        Assert.Equal(218, pedido.MesesContribuicao);
        Assert.Equal(SexoSegurado.Masculino, pedido.SexoInstituidor);
        Assert.True(pedido.HaConjuge);
        Assert.Equal(50, pedido.IdadeConjuge);
        Assert.True(pedido.UniaoDoisAnos);
        Assert.Equal(1200m, pedido.BeneficioConjuge);
        Assert.NotNull(pedido.Salarios);
        var periodo = Assert.Single(pedido.Salarios);
        Assert.Equal(new PeriodoSalarioContribuicao(new(1994, 7, 1), new(2025, 3, 1), 2800m), periodo);

        Opcao(vm, "origemMedia", 1);
        Alterar(vm, "mediaInformada", "3.100,00");
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        pedido = Assert.IsType<SimularPensaoMorteRequest>(captura.Pedido);
        Assert.Equal(3100m, pedido.Media);
        Assert.Null(pedido.Salarios);
    }

    [Fact]
    public async Task Trabalho_exterior_sugere_moeda_do_pais_e_le_cotacoes_digitadas()
    {
        var captura = new Captura<SimularTrabalhoExteriorRequest>();
        var vm = new TrabalhoExteriorMobileViewModel(captura, new PdfTeste(), NullLogger<TrabalhoExteriorMobileViewModel>.Instance);
        Assert.Equal("USD", Campo(vm, "moeda").Valor);
        Assert.Equal("1", Campo(vm, "usdPorUnidade").Valor);
        Assert.False(Campo(vm, "outroPais").Visivel);
        Assert.False(Campo(vm, "livroCaixa").Visivel);

        Opcao(vm, "pais", 1);
        Assert.Equal("EUR", Campo(vm, "moeda").Valor);
        Assert.Equal("0", Campo(vm, "usdPorUnidade").Valor);
        Opcao(vm, "pais", 30);
        Assert.True(Campo(vm, "outroPais").Visivel);
        Alterar(vm, "outroPais", "Noruega");
        Alterar(vm, "moeda", "NOK");
        Opcao(vm, "vinculo", 1);
        Assert.True(Campo(vm, "livroCaixa").Visivel);
        Alterar(vm, "recebimento", "10/03/2025");
        Alterar(vm, "remuneracao", "50.000,00");
        Alterar(vm, "jurosRecebidos", "100,00");
        Alterar(vm, "impostoExterior", "5.000,00");
        Alterar(vm, "usdPorUnidade", "0,094512");
        Alterar(vm, "dolarFiscal", "5,7843");
        Alterar(vm, "cambioEfetivo", "0,5321");
        Alterar(vm, "taxaMoeda", "150,00");
        Alterar(vm, "jurosBanco", "10,00");
        Alterar(vm, "taxaReais", "35,00");
        Opcao(vm, "creditoExterior", 1);
        Alterar(vm, "previdencia", "300,00");
        Alterar(vm, "dependentes", "2");
        Alterar(vm, "pensao", "800,00");
        Alterar(vm, "livroCaixa", "1.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = Assert.IsType<SimularTrabalhoExteriorRequest>(captura.Pedido);
        Assert.Equal(new DateOnly(2025, 3, 10), pedido.Recebimento);
        Assert.Equal("Noruega", pedido.PaisOrigem);
        Assert.Equal("NOK", pedido.Moeda);
        Assert.Equal(new EntradaTrabalhoExterior(VinculoTrabalhoExterior.ServicoPessoaFisica, 50000m, 100m, 5000m, 150m, 10m, 35m,
            0.094512m, 5.7843m, 0.5321m), pedido.Valores);
        Assert.True(pedido.CompensacaoExteriorConfirmada);
        Assert.Equal(300m, pedido.PrevidenciaBrasil);
        Assert.Equal(2, pedido.Dependentes);
        Assert.Equal(800m, pedido.PensaoPaga);
        Assert.Equal(1000m, pedido.LivroCaixa);

        Opcao(vm, "pais", 12);
        Opcao(vm, "vinculo", 0);
        Alterar(vm, "livroCaixa", "inválido");
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        pedido = Assert.IsType<SimularTrabalhoExteriorRequest>(captura.Pedido);
        Assert.Equal("Reino Unido", pedido.PaisOrigem);
        Assert.Equal("GBP", pedido.Moeda);
        Assert.Equal(0m, pedido.LivroCaixa);
        Assert.Equal(0m, pedido.Valores.CambioEfetivoReais);

        Alterar(vm, "dolarFiscal", "abc");
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Equal("Dólar compra fiscal (R$/USD): informe um número, por exemplo 3.500,00.", vm.Mensagem);
    }

    private static CampoMobile Campo(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave);

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) => Campo(vm, chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) => Campo(vm, chave).Indice = indice;

    private static void Marcar(CalculadoraTrabalhistaViewModel vm, string chave, bool marcado) => Campo(vm, chave).Marcado = marcado;

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
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
