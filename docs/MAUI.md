# Aplicativo móvel — .NET MAUI

## Escopo inicial

O aplicativo Android é uma apresentação adicional para o mesmo núcleo de cálculo. A solução principal `CalculosTrabalhistasTributarios.sln` inclui os projetos desktop e móveis. O WPF mantém o fluxo de publicação existente. Não há backend ou sincronização com o computador.

A primeira funcionalidade é a simulação de saque-aniversário do FGTS: saldo do extrato, garantia bloqueada, indicação de garantia já incluída, multa incluída, parcela anual comprometida e mês de aniversário. O resultado apresenta os destaques, proventos, descontos, total, memória e observações devolvidos pelo caso de uso existente.

Não estão disponíveis nesta etapa: análise de nova antecipação, histórico, importação/exportação, SQLite, PDF, Excel, atualização de tabelas e demais calculadoras. As limitações aparecem também na tela. Os dados do formulário permanecem somente na sessão; não há permissão de rede solicitada pelo manifesto da aplicação.

## Estrutura e dependências

```text
CalculosTrabalhistasTributarios.Mobile.sln
├── Domain                         # regras existentes, independentes de interface
├── Application                    # casos de uso e contratos existentes
├── Mobile.Presentation            # net10.0: ViewModels por funcionalidade, sem MAUI/WPF
│   ├── DependencyInjection.cs
│   └── SaqueAniversario/
├── Mobile                         # net10.0-android: composição e controles nativos
│   ├── MauiProgram.cs
│   ├── App.xaml
│   ├── Comportamentos/
│   ├── Features/SaqueAniversario/
│   ├── Platforms/Android/
│   └── Resources/
└── Mobile.Tests                   # net10.0: testes sem dispositivo ou workload MAUI
```

Fluxo: página → binding/command do ViewModel → `ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>` → domínio → `DemonstrativoDto` → página.

- `MauiProgram` é o ponto de composição. Registra somente os serviços necessários, usando `AddSaqueAniversario` da Application e `AddPresentation` da apresentação móvel.
- Cada janela possui um escopo de DI, descartado em `Window.Destroying`. Página e ViewModel são transient; não compartilhe formulários mutáveis em singletons.
- `Mobile.Presentation` usa CommunityToolkit.Mvvm para notificações e comandos assíncronos. Não depende de controles MAUI, WPF ou Infrastructure.
- `LeituraNumerica.cs` é incluído como fonte vinculada da apresentação existente. Há uma única implementação do parser, sem carregar o assembly WPF. Ao alterá-lo, execute os testes de ambas as apresentações. Se mais componentes independentes precisarem ser compartilhados, avalie uma biblioteca comum de apresentação, sem mover parsing de formulário para o domínio.
- Os bindings são tipados com `x:DataType`; mantenha a compilação XAML estrita. Textos e valores usam `pt-BR`; cores acompanham o tema do sistema.
- O comando impede execução simultânea e recebe `CancellationToken`. A saída da página cancela a operação. Editar uma entrada invalida o resultado exibido.
- Falhas esperadas usam o `Result` existente. Falhas inesperadas na fronteira de apresentação são registradas com `ILogger`, sem formulário completo, e produzem mensagem ao usuário. O provedor de diagnóstico atual é Debug; antes de distribuição em produção, integrar diagnóstico persistente no armazenamento privado do aplicativo.

## Preparação e execução

Requisitos: .NET SDK 10 compatível com `global.json`, workload MAUI Android, Android SDK, JDK compatível e emulador ou aparelho Android com depuração USB. O alvo mínimo configurado é Android 7/API 24. Para uma instalação de desenvolvimento, use o Visual Studio Installer ou restaure os workloads:

```powershell
dotnet workload restore .\CalculosTrabalhistasTributarios.Mobile\CalculosTrabalhistasTributarios.Mobile.csproj
dotnet restore .\CalculosTrabalhistasTributarios.Mobile.sln
dotnet build .\CalculosTrabalhistasTributarios.Mobile.sln -c Debug --no-restore
dotnet test .\CalculosTrabalhistasTributarios.Mobile.Tests\CalculosTrabalhistasTributarios.Mobile.Tests.csproj
```

Abra a solução principal ou a solução móvel no Visual Studio, selecione o projeto Mobile como inicialização e escolha o aparelho/emulador. Se a solução já estava aberta durante a inclusão dos projetos, aceite seu recarregamento ou feche e abra novamente o arquivo `.sln`. Com um destino Android disponível, também é possível executar:

```powershell
dotnet build .\CalculosTrabalhistasTributarios.Mobile\CalculosTrabalhistasTributarios.Mobile.csproj -t:Run -f net10.0-android
```

Os testes de apresentação podem rodar sem Android SDK. Para compilar somente o desktop sem workloads móveis, use `dotnet build .\CalculosTrabalhistasTributarios.Desktop.slnf`, como no README e no CI. A compilação da solução principal completa exige também o ambiente MAUI Android.

## Regras para ampliar

1. Reutilize ou implemente o caso de uso na Application, com regras exclusivamente no Domain. Não copie fórmulas para ViewModels ou XAML.
2. Registre a funcionalidade na camada responsável. Não chame `AddApplication` completo no móvel enquanto suas portas de infraestrutura não estiverem implementadas; o registro completo exige banco, consultas e outros serviços.
3. Crie o ViewModel em `Mobile.Presentation/<Funcionalidade>` e a página em `Mobile/Features/<Funcionalidade>`. Mantenha code-behind limitado ao ciclo de vida e comportamento visual.
4. Crie portas novas somente para necessidades reais e coloque interfaces em `Interfaces` da camada responsável. Não crie novos projetos Domain ou Application exclusivos do celular.
5. Para SQLite, adapte o caminho ao armazenamento privado (`FileSystem.AppDataDirectory`), inicialize em local gravável e preserve sementes, transações e compatibilidade. Não referencie a Infrastructure inteira apenas para usar um serviço: primeiro valide e separe as dependências incompatíveis.
6. Valide bibliotecas nativas, trimming e execução Release em aparelhos antes de incluir PDF ou outras exportações. Abrir e compartilhar arquivos requer implementação móvel.
7. Para iOS, acrescente alvo, inicializadores e recursos da plataforma em uma etapa própria, com compilação no Mac, assinatura e verificação real. iOS e Windows MAUI não estão configurados nesta base.
8. Cubra mapeamento de entradas, validações, falhas e ciclo de vida dos comandos. Verifique no dispositivo temas claro/escuro, teclado, rolagem, fonte ampliada, valores grandes e orientação. Compilação não comprova usabilidade.
9. Antes de publicar, defina o identificador definitivo da aplicação, assinatura, versionamento, ícones finais, política de dados e diagnóstico. O identificador atual é `br.com.mayconwisley.calculostrabalhistastributarios`; não o altere após a primeira distribuição sem planejar a continuidade dos dados.

## Referências

- [Injeção de dependências no MAUI](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection?view=net-maui-10.0)
- [Plataformas suportadas](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [Bindings compilados](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0)
