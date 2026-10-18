namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Agrupamento visual; os campos originais continuam sendo usados na validação e no histórico.</summary>
public sealed class CampoFormularioCustoFuncionarioViewModel(
    IReadOnlyList<CampoViewModel> remuneracao, IReadOnlyList<CampoViewModel> encargos,
    IReadOnlyList<CampoViewModel> planejamento, CampoBeneficiosFuncionarioViewModel beneficios) : CampoViewModel("Custo do funcionário", null)
{
    public IReadOnlyList<CampoViewModel> Remuneracao { get; } = remuneracao;
    public IReadOnlyList<CampoViewModel> Encargos { get; } = encargos;
    public IReadOnlyList<CampoViewModel> Planejamento { get; } = planejamento;
    public CampoBeneficiosFuncionarioViewModel Beneficios { get; } = beneficios;
}
