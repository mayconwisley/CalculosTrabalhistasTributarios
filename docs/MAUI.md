# Aplicativo móvel — .NET MAUI

## Escopo inicial

O aplicativo Android é uma apresentação adicional para o mesmo núcleo de cálculo. A solução principal `CalculosTrabalhistasTributarios.sln` inclui os projetos desktop e móveis. O WPF mantém o fluxo de publicação existente. Não há backend ou sincronização com o computador.

A primeira funcionalidade é a simulação de saque-aniversário do FGTS: saldo do extrato, garantia bloqueada, indicação de garantia já incluída, multa incluída, parcela anual comprometida e mês de aniversário. O resultado apresenta os destaques, proventos, descontos, total, memória e observações devolvidos pelo caso de uso existente.

O resultado pode ser compartilhado em PDF pelo menu nativo do Android (WhatsApp, e-mail e outros aplicativos compatíveis instalados). O documento contém destaques, verbas, total, memória, observações e o aviso de simulação em todas as páginas. Não há exportação Excel no aplicativo móvel nem dependência de ClosedXML ou QuestPDF.

Não estão disponíveis nesta etapa: análise de nova antecipação, histórico, importação de formulários, SQLite, atualização de tabelas e demais calculadoras. Os dados do formulário permanecem somente na sessão; não há permissão de rede solicitada pelo manifesto da aplicação. O aplicativo destinatário do compartilhamento gerencia o envio e sua conexão.

## Compartilhar PDF

1. Preencha o saldo, o mês de aniversário e os ajustes do extrato que se aplicarem.
2. Toque em **Calcular meu saque** e confira o valor estimado.
3. Use **Demonstrativo e memória** para consultar os detalhes na tela.
4. Toque em **Compartilhar PDF** e escolha o aplicativo e o destinatário. O envio só ocorre quando você o confirmar no aplicativo escolhido. Fechar o menu não indica falha nem comprova envio.

Editar uma entrada invalida o resultado e exige novo cálculo antes de compartilhar. Durante cálculo ou compartilhamento, os campos e comandos concorrentes ficam indisponíveis.

Os arquivos são gerados em `CacheDirectory/sharing-root`, com nomes exclusivos. O FileProvider expõe apenas essa subpasta. PDFs com mais de sete dias são removidos na próxima geração; o Android também pode limpar seu cache. O arquivo não é apagado ao fechar o menu, pois o destinatário pode ainda estar lendo o conteúdo. Não é solicitada permissão de acesso geral ao armazenamento.

O PDF usa `Android.Graphics.Pdf.PdfDocument`, sem serviços externos. O adaptador `ICompartilhamentoPdf` fica na apresentação móvel; geração e compartilhamento nativos ficam no projeto MAUI. O relatório consome o mesmo `DemonstrativoDto` exibido e não refaz cálculos. O texto obrigatório do rodapé é compartilhado com o desktop por `Application/Demonstrativos/AvisoSimulacao`.

## Aparência e validação

A tela separa saldo e ajustes em cartões, destaca o resultado e permite expandir demonstrativo e memória. Os temas acompanham a preferência do sistema.

<p><img src="imagens/maui-inicio-escuro.png" width="280" alt="Formulário móvel no tema escuro" /> <img src="imagens/maui-resultado-escuro.png" width="280" alt="Resultado móvel com ação Compartilhar PDF" /></p>

Conferência realizada em emulador Android x86_64 com densidade de 420 dpi, em 1080 × 2400 e 840 × 1680 pixels (aproximadamente 411 × 914 e 320 × 640 DIPs), nos temas claro e escuro. Foram verificados preenchimento, seleção de mês, rolagem, resultado, expansão dos detalhes e abertura do menu nativo com o PDF anexado. O exemplo de saldo R$ 8.000,00 produziu resultado de R$ 2.250,00 e PDF de duas páginas, ambas renderizadas e inspecionadas. Não houve envio a destinatários; WhatsApp não estava instalado no emulador. Isso não substitui a validação de distribuição Release e envio em aparelho físico.

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
6. Ao ampliar o relatório móvel para outra calculadora, inclua explicitamente quadros, comparativos ou outros dados especializados do DTO: o gerador atual atende o demonstrativo do saque-aniversário. Preserve o aviso em cada página, os valores da tela e a quebra de textos longos. Valide PDF renderizado, compartilhamento, trimming e execução Release em aparelhos.
7. Para iOS, acrescente alvo, inicializadores e recursos da plataforma em uma etapa própria, com compilação no Mac, assinatura e verificação real. iOS e Windows MAUI não estão configurados nesta base.
8. Cubra mapeamento de entradas, validações, falhas e ciclo de vida dos comandos. Verifique no dispositivo temas claro/escuro, teclado, rolagem, fonte ampliada, valores grandes e orientação. Compilação não comprova usabilidade.
9. Antes de publicar, defina o identificador definitivo da aplicação, assinatura, versionamento, ícones finais, política de dados e diagnóstico. O identificador atual é `br.com.mayconwisley.calculostrabalhistastributarios`; não o altere após a primeira distribuição sem planejar a continuidade dos dados.

## Referências

- [Injeção de dependências no MAUI](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection?view=net-maui-10.0)
- [Plataformas suportadas](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [Bindings compilados](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0)
- [Compartilhamento de arquivos e configuração do FileProvider](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share?view=net-maui-10.0)
- [PDF nativo do Android](https://developer.android.com/reference/android/graphics/pdf/PdfDocument)
