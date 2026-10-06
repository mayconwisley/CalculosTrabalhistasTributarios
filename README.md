# Cálculos Trabalhistas e Tributários

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="CalculosTrabalhistasTributarios/Assets/logo-dark.png" />
    <img src="CalculosTrabalhistasTributarios/Assets/logo-light.png" width="180" alt="Logo de Cálculos Trabalhistas e Tributários" />
  </picture>
</p>

<p align="center">
  Aplicação desktop para simular tributos, verbas trabalhistas, pensão alimentícia e indenizações.
</p>

<p align="center">
  <a href="https://github.com/mayconwisley/CalculosTrabalhistasTributarios/actions/workflows/ci.yml"><img src="https://github.com/mayconwisley/CalculosTrabalhistasTributarios/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/mayconwisley/CalculosTrabalhistasTributarios/releases/latest"><img src="https://img.shields.io/github/v/release/mayconwisley/CalculosTrabalhistasTributarios?label=vers%C3%A3o" alt="Versão mais recente" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/licen%C3%A7a-MIT-green" alt="Licença MIT" /></a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 9" />
  <img src="https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white" alt="C# 13" />
  <img src="https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows&logoColor=white" alt="WPF para Windows" />
  <img src="https://img.shields.io/badge/SQLite-Local%20database-003B57?logo=sqlite&logoColor=white" alt="SQLite" />
  <img src="https://img.shields.io/badge/QuestPDF-2025.12-FF4B4B" alt="QuestPDF" />
</p>

## Visão geral

**Cálculos Trabalhistas e Tributários** é um aplicativo de cálculos executados localmente. Reúne simulações de IRRF, INSS e FGTS, holerite e jornada pelo ponto, verbas trabalhistas, IRPF anual e dividendos, pensão alimentícia, débitos judiciais e indenização por estabilidade, com memória de cálculo, relatórios em PDF, planilhas do Excel e histórico de cálculos.

> Os resultados têm caráter de simulação. A conferência com a legislação vigente, o vínculo empregatício e os dados da competência continua sendo indispensável.

## Instalação

Baixe o instalador **CalculosTrabalhistasTributarios-X.Y.Z-setup.exe** na página de [Releases](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/releases) e execute-o.

- A instalação é feita para o usuário atual, em `%LOCALAPPDATA%\Programs\Cálculos Trabalhistas e Tributários`, e não pede permissão de administrador.
- O .NET já vem incluído: não é preciso instalar nenhum pré-requisito.
- Ao instalar uma versão nova sobre a anterior, as tabelas editadas pelo usuário são preservadas. A desinstalação também mantém o banco de dados.
- Nas atualizações de instalações anteriores, o instalador reutiliza a pasta antiga para manter o banco de dados. Em instalações novas, usa a pasta com o nome atual.
- Como o instalador não é assinado digitalmente, o Windows pode exibir o aviso do SmartScreen: selecione **Mais informações** e depois **Executar assim mesmo**.

## Manual do usuário

O passo a passo completo de cada tela, com prints, está no **[Manual do Usuário](docs/MANUAL.md)**, também disponível em **[PDF](docs/ManualDoUsuario.pdf)**. No aplicativo, o manual abre pelo botão **Manual do usuário** ou pela tecla **F1**, em qualquer janela.

O PDF é gerado a partir do Markdown. Depois de editar `docs/MANUAL.md` ou as imagens em `docs/imagens`, regenere-o na raiz do repositório:

```powershell
dotnet run --project .\tools\GeradorManual
```

## Recursos

### Simulação tributária

- Calcula **IRRF** pelas modalidades normal e simplificada.
- Considera dependentes, desconto simplificado e redução mensal do IRRF quando aplicável, e não retém o IRRF de até R$ 10,00 (desconto mínimo, Lei 9.430/1996, art. 67).
- Calcula **INSS** por faixas: alíquota única nas competências anteriores a março de 2020 e modelo progressivo nas posteriores, com o valor de cada faixa truncado nos centavos, como no eSocial.
- Calcula **FGTS padrão (8%)** e **FGTS para Jovem Aprendiz (2%)**.
- Compara as modalidades de IRRF e destaca a alternativa mais vantajosa.
- Mostra o salário líquido, descontando o INSS e o IRRF da modalidade mais vantajosa.
- Exibe indicadores, faixas utilizadas e fórmulas na memória de cálculo, com cada dedução da base do IRRF identificada.

