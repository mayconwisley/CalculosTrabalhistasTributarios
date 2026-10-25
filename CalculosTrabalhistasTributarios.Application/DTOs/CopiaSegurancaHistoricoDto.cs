namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>
/// Conteúdo do arquivo de cópia de segurança do histórico. <paramref name="Origem"/> identifica o aplicativo que gerou a
/// cópia, porque os formulários do desktop e do celular têm formatos diferentes e só reabrem no aplicativo de origem.
/// </summary>
public sealed record CopiaSegurancaHistoricoDto(string Formato, int Versao, string Origem, DateTime GeradaEm,
    IReadOnlyList<CalculoCopiaSegurancaDto> Calculos);
