# Política de Privacidade

Vigência: 26/10/2026. Vale para o aplicativo Cálculos Trabalhistas e Tributários para Windows e para Android.

## 1. Resumo

O aplicativo não coleta dados pessoais. Não há cadastro, conta, propaganda, estatística de uso ou rastreamento, e os dados digitados nos formulários não são enviados ao autor nem a terceiros. Os cálculos, o histórico e as preferências são processados e guardados apenas no computador ou aparelho em que o aplicativo está instalado.

## 2. O que fica guardado no computador ou aparelho

- **Tabelas e histórico:** faixas, parâmetros e índices por competência e os cálculos salvos no histórico ficam num banco SQLite local. No Windows, o arquivo é `BancoDados\calculoIrrf.db`, na pasta de instalação, e é preservado nas atualizações e na desinstalação; no Android, fica na área privada do aplicativo e é apagado ao desinstalá-lo.
- **Preferências:** tema, opções de internet, data da última atualização das tabelas, favoritas e outras opções. No Windows, ficam em `%LOCALAPPDATA%\CalculoIRRF\settings.json`, junto com o tamanho e a posição das janelas; no Android, nas preferências privadas do aplicativo.
- **Rascunhos (Android):** os formulários em preenchimento são guardados em arquivos privados do aplicativo para serem recuperados se ele for fechado.
- **Registro de erros:** falhas inesperadas são gravadas em arquivos mensais, com a data, a versão do aplicativo, a versão do sistema operacional, o ponto do aplicativo em que ocorreram e os detalhes técnicos do erro, sem os valores dos formulários. No Windows, ficam em `%LOCALAPPDATA%\CalculoIRRF\logs`; no Android, na área privada do aplicativo. Os arquivos com mais de um ano são apagados automaticamente, e o registro só sai do computador ou aparelho se você mesmo o enviar.

O histórico, os rascunhos e as cópias de segurança contêm os dados que você digitou, que podem incluir nomes, salários e datas. Proteja o acesso ao computador ou aparelho e guarde as cópias e os relatórios em locais de sua confiança.

## 3. Quando o aplicativo acessa a internet

O aplicativo só acessa a internet para:

- **Atualizar as tabelas:** na abertura, uma vez por dia, se a opção estiver ligada em Configurações, e quando você usa os botões de atualização. São lidas páginas e serviços públicos, como gov.br, Receita Federal, Planalto, IBGE, Banco Central e Ipeadata, e sites alternativos de tabelas, como debit.com.br, contabeis.com.br, idinheiro.com.br, vriconsulting.com.br e cajud.com.br.
- **Verificar novas versões (Windows):** na abertura, se a opção **Avisar sobre novas versões** estiver ligada, o aplicativo consulta no GitHub a última versão publicada. Ao confirmar a atualização, o instalador é baixado do GitHub e conferido antes de ser executado.
- **Abrir links:** links para o projeto, para a página de versões e para páginas oficiais abrem no navegador, que segue as políticas do site visitado.

Nessas consultas, nenhum dado dos cálculos, do histórico ou das preferências é enviado. Como em qualquer acesso à internet, os sites consultados recebem informações técnicas da conexão, como o endereço IP, e o aplicativo se identifica pelo nome e pela versão. O tratamento dessas informações segue a política de cada site. No Windows, com as duas opções de internet desligadas, o aplicativo não acessa a internet ao abrir; no Android, isso vale com a atualização ao abrir desligada.

## 4. Compartilhamento por sua iniciativa

PDFs, planilhas, cópias de segurança do histórico e o registro de erros só são gravados ou compartilhados quando você escolhe a ação. No Android, o compartilhamento usa o menu do sistema, e o destino escolhido, como e-mail, Drive ou WhatsApp, segue a política do respectivo serviço. O aplicativo não envia esses arquivos por conta própria.

## 5. Permissões no Android

- **Internet:** para atualizar as tabelas.
- **Vibração:** para o retorno tátil curto ao calcular e ao remover ou desfazer linhas.

O aplicativo não pede acesso a contatos, localização, câmera, microfone ou arquivos pessoais. A escolha de um arquivo para restaurar uma cópia de segurança é feita pelo seletor do próprio Android. O backup automático do Android está desativado para o aplicativo, para que o histórico não seja copiado para a nuvem sem a sua decisão.

## 6. Seus dados, seu controle

Como os dados ficam apenas com você, consultar, corrigir ou apagar é feito no próprio aplicativo: os cálculos são excluídos no Histórico, as tabelas são editadas na tela de cada uma, e as preferências são alteradas em Configurações. Para remover tudo, desinstale o aplicativo; no Windows, apague também a pasta de instalação, onde fica o banco, e a pasta `%LOCALAPPDATA%\CalculoIRRF`.

Como o autor não recebe os dados digitados, não há dados pessoais dos usuários sob a guarda dele para consultar, corrigir ou apagar nos termos da Lei Geral de Proteção de Dados (Lei 13.709/2018). Quem digitar dados de terceiros, como empregados ou clientes, é responsável pelo tratamento desses dados.

## 7. Contato

Dúvidas sobre esta política podem ser enviadas pelas issues do projeto no GitHub: <https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues>. Não inclua dados pessoais na mensagem.