### Calculadoras trabalhistas

Todas usam a mesma janela, com resumo, demonstrativo no formato de holerite, valores informativos (como o FGTS), memória de cálculo, observações e PDF:

| Calculadora | O que calcula |
| --- | --- |
| Holerite do mês | Salário, insalubridade ou periculosidade, horas extras, comissões com DSR automático, adicional noturno, faltas com o DSR perdido, atrasos, INSS, IRRF, pensão, vale-transporte (até 6%) e salário-família em um só demonstrativo. |
| Comissões e DSR | Comissões, repousos remunerados e complemento da garantia mínima, com INSS, IRRF, FGTS e impacto líquido; aceita valor que já inclua DSR e contagem de dias informada para períodos especiais. |
| INSS em múltiplos vínculos | Desconto do segurado por vínculo, na ordem informada, com teto mensal compartilhado; considera empregos, trabalho doméstico, avulso e prestação de serviço como contribuinte individual. |
| Jornada pelo ponto | Apuração das marcações de entrada e saída do mês: horas extras, noturnas (com a prorrogação da Súmula 60), faltas, atrasos e intervalos suprimidos (arts. 66 e 71), levadas à calculadora de horas extras ou ao holerite. |
| Salário bruto a partir do líquido | O salário bruto que, descontados INSS e IRRF, resulta no líquido desejado. |
| Horas extras e adicionais | Horas extras em duas faixas, adicional noturno urbano com a hora reduzida ou rural de 25% sem redução e o reflexo no DSR, com o líquido do mês. |
| Insalubridade e periculosidade | Insalubridade de 10%, 20% ou 40% sobre o salário mínimo (ou outra base de convenção) e periculosidade de 30%, aplicando o maior quando os dois se aplicam. |
| Salário-família | Direito e valor pela remuneração e pelos filhos, com as duas faixas anteriores a 2020 e a cota proporcional na admissão e no desligamento. |
| 13º salário | 1ª e 2ª parcelas, com médias, avos e INSS e IRRF de tributação exclusiva. |
| Férias | Férias com 1/3, dias de direito conforme as faltas, venda de 1/3 (abono, isento) e adiantamento do 13º. |
| PLR | IRRF pela tabela anual exclusiva da Lei 10.101/2000, recalculado sobre o total do ano, com a dedução da pensão alimentícia e sem INSS e FGTS. |
| Rescisão | Verbas por motivo de desligamento, inclusive a rescisão antecipada do contrato a prazo (arts. 479 e 480), aviso prévio proporcional com projeção, férias vencidas (em dobro) e proporcionais, indenização adicional da data-base, multa por atraso (art. 477), DSR perdido, verbas indenizatórias e outros descontos (limitados pelo art. 477, § 5º), FGTS, multa, saque e o seguro-desemprego estimado; para o doméstico, a indenização compensatória de 3,2% no lugar da multa, e para o aprendiz, FGTS de 2%. |
| Seguro-desemprego | Valor da parcela pela média dos últimos salários e quantidade de parcelas pelos meses trabalhados e pela solicitação (Lei 7.998/1990); para o doméstico, um salário mínimo em até 3 parcelas (LC 150/2015). |
| Custo do funcionário | Encargos por regime tributário (Lucro Real ou Presumido, Simples Nacional e empregador doméstico), FGTS de 2% do aprendiz, provisões de 13º e férias e benefícios. |
| Empregado doméstico (DAE) | Salário líquido do doméstico e o DAE do mês: 8% patronal, 0,8% de GILRAT, 8% de FGTS, 3,2% de indenização compensatória, INSS e IRRF do empregado (LC 150/2015). |
| Estágio | Bolsa líquida com o IRRF, sem INSS e FGTS, e o recesso remunerado proporcional (Lei 11.788/2008). |
| Trabalho intermitente | Pagamento de cada convocação: horas, DSR, férias proporcionais com 1/3, 13º proporcional e FGTS (CLT, art. 452-A). |
| Afastamentos e licenças | Doença e acidente de trabalho (15 dias da empresa e o auxílio estimado do INSS), licença-maternidade (120 ou 180 dias) e paternidade (LC 229/2026), com FGTS e estabilidade. |
| Saque-aniversário do FGTS | Valor do saque pela tabela da Lei 8.036/1990 e o efeito da opção numa dispensa. |
| Abono salarial (PIS/Pasep) | Direito e valor do abono, com o limite de renda da EC 135/2024. |
| Pró-labore e autônomo | INSS de 11% até o teto, IRRF, ISS do autônomo e o custo para a empresa. |
| CLT x PJ | O ano do mesmo profissional como CLT e como PJ no Simples Nacional (anexos III e V, com o fator R): total para o profissional, custo para a empresa e o valor de PJ que iguala o CLT. |
| IRPF anual | Declaração do ano-calendário 2026 em diante, com a tabela anual, a redução do art. 11-A, os modelos completo e simplificado e a tributação mínima das altas rendas com o redutor (Lei 15.270/2025). |
| Dividendos | Retenção de 10% sobre lucros e dividendos acima de R$ 50 mil no mês da mesma empresa, ou sobre qualquer valor para residentes no exterior. |
| Carnê-leão | IR mensal de honorários, aluguéis e outros rendimentos recebidos de pessoas físicas e do exterior, com livro-caixa, desconto simplificado e a redução mensal. |
| Ganho de capital | IR na venda de imóveis e outros bens: isenções de pequeno valor, do único imóvel e do reinvestimento, redução dos imóveis até 1988, fatores FR1 e FR2 e alíquotas de 15% a 22,5%. |
| Tributo em atraso | Multa de 0,33% ao dia (até 20%) e juros pela Selic mais 1% de DARF, DAS, DAE ou GPS pago depois do vencimento (Lei 9.430/1996, art. 61). |
| Correção de valores | Valor atualizado por IPCA, INPC, IPCA-E, Selic, TR ou taxa legal, com juros simples e multa opcionais. |

