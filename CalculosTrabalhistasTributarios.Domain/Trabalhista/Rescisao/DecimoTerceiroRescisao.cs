namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>O 13º proporcional da rescisão e a parte dele que vem da projeção do aviso.</summary>
/// <param name="Avos">Meses do ano com 15 dias ou mais de trabalho até o desligamento.</param>
/// <param name="AvosAviso">Os meses a mais pela projeção do aviso indenizado.</param>
/// <param name="Total">O 13º devido; arredondado uma vez, para a memória fechar com o valor pago.</param>
/// <param name="SobreAviso">A diferença entre o total e o proporcional até o desligamento.</param>
public sealed record DecimoTerceiroRescisao(int Avos, int AvosAviso, decimal Total, decimal Proporcional, decimal SobreAviso);
