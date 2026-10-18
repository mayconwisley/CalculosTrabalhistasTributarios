namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class LinhaBeneficioFuncionarioViewModel
{
    public CampoTextoViewModel Descricao { get; } = new("Descrição do benefício", TipoCampo.Texto, "", "Identifique o benefício, com até 100 caracteres.");
    public CampoTextoViewModel Custo { get; } = new("Custo do benefício (R$)", TipoCampo.Moeda, "0,00");
    public CampoTextoViewModel Participacao { get; } = new("Participação do empregado (R$)", TipoCampo.Moeda, "0,00");
}
