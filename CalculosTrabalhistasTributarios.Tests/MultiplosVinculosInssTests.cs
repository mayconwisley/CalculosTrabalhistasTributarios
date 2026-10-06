using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class MultiplosVinculosInssTests
{
    private static readonly TabelasDaCompetencia Tabela2020 = new(new DateOnly(2020, 5, 1),
        new PerfilTributario(
            [new(1, 1045m, 7.5m), new(2, 2089.60m, 9m), new(3, 3134.40m, 12m), new(4, 6101.06m, 14m)],
            [new(1, 10000m, 0m)], 0m, 0m, 0m, []));

    [Fact]
    public void Empregos_abaixo_do_teto_usam_faixas_progressivas_na_ordem_oficial_do_esocial()
    {
        var resultado = CalculadoraMultiplosVinculosInss.Calcular(Tabela2020,
            [new("A", TipoVinculoInss.Empregado, 2000m), new("B", TipoVinculoInss.Domestico, 1500m),
             new("C", TipoVinculoInss.Avulso, 1000m), new("D", TipoVinculoInss.Empregado, 1000m)]).Sucesso();

        Assert.Equal([164.32m, 184.61m, 140m, 140m], resultado.Vinculos.Select(item => item.Contribuicao));
        Assert.Equal(628.93m, resultado.ContribuicaoTotal);
        Assert.Equal(5500m, resultado.BaseTotalTributada);
    }

    [Fact]
    public void Vinculo_que_alcanca_teto_tributa_so_o_residuo_e_os_seguintes_nao_descontam()
    {
        var resultado = CalculadoraMultiplosVinculosInss.Calcular(Tabela2020,
            [new("A", TipoVinculoInss.Empregado, 2000m), new("B", TipoVinculoInss.Empregado, 1500m),
             new("C", TipoVinculoInss.Empregado, 3500m), new("D", TipoVinculoInss.Empregado, 1000m)]).Sucesso();

        Assert.Equal([164.32m, 184.61m, 364.14m, 0m], resultado.Vinculos.Select(item => item.Contribuicao));
        Assert.Equal(2601.06m, resultado.Vinculos[2].BaseTributada);
        Assert.Equal(6101.06m, resultado.BaseTotalTributada);
        Assert.Equal(1898.94m, resultado.RemuneracaoAcimaDoTeto);
    }

    [Fact]
    public void Contribuinte_individual_ocupa_teto_sem_avancar_faixa_progressiva_dos_empregos()
    {
        var resultado = CalculadoraMultiplosVinculosInss.Calcular(Tabela2020,
            [new("A", TipoVinculoInss.Empregado, 1000m), new("B empregado", TipoVinculoInss.Empregado, 1000m),
             new("B individual", TipoVinculoInss.ContribuinteIndividual, 2000m),
             new("C", TipoVinculoInss.Empregado, 4000m)]).Sucesso();

        Assert.Equal([75m, 89.32m, 220m, 268.76m], resultado.Vinculos.Select(item => item.Contribuicao));
        Assert.Equal(2000m, resultado.Vinculos[3].BaseProgressivaAnterior);
        Assert.Equal(2101.06m, resultado.Vinculos[3].BaseTributada);
    }

    [Fact]
    public void Individual_e_ebas_usam_aliquotas_proprias_sobre_base_residual()
    {
        var resultado = CalculadoraMultiplosVinculosInss.Calcular(Tabela2020,
            [new("A", TipoVinculoInss.Empregado, 2000m), new("B", TipoVinculoInss.ContribuinteIndividual, 2000m),
             new("C", TipoVinculoInss.ContribuinteIndividualEbas, 3000m)]).Sucesso();

        Assert.Equal([164.32m, 220m, 420.21m], resultado.Vinculos.Select(item => item.Contribuicao));
        Assert.Equal(2101.06m, resultado.Vinculos[2].BaseTributada);
    }

    [Fact]
    public void Validacao_impede_resultados_indevidos()
    {
        var dois = new[] { new VinculoInss("A", TipoVinculoInss.Empregado, 1000m), new VinculoInss("B", TipoVinculoInss.Empregado, 1000m) };
        Assert.True(CalculadoraMultiplosVinculosInss.Calcular(Tabela2020, dois[..1]).Falhou);
        Assert.True(CalculadoraMultiplosVinculosInss.Calcular(Tabela2020, [dois[0], dois[1] with { Remuneracao = -1m }]).Falhou);
        Assert.True(CalculadoraMultiplosVinculosInss.Calcular(Tabela2020, [dois[0], dois[1] with { Remuneracao = 1.001m }]).Falhou);
        Assert.True(CalculadoraMultiplosVinculosInss.Calcular(Tabela2020, [dois[0], dois[1] with { Tipo = (TipoVinculoInss)99 }]).Falhou);
        var tabelaAntiga = new TabelasDaCompetencia(new DateOnly(2020, 2, 1), new PerfilTributario(
            [new(1, 6101.06m, 11m)], [new(1, 10000m, 0m)], 0m, 0m, 0m, []));
        Assert.True(CalculadoraMultiplosVinculosInss.Calcular(tabelaAntiga, dois).Falhou);
    }

    [Fact]
    public void Formulario_aceita_linhas_de_categorias_e_valores_em_reais()
    {
        var resultado = CalculadoraInssMultiplosVinculos.LerVinculos("Empregado; 2.000,00; Empresa A\r\nCI; 1.500,00; Tomador B\nDoméstico; 500,00").Sucesso();

        Assert.Equal(3, resultado.Count);
        Assert.Equal(TipoVinculoInss.ContribuinteIndividual, resultado[1].Tipo);
        Assert.Equal(1500m, resultado[1].Remuneracao);
        Assert.Equal("Vínculo 3", resultado[2].Identificacao);
        Assert.True(CalculadoraInssMultiplosVinculos.LerVinculos("Empregado; 2000").Falhou);
        Assert.True(CalculadoraInssMultiplosVinculos.LerVinculos("Empregado; 2000\nRPPS; 2000").Falhou);
    }

    [Fact]
    public void Formulario_visual_permite_adicionar_reordenar_remover_e_restaurar_vinculos()
    {
        var campo = new CampoVinculosInssViewModel();
        Assert.Equal(2, campo.Linhas.Count);
        campo.Linhas[0].Remuneracao = "2.000,00";
        campo.Linhas[0].Identificacao = "Empresa A";
        campo.Linhas[1].Remuneracao = "1.500,00";
        campo.Linhas[1].Categoria = VinculoInssLinhaViewModel.Categorias[3];
        campo.AdicionarCommand.Execute(null);
        campo.Linhas[2].Remuneracao = "600,00";
        campo.SubirCommand.Execute(campo.Linhas[2]);

        var vinculos = campo.Ler().Sucesso();
        Assert.Equal(["Empresa A", "Vínculo 2", "Vínculo 3"], vinculos.Select(item => item.Identificacao));
        Assert.Equal([2000m, 600m, 1500m], vinculos.Select(item => item.Remuneracao));
        Assert.Equal(TipoVinculoInss.ContribuinteIndividual, vinculos[2].Tipo);
        Assert.Equal([1, 2, 3], campo.Linhas.Select(linha => linha.Numero));

        var restaurado = new CampoVinculosInssViewModel();
        Assert.True(restaurado.Importar(campo.Exportar()));
        Assert.Equal(vinculos, restaurado.Ler().Sucesso());
        restaurado.RemoverCommand.Execute(restaurado.Linhas[1]);
        Assert.Equal(2, restaurado.Linhas.Count);
        restaurado.RemoverCommand.Execute(restaurado.Linhas[0]);
        Assert.Equal(2, restaurado.Linhas.Count);
        restaurado.Linhas[0].Remuneracao = "0,00";
        Assert.True(restaurado.Ler().Falhou);
    }

    [Fact]
    public async Task Historico_antigo_em_texto_reabre_no_novo_formulario_visual()
    {
        var calculadora = new CalculadoraInssMultiplosVinculos(new SimularMultiplosVinculosInssUseCase(await Ambiente.ConsultaAsync()));
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Competência"] = "10/2026",
            ["Vínculos em ordem de desconto"] = "Empregado; 2.000,00; Empresa A\nCI; 1.500,00; Tomador B"
        });

        var campo = calculadora.Campos.OfType<CampoVinculosInssViewModel>().Single();
        Assert.Equal(2, campo.Linhas.Count);
        Assert.Equal("Empresa A", campo.Linhas[0].Identificacao);
        Assert.Equal(TipoVinculoInss.ContribuinteIndividual, campo.Ler().Sucesso()[1].Tipo);
        var salvo = calculadora.ExportarCampos();
        Assert.StartsWith("[", salvo["Vínculos em ordem de desconto"]);
        Assert.True((await calculadora.CalcularAsync(default)).Sucesso().TotalDescontos > 0m);
    }

    [Fact]
    public async Task Demonstrativo_mostra_desconto_por_vinculo_e_nao_chama_resultado_de_liquido()
    {
        var request = new SimularMultiplosVinculosInssRequest(new DateOnly(2026, 10, 1),
            [new("A", TipoVinculoInss.Empregado, 3000m), new("B", TipoVinculoInss.ContribuinteIndividual, 2000m)]);
        var resultado = (await new SimularMultiplosVinculosInssUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default)).Sucesso();

        Assert.Equal(2, resultado.Descontos.Count);
        Assert.Equal(resultado.Descontos.Sum(item => item.Valor), resultado.TotalDescontos);
        Assert.Equal(5000m - resultado.TotalDescontos, resultado.Resultado);
        Assert.Contains("antes do IRRF", resultado.RotuloResultado);
        Assert.Equal(2, resultado.Memoria.Count);
    }
}
