using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Taxa legal de cada mês pela série 29543 do Sistema Gerenciador de Séries do Banco Central, que a calcula e publica.</summary>
public sealed class FonteTaxaLegalBancoCentral() : FonteSerieBancoCentral(TipoTabelaTributaria.TaxaLegal, 29543,
    "https://www3.bcb.gov.br/CALCIDADAO/publico/metodologiaCorrigirPelaTaxaLegal.do?method=metodologiaCorrigirPelaTaxaLegal");
