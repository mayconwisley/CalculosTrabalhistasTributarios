namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IArquivoDialogService
{
    string? SolicitarDestinoPdf(string nomeArquivoSugerido);

    /// <summary>Destino da planilha do Excel; a extensão do nome sugerido é trocada por .xlsx.</summary>
    string? SolicitarDestinoPlanilha(string nomeArquivoSugerido);
}
