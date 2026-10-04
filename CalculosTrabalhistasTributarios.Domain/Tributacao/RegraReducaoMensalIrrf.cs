namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Regra de redução aplicada ao IRRF já apurado pela tabela progressiva.</summary>
public sealed record RegraReducaoMensalIrrf(int Faixa, decimal LimiteRendimentos, decimal Multiplicador, decimal ValorBase);
