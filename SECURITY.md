# Política de Segurança

## Versões atendidas

Correções de segurança saem sempre na versão mais recente, publicada em [Releases](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/releases). Versões anteriores não recebem correções: atualize pelo instalador, que preserva as tabelas cadastradas.

## O que considerar

O aplicativo roda só no computador do usuário, sem servidor e sem contas. Os pontos mais sensíveis são:

- a **atualização das tabelas pela internet**, que baixa e interpreta páginas HTML e APIs públicas;
- os **arquivos gerados e abertos**: o banco SQLite local, os PDFs, as planilhas do Excel e o histórico de cálculos;
- o **instalador**, que não é assinado digitalmente.

Uma divergência de cálculo não é uma falha de segurança: relate-a no modelo [Erro de cálculo](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues/new?template=erro-de-calculo.yml).

## Como relatar

**Não abra uma issue pública com os detalhes de uma falha.** Abra uma issue no modelo [Pedido de contato privado](https://github.com/mayconwisley/CalculosTrabalhistasTributarios/issues/new?template=contato-privado.yml), sem descrever o problema, e o mantenedor indicará um canal privado para receber o relato.

No relato, informe:

- a versão do aplicativo e do Windows;
- o que a falha permite e os passos para reproduzi-la;
- se possível, uma sugestão de correção.

Você receberá uma resposta assim que possível. Depois de corrigida, a falha é descrita nas notas da versão, com o crédito a quem a relatou, se a pessoa quiser.