As regras da CLT ficam em `CalculosTrabalhistasTributarios.Domain/Trabalhista` (com a rescisão em `Trabalhista/Rescisao`), e a apuração de INSS e IRRF comum a todas as calculadoras, em `CalculosTrabalhistasTributarios.Domain/Tributacao/TabelasDaCompetencia`.

### Pensão alimentícia e documentos

- Simula pensão alimentícia de um ou mais beneficiários, com os próprios dados de rendimentos, sobre os rendimentos líquidos, os brutos, o salário mínimo ou em valor fixo; com vários beneficiários, as pensões incidem sobre a mesma base ou cada uma após descontar as anteriores.
- Compara as modalidades de tributação: a pensão é deduzida da base do IRRF nas deduções legais, e não no desconto simplificado, que substitui todas elas.
- Explica como a pensão foi obtida e mostra o efeito dela em quem paga: IRRF com e sem a pensão, economia de imposto e líquido.
- Desconta a pensão também no 13º salário, nas férias, na rescisão (sobre as verbas salariais) e na PLR, com a dedução no IRRF de cada verba.
- Compara a pensão atual com a proposta em uma revisão, com o efeito no IRRF e no líquido de quem paga.
- Atualiza a pensão em atraso pelo INPC ou pelo IPCA, com juros de 1% ao mês até 29/08/2024 e a taxa legal publicada pelo Banco Central depois (Lei 14.905/2024 e Resolução CMN 5.171/2024, proporcional aos dias), separa as parcelas do rito da prisão (Súmula 309 do STJ) das do rito da penhora e inclui, se escolhidos, a multa e os honorários de 10% do art. 523 do CPC.
- Atualiza débitos judiciais trabalhistas (IPCA-E e TR antes do ajuizamento, Selic até 29/08/2024 e IPCA com taxa legal depois, ADC 58 e TST) e cíveis (o índice da decisão, a Selic com os juros até 29/08/2024 e IPCA com taxa legal depois, Tema 1.368 do STJ).
- Exporta relatórios em PDF com resumo executivo e memória de cálculo.

