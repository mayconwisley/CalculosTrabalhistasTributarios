using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraInssMultiplosVinculos(ISimularDemonstrativoUseCase<SimularMultiplosVinculosInssRequest> simulador) : CalculadoraBase
{
    private readonly CampoVinculosInssViewModel _vinculos = new();

    public override string Titulo => "INSS em múltiplos vínculos";
    public override string Descricao => "Desconto por vínculo, com teto compartilhado e faixas progressivas.";
    public override string InstrucaoInicial => "Preencha a remuneração dos dois vínculos iniciais e selecione Calcular. Acrescente ou reordene vínculos quando necessário.";
    public override IReadOnlyList<CampoViewModel> Campos => [_vinculos.Competencia, _vinculos];
    public override string NomeArquivoPdf => $"inss-multiplos-vinculos-{_vinculos.Competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _vinculos.Competencia;

    public override Dictionary<string, string> ExportarCampos()
    {
        return new Dictionary<string, string>
        {
            [_vinculos.Competencia.Rotulo] = _vinculos.Competencia.Valor,
            [_vinculos.Rotulo] = _vinculos.Exportar()
        };
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.TryGetValue(_vinculos.Competencia.Rotulo, out var competencia))
            _vinculos.Competencia.Valor = competencia;
        if (!valores.TryGetValue(_vinculos.Rotulo, out var dados) || _vinculos.Importar(dados))
            return;
        var antigos = LerVinculos(dados);
        if (!antigos.Falhou)
            _vinculos.Preencher(antigos.Valor);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var leitor = new LeitorFormulario();
        var competencia = leitor.Competencia(_vinculos.Competencia);
        if (leitor.Erro is { } erro)
            return erro;
        var vinculos = _vinculos.Ler();
        if (vinculos.Falhou)
            return vinculos.Erro;
        return await simulador.ExecutarAsync(new SimularMultiplosVinculosInssRequest(competencia, vinculos.Valor), cancellationToken);
    }

    public static Result<IReadOnlyList<VinculoInss>> LerVinculos(string texto)
    {
        var vinculos = new List<VinculoInss>();
        var linhas = texto.Replace("\r", "").Split('\n');
        for (var indice = 0; indice < linhas.Length; indice++)
        {
            var linha = linhas[indice].Trim();
            if (linha.Length == 0)
                continue;
            var partes = linha.Split(';', StringSplitOptions.TrimEntries);
            if (partes.Length is < 2 or > 3 || !TentarTipo(partes[0], out var tipo)
                || !LeituraNumerica.TentarLer(partes[1], out var valor) || valor <= 0m
                || partes.Length == 3 && partes[2].Length == 0)
                return Erro.Validacao($"Revise a linha {indice + 1}: use categoria; remuneração; identificação opcional. Exemplo: Empregado; 2.000,00; Empresa A.");
            vinculos.Add(new VinculoInss(partes.Length == 3 ? partes[2] : $"Vínculo {vinculos.Count + 1}", tipo, valor));
        }
        return vinculos.Count >= 2
            ? vinculos
            : Erro.Validacao("Informe pelo menos dois vínculos, um por linha, na ordem de desconto.");
    }

    private static bool TentarTipo(string texto, out TipoVinculoInss tipo)
    {
        var categoria = texto.Trim().ToUpperInvariant();
        TipoVinculoInss? encontrado = categoria switch
        {
            "EMPREGADO" or "E" => TipoVinculoInss.Empregado,
            "DOMÉSTICO" or "DOMESTICO" or "D" => TipoVinculoInss.Domestico,
            "AVULSO" or "A" => TipoVinculoInss.Avulso,
            "INDIVIDUAL" or "AUTÔNOMO" or "AUTONOMO" or "CI" => TipoVinculoInss.ContribuinteIndividual,
            "EBAS" => TipoVinculoInss.ContribuinteIndividualEbas,
            _ => null
        };
        tipo = encontrado ?? default;
        return encontrado.HasValue;
    }
}
