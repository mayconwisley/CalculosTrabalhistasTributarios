using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularMultiplosVinculosInssUseCase(ITributacaoConsulta consulta) : ISimularDemonstrativoUseCase<SimularMultiplosVinculosInssRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularMultiplosVinculosInssRequest request, CancellationToken cancellationToken)
    {
        if (request.Vinculos is null || request.Vinculos.Count < 2)
            return Erro.Validacao("Informe pelo menos dois vínculos da mesma competência, na ordem de desconto.");
        if (request.Competencia < new DateOnly(2020, 3, 1))
            return Erro.Validacao("A apuração de múltiplos vínculos está disponível a partir de 03/2020.");

        var consultaTabelas = await consulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var apuracao = CalculadoraMultiplosVinculosInss.Calcular(consultaTabelas.Valor, request.Vinculos);
        if (apuracao.Falhou)
            return apuracao.Erro;
        var calculo = apuracao.Valor;

        var proventos = calculo.Vinculos.Select((item, indice) => new VerbaDto($"{indice + 1}º vínculo: {item.Vinculo.Identificacao}", NomeTipo(item.Vinculo.Tipo), item.Vinculo.Remuneracao)).ToArray();
        var descontos = calculo.Vinculos.Select((item, indice) => new VerbaDto($"INSS do {indice + 1}º vínculo: {item.Vinculo.Identificacao}", NomeTipo(item.Vinculo.Tipo), item.Contribuicao)).ToArray();
        var memoria = calculo.Vinculos.Select((item, indice) => MemoriaVinculo(item, indice, calculo.Teto)).ToArray();
        var observacoes = new List<string>
        {
            "Os vínculos devem estar na ordem combinada para o desconto. Cada fonte pagadora recebe as remunerações dos vínculos anteriores na informação de múltiplos vínculos do eSocial.",
            "Empregado, doméstico e avulso usam as faixas progressivas em conjunto. A remuneração como contribuinte individual ocupa o teto mensal, mas não altera a faixa progressiva dos vínculos de empregado.",
            "O contribuinte individual com retenção pela empresa usa 11% da base disponível. A opção EBAS usa 20% somente quando o tomador é entidade beneficente de assistência social nessa condição.",
            "Este demonstrativo calcula somente o INSS mensal do segurado. O IRRF é apurado por cada fonte pagadora, e o 13º tem apuração previdenciária separada. Recolhimento por conta própria em GPS exige cálculo próprio sobre a base residual.",
            "Não inclua remunerações vinculadas a regime próprio de previdência (RPPS): elas não se comunicam com o teto do RGPS."
        };
        if (calculo.RemuneracaoAcimaDoTeto > 0m)
            observacoes.Add($"{Formato.Moeda(calculo.RemuneracaoAcimaDoTeto)} ficaram acima do teto de {Formato.Moeda(calculo.Teto)} e não geraram contribuição adicional do segurado.");

        return new DemonstrativoDto(
            "INSS em múltiplos vínculos",
            $"Competência {Formato.Competencia(request.Competencia)}",
            [
                new("INSS total do segurado", Formato.Moeda(calculo.ContribuicaoTotal), $"Distribuído entre {calculo.Vinculos.Count} vínculos"),
                new("Base sujeita ao INSS", Formato.Moeda(calculo.BaseTotalTributada), $"Teto: {Formato.Moeda(calculo.Teto)}"),
                new("Acima do teto", Formato.Moeda(calculo.RemuneracaoAcimaDoTeto), "Sem novo desconto previdenciário"),
                new("Após INSS", Formato.Moeda(calculo.RemuneracaoTotal - calculo.ContribuicaoTotal), "Antes de IRRF e outros descontos")
            ],
            proventos,
            descontos,
            [],
            memoria,
            observacoes,
            RotuloProventos: "Remuneração",
            RotuloResultado: "Após INSS (antes do IRRF)");
    }

    private static GrupoMemoriaDto MemoriaVinculo(ContribuicaoVinculoInss item, int indice, decimal teto)
    {
        var formulas = new List<FormulaDto>
        {
            new("Teto disponível", $"{Formato.Moeda(teto)} - {Formato.Moeda(item.BaseAnteriorNoTeto)} (base já ocupada) = {Formato.Moeda(teto - item.BaseAnteriorNoTeto)}"),
            new("Base tributada", $"Menor entre {Formato.Moeda(item.Vinculo.Remuneracao)} e {Formato.Moeda(teto - item.BaseAnteriorNoTeto)} = {Formato.Moeda(item.BaseTributada)}")
        };
        if (item.Faixas.Count > 0)
        {
            formulas.Add(new("Faixas anteriores de empregado", Formato.Moeda(item.BaseProgressivaAnterior)));
            formulas.AddRange(item.Faixas.Select(faixa => new FormulaDto($"Faixa {faixa.Faixa}",
                $"{Formato.Moeda(faixa.BaseCalculada)} x {Formato.PercentualCurto(faixa.Aliquota)} = {Formato.Moeda(faixa.Imposto)}")));
        }
        else if (item.BaseTributada > 0m)
        {
            var aliquota = item.Vinculo.Tipo == TipoVinculoInss.ContribuinteIndividualEbas ? 20m : 11m;
            formulas.Add(new("Alíquota do contribuinte individual", $"{Formato.Moeda(item.BaseTributada)} x {Formato.PercentualCurto(aliquota)} = {Formato.Moeda(item.Contribuicao)}"));
        }
        formulas.Add(new("Contribuição deste vínculo", Formato.Moeda(item.Contribuicao)));
        return new GrupoMemoriaDto($"{indice + 1}º vínculo: {item.Vinculo.Identificacao}",
            $"INSS: {Formato.Moeda(item.Contribuicao)}", formulas);
    }

    private static string NomeTipo(TipoVinculoInss tipo) => tipo switch
    {
        TipoVinculoInss.Empregado => "Empregado",
        TipoVinculoInss.Domestico => "Doméstico",
        TipoVinculoInss.Avulso => "Avulso",
        TipoVinculoInss.ContribuinteIndividual => "Individual 11%",
        TipoVinculoInss.ContribuinteIndividualEbas => "Individual EBAS 20%",
        _ => "Categoria inválida"
    };
}