### Cálculo de estabilidade

- Apura a indenização pelos salários do período restante de estabilidade: meses cheios e os dias que sobram.
- Calcula 13º salário e férias proporcionais, incluindo o adicional de um terço.
- Calcula FGTS de 8% e multa rescisória de 40%, com complementos informados pelo usuário.
- Exibe a memória de cálculo para conferência dos valores.
- Gera um demonstrativo em PDF inspirado no relatório legado, com as verbas, os dados considerados e o **Total a Receber**.

### Gestão de tabelas

- Mantém localmente faixas de INSS e IRRF, dedução por dependente, desconto simplificado, desconto mínimo, redução mensal, tabela anual da PLR, salário-família, salário mínimo e seguro-desemprego, com o histórico desde 2017, e os índices mensais INPC, IPCA, IPCA-E, Selic e TR desde 2015 e a taxa legal desde 08/2024.
- Permite incluir, editar e remover registros por competência.
- Inicializa dados históricos de forma idempotente, sem sobrescrever manutenções locais.
- Atualiza pela internet todas as tabelas, inclusive o seguro-desemprego e o desconto mínimo, pela fonte oficial ou, quando ela ainda não publicou a tabela do ano, por duas fontes alternativas que concordem entre si; em caso de falha, preserva os dados locais.
- Atualiza os índices pela API do IBGE (INPC, IPCA e IPCA-15, usado como IPCA-E) e do Banco Central (taxa legal, Selic e TR, séries 29543, 4390 e 7811); se a fonte oficial não responder, uma fonte alternativa, como o Ipeadata, só preenche os meses que faltam.

### Experiência de uso

- Tela inicial em cartões, com as calculadoras agrupadas por assunto e as tabelas em uma aba própria; cada cálculo abre na sua janela.
- Avisos ao abrir: versão nova publicada no GitHub (a consulta pode ser desligada) e tabelas do ano ainda não cadastradas; na primeira abertura, o lembrete de que os resultados são simulações.
- Erros inesperados não fecham o aplicativo em silêncio: a mensagem aparece, e os detalhes vão para um arquivo de log mensal em `%LOCALAPPDATA%\CalculoIRRF\logs`, para o suporte.
- Cada calculadora abre preenchida com a competência, o salário e os dependentes do último cálculo.
- Histórico de cálculos: cada cálculo pode ser salvo com um nome e depois aberto, duplicado, renomeado ou excluído na aba Histórico, com busca por nome ou calculadora.
- Exportação para o Excel em todas as calculadoras, com os valores como números e a memória de cálculo em outra aba.
- A janela inteira rola quando o resultado não cabe, sem espremer o resultado abaixo do formulário.
- Manual do usuário integrado: botão no cabeçalho e tecla F1 em qualquer janela.
- Tema claro, escuro e automático, seguindo a preferência do Windows no modo automático.
- Barras de rolagem finas e arredondadas, no padrão atual do Windows, com cores ajustadas a cada tema.
- Valores formatados no padrão brasileiro em todas as telas, inclusive nas tabelas de manutenção.
- Preferência de tema persistida no perfil do usuário.
- Renderização por software por padrão, reduzindo o consumo de memória; a aceleração por GPU pode ser reativada nas configurações locais.
- Processamento e persistência locais em SQLite.
- Identidade visual adaptada aos dois temas, com ícones Windows multirresolução.

## Tecnologias

