using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Campos da pensão alimentícia descontada no cálculo: a forma definida na decisão, o percentual e o valor informado.</summary>
public sealed class CamposPensao
{
    /// <summary>Opção "Não há", que não corresponde a nenhuma <see cref="BasePensao"/>.</summary>
    public static readonly object SemPensao = new();

    /// <summary>Pensão descontada de uma verba: não há, percentual do líquido ou do bruto, ou valor informado.</summary>
    public CamposPensao(string dicaValor)
        : this("Pensão alimentícia", "Percentual da pensão", "Valor da pensão", dicaValor,
            [
                new("Não há", SemPensao),
                new("% do líquido", BasePensao.RendimentosLiquidos),
                new("% do bruto", BasePensao.RendimentosBrutos),
                new("Valor informado", BasePensao.ValorFixo)
            ])
    {
    }

    private CamposPensao(string rotuloForma, string rotuloPercentual, string rotuloValor, string dicaValor, IReadOnlyList<OpcaoCampo> formas)
    {
        Forma = new(rotuloForma, formas, "Como a decisão ou o acordo define a pensão. Líquido: os rendimentos menos o INSS e o IRRF.");
        Percentual = new(rotuloPercentual, TipoCampo.Numero, "0", "Percentual definido na decisão ou no acordo, de 0 a 100; sobre o salário mínimo, até 1.000 (150 para 1,5 salário mínimo).");
        Valor = new(rotuloValor, TipoCampo.Moeda, "0,00", dicaValor);
        Forma.AoAlterar = Ajustar;
        Ajustar();
    }

    /// <summary>Uma das pensões comparadas na revisão, como "atual" ou "proposta", com todas as bases da calculadora de pensão.</summary>
    public static CamposPensao Cenario(string nome, string adjetivo) => new($"Pensão {nome}", $"Percentual {adjetivo}", $"Valor {adjetivo}", $"Valor mensal da pensão {nome}.",
        [
            new("% do líquido", BasePensao.RendimentosLiquidos),
            new("% do bruto", BasePensao.RendimentosBrutos),
            new("% do salário mínimo", BasePensao.SalarioMinimo),
            new("Valor fixo", BasePensao.ValorFixo)
        ]);

    public CampoOpcaoViewModel Forma { get; }
    public CampoTextoViewModel Percentual { get; }
    public CampoTextoViewModel Valor { get; }
    public IEnumerable<CampoViewModel> Campos => [Forma, Percentual, Valor];

    private void Ajustar()
    {
        var forma = Forma.Selecionada.Valor as BasePensao?;
        Percentual.Visivel = forma is BasePensao.RendimentosLiquidos or BasePensao.RendimentosBrutos or BasePensao.SalarioMinimo;
        Valor.Visivel = forma == BasePensao.ValorFixo;
    }
}
