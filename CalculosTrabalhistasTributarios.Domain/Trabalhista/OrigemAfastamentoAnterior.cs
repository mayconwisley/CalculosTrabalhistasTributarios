namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Quem pagou o afastamento anterior: só a empresa, nos até 15 primeiros dias, ou um benefício do INSS já cessado.</summary>
public enum OrigemAfastamentoAnterior { Empresa, BeneficioInss }
