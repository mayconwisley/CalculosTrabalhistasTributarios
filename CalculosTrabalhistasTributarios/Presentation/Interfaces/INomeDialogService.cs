using CalculosTrabalhistasTributarios.Presentation.Services;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface INomeDialogService
{
    /// <summary>Nome informado, sem espaços nas pontas, ou nulo se o usuário cancelar.</summary>
    /// <param name="acao">Texto do botão de confirmação, como "Salvar" ou "Renomear".</param>
    /// <param name="oferecerComoNovo">Mostra a opção de salvar como um cálculo novo, para quem abriu um cálculo do histórico.</param>
    NomeEscolhido? SolicitarNome(string titulo, string acao, string nomeAtual, bool oferecerComoNovo = false);
}
