using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Um recebimento mensal de trabalho de fonte situada no exterior por residente fiscal brasileiro.</summary>
public sealed record SimularTrabalhoExteriorRequest(
    DateOnly Recebimento,
    string PaisOrigem,
    string Moeda,
    EntradaTrabalhoExterior Valores,
    bool CompensacaoExteriorConfirmada,
    decimal PrevidenciaBrasil,
    int Dependentes,
    decimal PensaoPaga,
    decimal LivroCaixa);
