# Como contribuir

Obrigado pelo interesse em melhorar o **Cálculos Trabalhistas e Tributários**! Este guia explica como preparar o ambiente, as regras do código e o caminho de uma contribuição até o `master`.

Ao participar, você concorda em seguir o [Código de Conduta](CODE_OF_CONDUCT.md).

## Formas de contribuir

- **Relatar um erro de cálculo:** use o modelo [Erro de cálculo](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues/new?template=erro-de-calculo.yml). Informe a competência, os valores digitados, o resultado esperado e, de preferência, a norma que fundamenta o valor (lei, artigo, instrução normativa ou solução de consulta).
- **Relatar outro problema:** telas, relatórios, instalação ou atualização das tabelas, no modelo [Problema](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues/new?template=problema.yml).
- **Sugerir uma melhoria:** uma calculadora nova, uma verba ou uma fonte de tabela, no modelo [Sugestão](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues/new?template=sugestao.yml).
- **Enviar código ou documentação:** correções, testes, fontes de tabelas que mudaram de formato, melhorias no manual.

Para mudanças grandes, como uma calculadora nova ou uma mudança de arquitetura, abra uma issue antes de começar, para combinarmos a abordagem.

Falhas de segurança não devem ir para issues públicas: veja a [Política de Segurança](SECURITY.md).

## Ambiente

- Windows 10 ou superior: a interface é WPF.
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
- Um editor com suporte a C#: Visual Studio 2022, JetBrains Rider ou VS Code com o C# Dev Kit.

O [Inno Setup 6](https://jrsoftware.org/isdl.php) e o GitHub CLI só são necessários para gerar e publicar instaladores, tarefa do mantenedor.

```powershell
git clone https://github.com/mayconwisley/CalculosTrabalhistasTributarios.git
cd CalculosTrabalhistasTributarios
dotnet build .\CalculosTrabalhistasTributarios.sln
dotnet test .\CalculosTrabalhistasTributarios.Tests
dotnet run --project .\CalculosTrabalhistasTributarios
```

Os testes não usam a internet: cada execução parte de uma cópia do banco do projeto, e as fontes das tabelas são lidas de páginas fixas.

## Arquitetura

A solução segue a Clean Architecture com DDD, e cada camada é um projeto. A seção [Arquitetura do README](README.md#arquitetura) mostra as pastas; as regras são estas:

| Projeto | Pode depender de | Contém |
| --- | --- | --- |
| `CalculosTrabalhistasTributarios.Domain` | nada | Regras de negócio: INSS, IRRF, CLT, rescisão, pensão, índices judiciais. |
| `CalculosTrabalhistasTributarios.Application` | Domain | Casos de uso, DTOs e montagem dos demonstrativos. |
| `CalculosTrabalhistasTributarios.Infrastructure` | Application | SQLite, relatórios PDF e Excel, atualização das tabelas pela internet. |
| `CalculosTrabalhistasTributarios` | todos | Aplicativo WPF (MVVM). |

Os testes de arquitetura (`ArquiteturaTests`) falham se uma dependência apontar para fora ou se uma interface ficar fora da pasta `Interfaces`.

## Regras do código

- **Interfaces** ficam na pasta `Interfaces` do projeto a que pertencem.
- **Um tipo por arquivo**, com o nome do tipo. Tipos privados aninhados são a exceção.
- **Result Pattern:** regras e casos de uso devolvem `Result` ou `Result<T>`. Dado inválido, tabela não cadastrada e fonte fora do ar viram um `Erro`, com a mensagem para o usuário, e não exceção. Exceções ficam para falhas inesperadas, como erro de banco ou de arquivo, e para erros de programação.
- **Nomes em português**, como no restante do código: o domínio é a legislação brasileira, e os termos dela (competência, provento, alíquota, verba) ficam mais claros sem tradução.
- **Comentários explicam o porquê** e citam a norma quando a regra vem dela, por exemplo `// Lei 9.430/1996, art. 67`.
- **Estilo:** siga o `.editorconfig` (4 espaços, CRLF, namespaces de arquivo). A compilação deve terminar sem avisos.

### Cálculos e tabelas

- Toda regra nova ou corrigida precisa de teste em `CalculosTrabalhistasTributarios.Tests`, com valores conferidos à mão ou com um exemplo oficial (Receita Federal, INSS, Ministério do Trabalho).
- As tabelas históricas ficam em `Infrastructure/Persistence/Inicializacao/SementesTributarias.cs`. Ao alterar sementes ou scripts do banco, incremente `VersaoSementes` em `InicializadorBancoTributario`, para que os bancos já instalados recebam a nova carga.
- Uma fonte de tabela na internet é uma classe `IFonteTabela<T>` em `Infrastructure/Tributacao/Fontes`, registrada em `Infrastructure/DependencyInjection.cs`. A tabela publicada valida a própria estrutura em `Validar()`, e o teste da fonte usa `PaginasFixas`, com um trecho no formato da página, nunca a página real.

### Documentação

Mudou uma tela, uma mensagem ou uma regra de cálculo? Atualize o [Manual do Usuário](docs/MANUAL.md) e, se for o caso, as imagens em `docs/imagens`. O PDF do manual é gerado a partir do Markdown:

```powershell
dotnet run --project .\tools\GeradorManual
```

## Enviando a contribuição

1. Faça um fork e crie um branch a partir do `master`, com um nome curto, como `corrige-dsr-feriado`.
2. Faça commits pequenos, com mensagens que digam o que mudou e por quê.
3. Rode `dotnet build` e `dotnet test` antes de enviar.
4. Abra o pull request preenchendo o modelo: o que mudou, como foi testado e a base legal, quando houver.

O CI compila e roda os testes em cada pull request. Um mantenedor revisa, pode pedir ajustes e faz o merge quando estiver tudo certo.

## Licença

Ao enviar uma contribuição, você concorda que ela seja distribuída sob a [licença MIT](LICENSE), a mesma do projeto.
