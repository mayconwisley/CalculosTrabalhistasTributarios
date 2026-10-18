namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;

public sealed record BeneficioFuncionario(string Descricao, decimal Custo, decimal ParticipacaoEmpregado)
{
    public decimal CustoLiquido => Custo - ParticipacaoEmpregado;
}

