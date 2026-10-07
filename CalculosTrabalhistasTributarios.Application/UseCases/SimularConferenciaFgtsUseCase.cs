using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Conferência dos depósitos do FGTS por competência: devido, informado e diferença, com os totais mensais e rescisórios
/// separados e o reflexo das diferenças na multa. É uma conferência local: não consulta o FGTS Digital nem o extrato.
/// </summary>
public sealed class SimularConferenciaFgtsUseCase : ISimularDemonstrativoUseCase<SimularConferenciaFgtsRequest>
{
    private const decimal MultaRescisoria = 40m;

    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularConferenciaFgtsRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = ConferenciaFgts.Calcular(request.Categoria, request.Lancamentos, request.Desligamento);
        return Task.FromResult(resultado.Falhou ? Result<DemonstrativoDto>.Falha(resultado.Erro) : Montar(resultado.Valor));
    }

    private static Result<DemonstrativoDto> Montar(ApuracaoConferenciaFgts f)
    {
        var mensais = f.Mensais.ToArray();
        var rescisorias = f.Rescisorias.ToArray();
        var faltaMensal = f.Falta(mensais);
        var faltaRescisoria = f.Falta(rescisorias);
        var excesso = f.Excesso(f.Linhas);
        var domestico = f.Categoria == CategoriaFgts.Domestico;
        var reflexoMulta = domestico ? 0m : Arredondar((faltaMensal + faltaRescisoria) * MultaRescisoria / 100m);
        var aliquota = Formato.PercentualCurto(f.Aliquota);

        var destaques = new List<DestaqueDto>
        {
            new("Falta depositar (mensal)", Formato.Moeda(faltaMensal), $"{mensais.Count(linha => linha.Diferenca > 0m)} competência(s) com diferença"),
            new("Falta depositar (rescisório)", rescisorias.Length == 0 ? "Sem rescisão" : Formato.Moeda(faltaRescisoria), rescisorias.Length == 0 ? "Nenhuma competência rescisória" : "Mês do desligamento"),
            new("FGTS devido", Formato.Moeda(f.Devido(f.Linhas)), $"{aliquota} sobre {Formato.Moeda(f.Linhas.Sum(linha => linha.Lancamento.Remuneracao))}"),
            domestico
                ? new("Indenização compensatória", Formato.Moeda(f.Compensatoria), "3,2% à parte, não conferida")
                : new("Reflexo na multa de 40%", Formato.Moeda(reflexoMulta), "Sobre o que falta depositar")
        };

        var proventos = new List<VerbaDto> { new("FGTS devido — competências mensais", $"{mensais.Length} competência(s) a {aliquota}", f.Devido(mensais)) };
        var descontos = new List<VerbaDto> { new("Depósitos informados — mensais", "Extrato ou guias", f.Informado(mensais)) };
        if (rescisorias.Length > 0)
        {
            proventos.Add(new("FGTS devido — rescisório", $"{rescisorias[0].Lancamento.Competencia:MM/yyyy}", f.Devido(rescisorias)));
            descontos.Add(new("Depósito informado — rescisório", "Extrato ou guia", f.Informado(rescisorias)));
        }
        var informativos = new List<VerbaDto>();
        if (excesso > 0m) informativos.Add(new("Depósitos acima do devido", "Não abatidos das diferenças", excesso));
        if (reflexoMulta > 0m) informativos.Add(new("Reflexo das diferenças na multa de 40%", "Dispensa sem justa causa", reflexoMulta));
        if (domestico) informativos.Add(new("Indenização compensatória do doméstico", "3,2% da remuneração", f.Compensatoria));

        var memoria = new List<GrupoMemoriaDto> { Grupo("Competências mensais", mensais, aliquota, faltaMensal) };
        if (rescisorias.Length > 0)
            memoria.Add(Grupo("Competência rescisória", rescisorias, aliquota, faltaRescisoria));
        if (reflexoMulta > 0m)
            memoria.Add(new GrupoMemoriaDto("Reflexo na multa", Formato.Moeda(reflexoMulta),
                [new("Multa de 40%", $"({Formato.Moeda(faltaMensal)} + {Formato.Moeda(faltaRescisoria)}) × 40% = {Formato.Moeda(reflexoMulta)}, se a dispensa for sem justa causa; 20% na culpa recíproca ou no acordo")]));

        var observacoes = new List<string>
        {
            $"O devido é a remuneração paga ou devida no mês × {aliquota} (Lei 8.036/1990, art. 15{(f.Categoria == CategoriaFgts.Aprendiz ? ", § 7º, para o aprendiz" : "")}{(domestico ? "; LC 150/2015, art. 34" : "")}). Entram salário, horas extras, adicionais, comissões, 13º no mês do pagamento e férias gozadas com o terço; não entram as parcelas do art. 28, § 9º, da Lei 8.212/1991, como férias indenizadas. Em afastamento por acidente do trabalho e no serviço militar, o depósito continua devido (art. 15, § 5º).",
            "Até a competência 02/2024, o depósito mensal vencia no dia 7 do mês seguinte; desde 03/2024, no FGTS Digital, vence no dia 20. Sem expediente bancário, o prazo é antecipado: o vencimento mostrado só considera sábados e domingos. O rescisório vence no 10º dia corrido após o desligamento.",
            "O FGTS Digital individualiza os débitos a partir das remunerações informadas no eSocial. Esta conferência usa os valores digitados e não consulta o extrato oficial; depósitos em atraso têm juros, multa e atualização (Lei 8.036/1990, art. 22) calculados pelo FGTS Digital, não incluídos aqui.",
            "As diferenças somam só o que falta em cada competência: um depósito a maior em um mês não quita outro, e aparece à parte."
        };
        if (domestico)
            observacoes.Add("No doméstico, os 3,2% da indenização compensatória são depositados à parte e substituem a multa de 40% (LC 150/2015, art. 22). O valor é mostrado como referência, sem comparação com o informado.");
        if (f.Desligamento is not null)
            observacoes.Add("Use Levar à rescisão para preencher o histórico de depósitos do FGTS da rescisão com o valor devido de cada competência anterior ao desligamento.");

        var depositos = mensais.Where(linha => f.Desligamento is not { } data || linha.Lancamento.Competencia < new DateOnly(data.Year, data.Month, 1))
            .Select(linha => new DepositoFgtsHistorico(linha.Lancamento.Competencia, linha.Devido)).ToArray();
        var periodo = f.Linhas.Count == 1 ? $"{f.Linhas[0].Lancamento.Competencia:MM/yyyy}" : $"{f.Linhas[0].Lancamento.Competencia:MM/yyyy} a {f.Linhas[^1].Lancamento.Competencia:MM/yyyy}";
        return new DemonstrativoDto("Conferência do FGTS", $"Competências {periodo} • alíquota de {aliquota}",
            destaques, proventos, descontos, informativos, memoria, observacoes,
            RotuloProventos: "Devido", RotuloResultado: "Diferença líquida (devido - informado)",
            DepositosFgts: depositos.Length > 0 ? new DepositosFgtsRescisaoDto(depositos, f.Desligamento) : null);
    }

    private static GrupoMemoriaDto Grupo(string titulo, IReadOnlyList<LinhaConferenciaFgts> linhas, string aliquota, decimal falta) =>
        new(titulo, $"Falta depositar: {Formato.Moeda(falta)}", linhas.Select(linha => new FormulaDto(
            $"{linha.Lancamento.Competencia:MM/yyyy}{(linha.Vencimento is { } vencimento ? $" • vence em {Formato.Data(vencimento)}" : "")}",
            $"{Formato.Moeda(linha.Lancamento.Remuneracao)} × {aliquota} = {Formato.Moeda(linha.Devido)}; informado {Formato.Moeda(linha.Lancamento.DepositoInformado)}; diferença {Formato.MoedaComSinal(linha.Diferenca)}")).ToArray());

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
