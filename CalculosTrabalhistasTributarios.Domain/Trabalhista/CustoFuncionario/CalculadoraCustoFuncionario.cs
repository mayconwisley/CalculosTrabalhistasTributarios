using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;

public static class CalculadoraCustoFuncionario
{
    public const decimal LimiteMonetario = 100_000_000m;
    public const int LimiteBeneficios = 30;

    public static Result<ResultadoCustoFuncionario> Calcular(EntradaCustoFuncionario entrada)
    {
        var validacao = Validar(entrada);
        if (validacao.Falhou) return validacao.Erro;
        var domestico = entrada.Regime == RegimeTributario.EmpregadorDomestico;
        var patronal = domestico ? 8m : entrada.Regime == RegimeTributario.SimplesNacional ? 0m : 20m;
        var rat = domestico ? 0.8m : entrada.Regime == RegimeTributario.SimplesNacional ? 0m : entrada.Rat * entrada.Fap;
        var terceiros = entrada.Regime == RegimeTributario.LucroRealOuPresumido ? entrada.Terceiros : 0m;
        // Lei 8.036/1990, art. 15, § 7º; LC 150/2015, art. 34.
        var fgts = entrada.Aprendiz ? 2m : 8m;
        var compensatoria = domestico ? 3.2m : 0m;
        var encargos = patronal + rat + terceiros + fgts + compensatoria;
        var valorPatronal = Percentual(entrada.Salario, patronal);
        var valorRat = Percentual(entrada.Salario, rat);
        var valorTerceiros = Percentual(entrada.Salario, terceiros);
        var valorFgts = Percentual(entrada.Salario, fgts);
        var valorCompensatoria = Percentual(entrada.Salario, compensatoria);
        var beneficios = entrada.Beneficios + (entrada.BeneficiosDetalhados?.Sum(item => item.CustoLiquido) ?? 0m);
        var salarioComEncargos = entrada.Salario + valorPatronal + valorRat + valorTerceiros + valorFgts + valorCompensatoria;
        var provisao13 = entrada.IncluirProvisoes ? Arredondar(entrada.Salario / 12m) : 0m;
        var provisaoFerias = entrada.IncluirProvisoes ? Arredondar(entrada.Salario / 9m) : 0m;
        var encargosProvisoes = Percentual(provisao13 + provisaoFerias, encargos);
        var reserva = provisao13 + provisaoFerias + encargosProvisoes;
        var recorrente = salarioComEncargos + beneficios;
        var total = recorrente + reserva;
        // Preserva a política de provisões mensais arredondadas. O salário das férias já está na reserva:
        // retirá-lo uma vez evita contar 13 salários ordinários no ano (CLT, arts. 129 e 142).
        var anual = entrada.IncluirProvisoes ? total * 12m - salarioComEncargos : total * 12m;
        var media = anual / 12m;
        return new ResultadoCustoFuncionario(patronal, rat, terceiros, fgts, compensatoria,
            valorPatronal, valorRat, valorTerceiros, valorFgts, valorCompensatoria,
            provisao13, provisaoFerias, encargosProvisoes, beneficios, recorrente, reserva, total, anual, media,
            entrada.Salario == 0m ? null : (media - entrada.Salario) / entrada.Salario * 100m, media / entrada.HorasMensais);
    }

    private static Result Validar(EntradaCustoFuncionario e)
    {
        if (!Enum.IsDefined(e.Regime)) return Erro.Validacao("Regime da empresa inválido. Selecione um dos regimes disponíveis.");
        if (!MoedaValida(e.Salario)) return Erro.Validacao("O salário deve estar entre 0 e 100.000.000,00, com até duas casas decimais.");
        if (!MoedaValida(e.Beneficios)) return Erro.Validacao("Os benefícios devem estar entre 0 e 100.000.000,00, com até duas casas decimais.");
        if (e.HorasMensais is <= 0m or > 744m || decimal.Round(e.HorasMensais, 2) != e.HorasMensais)
            return Erro.Validacao("O divisor mensal deve ser maior que zero e até 744 horas, com até duas casas decimais.");
        if (e.Regime == RegimeTributario.EmpregadorDomestico && e.Aprendiz)
            return Erro.Validacao("Jovem aprendiz não se aplica ao regime de empregador doméstico. Revise o regime e o contrato.");
        if (e.Regime is RegimeTributario.LucroRealOuPresumido or RegimeTributario.SimplesNacionalAnexoIV)
        {
            if (e.Rat is not (1m or 2m or 3m)) return Erro.Validacao("O RAT deve ser 1%, 2% ou 3%, conforme o grau de risco da atividade.");
            if (e.Fap is < 0.5m or > 2m || decimal.Round(e.Fap, 4) != e.Fap)
                return Erro.Validacao("O FAP deve estar entre 0,5 e 2, com até quatro casas decimais.");
        }
        if (e.Regime == RegimeTributario.LucroRealOuPresumido && (e.Terceiros is < 0m or > 100m || decimal.Round(e.Terceiros, 4) != e.Terceiros))
            return Erro.Validacao("Terceiros deve estar entre 0% e 100%, com até quatro casas decimais; confirme a alíquota com a contabilidade.");
        if (e.BeneficiosDetalhados is { } itens)
        {
            if (itens.Count > LimiteBeneficios) return Erro.Validacao("Benefícios: informe no máximo 30 linhas.");
            for (var i = 0; i < itens.Count; i++)
            {
                var item = itens[i];
                if (item is null || string.IsNullOrWhiteSpace(item.Descricao) || item.Descricao.Length > 100)
                    return Erro.Validacao($"Benefício {i + 1}: informe uma descrição de até 100 caracteres.");
                if (!MoedaValida(item.Custo) || !MoedaValida(item.ParticipacaoEmpregado) || item.ParticipacaoEmpregado > item.Custo)
                    return Erro.Validacao($"Benefício {i + 1}: informe custo e participação de 0 a 100.000.000,00, com até duas casas decimais; a participação não pode superar o custo.");
            }
        }
        return Result.Ok();
    }

    private static bool MoedaValida(decimal valor) => valor is >= 0m and <= LimiteMonetario && decimal.Round(valor, 2) == valor;
    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
    private static decimal Percentual(decimal valor, decimal aliquota) => Arredondar(valor * aliquota / 100m);
}
