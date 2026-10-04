namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Guarda os dados do último cálculo feito em qualquer janela, para que a próxima calculadora já abra com a mesma
/// competência, o mesmo salário e os mesmos dependentes.
/// </summary>
public sealed class ContextoCompartilhado
{
    public ContextoCalculo Atual { get; private set; } = new(null, null, null);

    /// <summary>Os valores ausentes no cálculo, como o salário em uma calculadora que não o usa, mantêm os anteriores.</summary>
    public void Registrar(ContextoCalculo contexto) =>
        Atual = new(contexto.Competencia ?? Atual.Competencia, contexto.Salario ?? Atual.Salario, contexto.Dependentes ?? Atual.Dependentes);
}
