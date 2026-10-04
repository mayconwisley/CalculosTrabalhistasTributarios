namespace CalculosTrabalhistasTributarios.Tests;

internal static class Texto
{
    /// <summary>O formato de moeda pt-BR separa "R$" do valor com espaço não separável; os testes comparam com espaço comum.</summary>
    public static string Normalizar(string texto) => texto.Replace(' ', ' ').Replace(' ', ' ');
}