| Tecnologia | Versão | Uso no projeto |
| --- | --- | --- |
| [.NET](https://dotnet.microsoft.com/) | 9 | Plataforma de execução e compilação. |
| C# | 13 | Linguagem principal da aplicação. |
| [WPF](https://learn.microsoft.com/dotnet/desktop/wpf/) | .NET 9 | Interface desktop, recursos e temas. |
| [SQLite](https://www.sqlite.org/) | — | Banco de dados local das tabelas tributárias. |
| [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) | 9.0.2 | Acesso direto ao SQLite, com as tabelas mantidas em memória entre os cálculos. |
| [QuestPDF](https://www.questpdf.com/) | 2025.12.1 | Geração dos relatórios PDF. |
| [Html Agility Pack](https://html-agility-pack.net/) | 1.11.74 | Leitura das páginas HTML usadas na atualização das tabelas. |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) | 0.105.0 | Geração das planilhas do Excel. |

## Arquitetura

A solução segue a Clean Architecture com DDD: cada camada é um projeto, e as dependências apontam só para dentro, o que o compilador garante. O domínio não depende de nada; a aplicação, só do domínio.

```text
CalculosTrabalhistasTributarios.Domain/          Regras de negócio, sem dependências externas
├── Comum/             Result, Result<T> e Erro (Result Pattern)
├── Tributacao/        INSS, IRRF, PLR, Simples Nacional e as tabelas da competência
├── Trabalhista/       CLT, horas extras, seguro-desemprego e, em Rescisao/, as verbas rescisórias
├── Pensao/            Regra da pensão e o cálculo sobre o líquido
├── Judicial/          Índices, correção e juros dos débitos judiciais e da pensão em atraso
└── Estabilidade/      Indenização do período de estabilidade
CalculosTrabalhistasTributarios.Application/     Casos de uso: orquestram o domínio e montam os demonstrativos
├── Interfaces/        Portas (consulta das tabelas, histórico, relatórios) e contratos dos casos de uso
├── UseCases/          Um caso de uso por calculadora
├── Demonstrativos/    Memória de cálculo, formatação e montagem dos demonstrativos
├── DTOs/              Pedidos e resultados que atravessam as camadas
└── Extensoes/, Mapeamentos/
CalculosTrabalhistasTributarios.Infrastructure/  Adaptadores das portas
├── Interfaces/        Contratos internos (fontes das tabelas, cache, inicialização do banco)
├── Persistence/       SQLite; em Inicializacao/, esquema, correções e carga das tabelas oficiais
├── Reporting/         PDF (QuestPDF) e Excel (ClosedXML), uma classe por relatório
└── Tributacao/        Atualização das tabelas pela internet
CalculosTrabalhistasTributarios/                 Aplicativo WPF (MVVM)
├── Presentation/      Interfaces/, ViewModels, serviços WPF, comportamentos e tema
├── Views/             Janelas em XAML
├── Assets/            Logos e ícones para os temas claro e escuro
└── BancoDados/        Banco SQLite distribuído com a aplicação
```

- **Interfaces:** ficam na pasta `Interfaces` de cada projeto, e um teste de arquitetura falha se alguma ficar fora dela.
- **Result Pattern:** regras e casos de uso devolvem `Result<T>`. Dado inválido, tabela não cadastrada e fonte fora do ar viram um `Erro` com a mensagem para o usuário, sem exceção; exceções ficam para falhas inesperadas, como erro de banco ou de arquivo. Na tela, o `LeitorFormulario` lê os campos e guarda o primeiro em formato inválido.
- **Responsabilidade única:** um tipo por arquivo. Nas rescisões, o domínio calcula as verbas (`CalculadoraRescisao`), o caso de uso cuida das tabelas, dos tributos e da pensão, e `Demonstrativos/Rescisao` monta a memória e as observações.
- **Injeção de dependência:** cada camada registra os seus serviços (`AddApplication`, `AddInfrastructure`, `AddPresentation`), e o `App.xaml.cs` só compõe.

### Fluxo de uma calculadora

1. A tela lê o formulário com o `LeitorFormulario`; um campo em formato inválido vira aviso sem chamar o cálculo.
2. O caso de uso da `Application` obtém as tabelas da competência pela porta `ITributacaoConsulta` e chama as regras do `Domain`.
3. Cada etapa devolve `Result`: a primeira falha volta para a tela, que a mostra como aviso.
4. Com sucesso, o caso de uso monta o `DemonstrativoDto` (proventos, descontos, memória e observações), que a tela exibe e os relatórios exportam.

## Pré-requisitos

- Windows 10 ou superior.
- **.NET 9 SDK** para compilar e desenvolver.
- Para gerar e publicar instaladores: [Inno Setup 6](https://jrsoftware.org/isdl.php) e o [GitHub CLI](https://cli.github.com) autenticado (`gh auth login`).

## Executar localmente

No diretório raiz da solução:

```powershell
dotnet restore
dotnet build .\CalculosTrabalhistasTributarios.sln
dotnet run --project .\CalculosTrabalhistasTributarios\CalculosTrabalhistasTributarios.csproj
```

O executável de desenvolvimento é gerado como `CalculosTrabalhistasTributarios.exe` em `CalculosTrabalhistasTributarios\bin\<configuração>\net9.0-windows`.

## Testes

O projeto `CalculosTrabalhistasTributarios.Tests` (xUnit) confere os cálculos com valores de referência:

- a simulação tributária contra um modelo independente, com as tabelas oficiais digitadas à parte, em toda a faixa de salários de 2019 a 2026;
- os pontos fixos da pensão, a taxa legal parcela a parcela, a rescisão, o seguro-desemprego, o CLT x PJ, o holerite, a jornada pelo ponto, os débitos judiciais (contra um recálculo independente das fases), o IRPF anual e os dividendos (com os exemplos da Fazenda) e as demais calculadoras, com valores conferidos à mão;
- as planilhas do Excel geradas e o histórico, inclusive o formulário de cada calculadora salvo e reaberto;
- as tabelas semeadas e a leitura de números digitados;
- a arquitetura: as dependências entre as camadas, as interfaces na pasta `Interfaces` e a composição de todos os serviços.

Cada execução parte de uma cópia nova do banco do projeto, inicializada como na primeira abertura do aplicativo.

```powershell
dotnet test .\CalculosTrabalhistasTributarios.Tests
```

O `gerar-instalador.ps1` roda os testes antes de publicar: se algum falhar, a versão não é gerada. No GitHub, o [CI](.github/workflows/ci.yml) compila a solução e roda os testes em cada push no `master` e em cada pull request.

## Gerar o instalador

O instalador é gerado localmente com o Inno Setup. O script publica o aplicativo para Windows x64 com o .NET incluído e grava o resultado em `artefatos\`:

```powershell
.\instalador\gerar-instalador.ps1 -Versao 1.2.0
```

| Arquivo | Função |
| --- | --- |
| `instalador\CalculosTrabalhistasTributarios.iss` | Definição do instalador: instalação por usuário, atalhos, idioma e preservação do banco. |
| `instalador\gerar-instalador.ps1` | Publica o aplicativo e compila o instalador. |
| `instalador\publicar-release.ps1` | Gera o instalador de uma tag e o publica no GitHub Releases. |
| `.githooks\pre-push` | Aciona a publicação quando uma tag de versão é enviada. |

## Publicar uma nova versão

Esta seção é para o mantenedor. A publicação acontece ao enviar uma tag no formato `vX.Y.Z`. Todo o processo roda na sua máquina; o GitHub recebe apenas a tag e o instalador pronto.

Ative os hooks do repositório uma única vez por clone:

```powershell
git config core.hooksPath .githooks
```

Depois, para cada versão:

```powershell
git tag v1.2.0
git push origin master v1.2.0
```

Durante o push, o hook:

1. Compila o aplicativo a partir de uma cópia isolada do código da tag (`git worktree`), com a versão da tag gravada no executável.
2. Gera o instalador com o Inno Setup. Se algo falhar, **o push é cancelado** e nenhuma versão quebrada chega ao GitHub.
3. Deixa um processo em segundo plano aguardando a tag chegar ao GitHub. Em seguida, ele cria o release com o instalador, as instruções de instalação e o SHA-256 do arquivo. O resultado é avisado em uma janela e registrado em `artefatos\release-vX.Y.Z.log`.

Pushes sem tag de versão não são afetados. Tags com sufixo, como `v1.2.0-beta.1`, são publicadas como *pré-lançamento*.

Para testar o processo sem enviar nada, use a simulação, que gera o instalador e mostra o que seria publicado:

```powershell
$env:CALCULADORA_SIMULAR_RELEASE = '1'
git push --dry-run origin v1.2.0
Remove-Item Env:CALCULADORA_SIMULAR_RELEASE
```

Se a tag já estiver no GitHub sem o release, por exemplo porque a conexão caiu, publique manualmente:

```powershell
.\instalador\publicar-release.ps1 -Tag v1.2.0
```

## Dados locais e configurações

| Item | Localização | Comportamento |
| --- | --- | --- |
| Tabelas tributárias e histórico | `BancoDados\calculoIrrf.db`, na pasta do aplicativo | Armazena faixas, parâmetros e índices por competência e os cálculos salvos no histórico. Instalado apenas na primeira instalação; atualizações e a desinstalação preservam o arquivo. |
| Tema e renderização | `%LOCALAPPDATA%\CalculoIRRF\settings.json` | Mantém a opção Claro, Escuro ou Automático. Com `"HardwareAcceleration": true`, a interface volta a ser renderizada pela GPU, com maior consumo de memória. |
| PDFs e planilhas | Diretório escolhido pelo usuário | Gerados sob demanda em todas as calculadoras. |

Os caminhos `BancoDados\calculoIrrf.db` e `%LOCALAPPDATA%\CalculoIRRF\settings.json` são mantidos para preservar os dados e as preferências de instalações existentes. O mesmo identificador do instalador permite atualizar versões anteriores sem criar outro aplicativo no Windows.

## Atualização das tabelas pela internet

No começo do ano, alguns sites publicam as novas tabelas de INSS e IRRF antes das páginas do governo. Para a calculadora não ficar atrasada, a atualização consulta em paralelo a fonte oficial e duas fontes alternativas:

| Tabela | Fonte oficial | Fontes alternativas |
| --- | --- | --- |
| INSS | [gov.br/inss](https://www.gov.br/inss/pt-br/direitos-e-deveres/inscricao-e-contribuicao/tabela-de-contribuicao-mensal) | [debit.com.br](https://www.debit.com.br/tabelas/tabelas-inss) e [contabeis.com.br](https://www.contabeis.com.br/tabelas/inss/) |
| IRRF | [Receita Federal](https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/tabelas) | [debit.com.br](https://www.debit.com.br/tabelas/tabelas-irrf) e [contabeis.com.br](https://www.contabeis.com.br/tabelas/imposto-renda/) |
| PLR | [Receita Federal](https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/tabelas), na mesma página do IRRF | Nenhuma: os sites alternativos não publicam a tabela da PLR |
| Salário-família | [gov.br/inss](https://www.gov.br/inss/pt-br/direitos-e-deveres/salario-familia/valor-limite-para-direito-ao-salario-familia) | [debit.com.br](https://www.debit.com.br/tabelas/salario-familia) e [contabeis.com.br](https://www.contabeis.com.br/tabelas/salario-familia/) |
| Salário mínimo | 1ª faixa da [tabela de contribuição do INSS](https://www.gov.br/inss/pt-br/direitos-e-deveres/inscricao-e-contribuicao/tabela-de-contribuicao-mensal), que desde a EC 103/2019 vai até um salário mínimo | [contabeis.com.br](https://www.contabeis.com.br/tabelas/salario-minimo/) e a 1ª faixa do INSS no [debit.com.br](https://www.debit.com.br/tabelas/tabelas-inss) |
| Seguro-desemprego | [Ministério do Trabalho](https://www.gov.br/trabalho-e-emprego/pt-br/servicos/trabalhador/seguro-desemprego/seguro-desemprego-formal) | [debit.com.br](https://www.debit.com.br/tabelas/seguro-desemprego) e [idinheiro.com.br](https://www.idinheiro.com.br/tabelas/tabela-seguro-desemprego/) |
| Desconto mínimo | Art. 67 da [Lei 9.430/1996](https://www.planalto.gov.br/ccivil_03/leis/l9430.htm), no Planalto | Nenhuma: o valor é fixado pela lei |
| INPC, IPCA e IPCA-E | API Sidra do [IBGE](https://sidra.ibge.gov.br/) (tabelas 1736, 1737 e 3065) | [vriconsulting.com.br](https://www.vriconsulting.com.br/) para o INPC e o IPCA |
| Taxa legal, Selic e TR | API do [Banco Central](https://dadosabertos.bcb.gov.br/) (séries 29543, 4390 e 7811) | [cajud.com.br](https://cajud.com.br/tabelas/taxa-legal) para a taxa legal e [Ipeadata](https://www.ipeadata.gov.br/) para a Selic e a TR |

A tabela gravada é a de competência mais recente que tenha sido confirmada:

- a da fonte oficial vale sozinha;
- sem ela, a tabela só é aceita quando as duas fontes alternativas trazem exatamente os mesmos valores. Uma fonte sozinha, ou fontes que divergem, não alteram o banco, e a mensagem avisa qual fonte já tem a tabela nova.

As faixas do seguro-desemprego só são gravadas se fecharem entre si: o valor fixo da 2ª faixa é 80% do limite da 1ª, e o valor máximo, a parcela no limite da 2ª. O desconto mínimo é conferido com a lei desde o início da vigência dela; os registros com outro valor até o mês atual são corrigidos.

As fontes alternativas do IRRF publicam apenas as faixas. Nesse caso, o desconto simplificado é calculado em 25% do limite da faixa isenta, como determina a Lei 9.250/1995, e a dedução por dependente e a redução mensal continuam com os valores cadastrados.

Cada fonte é lida por uma classe em `Infrastructure/Tributacao/Fontes`, a escolha entre elas fica em `ConsultaDeFontes`, e `AtualizadorTabelas` encaminha a atualização de cada tabela da janela de manutenção ao seu atualizador. Como a estrutura das páginas públicas pode mudar, uma fonte com falha é apenas ignorada; se nenhuma tabela for confirmada, nada é gravado e a mensagem explica o que aconteceu com cada fonte.

Revise os valores atualizados antes de utilizá-los em cálculos que exijam precisão legal ou contábil.

## Identidade visual

O aplicativo seleciona automaticamente a logo apropriada ao tema ativo. Os arquivos também podem ser reutilizados em instaladores e materiais de distribuição:

| Tema claro | Tema escuro |
| --- | --- |
| [Logo PNG](CalculosTrabalhistasTributarios/Assets/logo-light.png) · [Ícone ICO](CalculosTrabalhistasTributarios/Assets/icon-light.ico) | [Logo PNG](CalculosTrabalhistasTributarios/Assets/logo-dark.png) · [Ícone ICO](CalculosTrabalhistasTributarios/Assets/icon-dark.ico) |

## Contribuindo

Contribuições são bem-vindas: correções de cálculo com a base legal, testes, fontes de tabelas que mudaram de formato e melhorias no manual. Antes de começar, leia o [guia de contribuição](CONTRIBUTING.md) e o [Código de Conduta](CODE_OF_CONDUCT.md).

- Encontrou um valor errado? Abra uma issue no modelo [Erro de cálculo](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues/new?template=erro-de-calculo.yml), com a competência, os valores e o resultado esperado.
- Falhas de segurança seguem a [Política de Segurança](SECURITY.md), sem detalhes em issues públicas.

## Limitações e responsabilidade

- A aplicação não substitui sistemas de folha de pagamento, contadores ou orientação jurídica.
- A exatidão da simulação depende da competência e dos parâmetros tributários mantidos no banco local.
- No cálculo de estabilidade, cabe ao usuário informar corretamente as datas, a média remuneratória, os dias-base e os complementos aplicáveis ao vínculo.
- Atualizações online dependem da disponibilidade e da estrutura das páginas das fontes oficiais e alternativas.

## Licença

Distribuído sob a [licença MIT](LICENSE): você pode usar, modificar e redistribuir o código, inclusive em projetos comerciais, mantendo o aviso de copyright.

O aplicativo usa componentes de terceiros, cada um com a sua licença, listados em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). O QuestPDF, que gera os PDFs, é gratuito para projetos de código aberto e para empresas com faturamento anual abaixo de US$ 1 milhão; acima disso, quem desenvolver uma versão própria precisa de uma [licença comercial do QuestPDF](https://www.questpdf.com/license/).
