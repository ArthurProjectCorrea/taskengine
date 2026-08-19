# Especificação de Requisitos de Software (ERS)

**Projeto:** TaskEngine — Módulo de Tarefas
**Cliente/Órgão:** Uso pessoal/interno (Arthur)
**Data:** 19/08/2026
**Versão:** 1.0

## Histórico de Revisões

| Versão | Data       | Autor  | Descrição das Alterações                                                                                                              |
| ------ | ---------- | ------ | ---------------------------------------------------------------------------------------------------------------------------------------|
| 1.0    | 19/08/2026 | Arthur | Criação do documento inicial, consolidando o escopo da V1 discutido antes da versão final do protótipo visual.                        |
| 1.1    | 19/08/2026 | Arthur | Revisão a partir da primeira versão navegável do protótipo: adiciona RF-016 (relatório geral consolidado, distinto do relatório por tarefa do RF-010/011) e esclarece que qualquer status fora de "em andamento"/"concluído" pausa o registro, sem exceções por nome de status. |
| 1.2    | 19/08/2026 | Arthur | Revisão a partir da versão final do protótipo (`prototipo/`): RF-016 passa a permitir filtro opcional por período (CA-016.3). Demais telas confirmadas sem novos requisitos funcionais. |

---

## 1. Introdução

_(Em conformidade com a norma ISO/IEC/IEEE 29148)_

### 1.1. Propósito do Documento

Este documento descreve as especificações de requisitos de software para o módulo de Tarefas do TaskEngine. O objetivo é fornecer uma visão clara, precisa e testável de todas as funcionalidades e restrições desse módulo, servindo como base técnica de referência independente de linguagem ou tecnologia de implementação.

### 1.2. Escopo do Produto

O TaskEngine é uma aplicação desktop que atua como ponte de sincronização entre o usuário e um provedor externo de gestão de tarefas (ex.: GitHub, ClickUp, Jira), permitindo acompanhar e registrar o tempo investido nas tarefas atribuídas ao usuário, sem duplicar a criação/gestão de tarefas que já acontece no provedor.

O que o sistema fará:

- Sincronizar (puxar) as tarefas atribuídas ao usuário a partir do provedor conectado, excluindo tarefas já concluídas antes da conexão.
- Exibir as tarefas sincronizadas em uma lista consolidada e em um painel de acompanhamento rápido.
- Permitir iniciar e concluir o acompanhamento de tempo de uma tarefa.
- Calcular e exibir o tempo investido em cada tarefa, separado por origem (humano/IA), além de tempo de expediente e tempo não mapeado.
- Permitir a seleção manual, no momento da conclusão, de quais atividades registradas realmente pertencem à tarefa — definindo o tempo final.
- Sincronizar de volta ao provedor o status e o tempo final de uma tarefa concluída.
- Gerar e exportar um relatório de linha do tempo por tarefa concluída (RF-010/RF-011).
- Gerar e exportar um relatório geral consolidado, com uma linha por tarefa sincronizada e seus tempos totais (RF-016).
- Operar sem conexão à internet, permitindo inclusive a conclusão de tarefas offline (com sincronização pendente até a conexão retornar).
- Congelar a conclusão e a sincronização de tarefas de um provedor cujo acesso tenha sido revogado, mantendo o registro de tempo ativo.
- Pausar e retomar automaticamente o registro de tempo conforme mudanças de status — feitas no sistema ou detectadas no provedor durante a sincronização.

O que o sistema **NÃO** fará (Fora do Escopo):

- Criar ou editar tarefas diretamente no provedor externo a partir do sistema.
- Puxar tarefas que já estavam concluídas no provedor antes da conexão inicial ser estabelecida.
- Alterar dados de uma tarefa já concluída localmente, ou refletir no sistema alterações feitas posteriormente no provedor.
- Utilizar inteligência artificial para gerar ou estruturar conteúdo de tarefas (fora do escopo desta versão).

### 1.3. Agentes (AGT)

| ID      | Agente (AGT)               | Descrição                                                                                          | Nível de Acesso            |
| ------- | --------------------------- | --------------------------------------------------------------------------------------------------- | --------------------------- |
| AGT-001 | Usuário Padrão               | Pessoa que usa o sistema para acompanhar e trabalhar em suas tarefas.                               | Acesso total (local)        |
| AGT-002 | Provedor de Tarefas Externo | Sistema de gestão de tarefas de terceiros de onde as tarefas são obtidas e para onde o progresso é enviado. | Acesso via integração autenticada |
| AGT-003 | Agente de IA                | Ferramenta de inteligência artificial que pode alterar arquivos/executar trabalho em nome do usuário durante o período de uma tarefa. | Acesso indireto (monitorado) |

### 1.4. Definições, Acrônimos e Abreviações

| Termo/Acrônimo       | Definição                                                                                                    |
| --------------------- | -------------------------------------------------------------------------------------------------------------- |
| ERS                   | Especificação de Requisitos de Software.                                                                       |
| Sincronização         | Processo de busca de tarefas atualizadas junto ao provedor externo.                                            |
| Tempo Humano           | Tempo de atividade atribuído à interação direta do usuário.                                                    |
| Tempo de IA            | Tempo de atividade atribuído a um agente de IA.                                                                |
| Tempo de Expediente    | Tempo esperado de trabalho no período da tarefa, conforme configuração de expediente do usuário.               |
| Tempo Não Mapeado      | Tempo declarado manualmente pelo usuário como investido na tarefa fora do computador.                          |
| Período Ativo          | Intervalo de tempo em que a tarefa esteve efetivamente com status "em andamento", sem contar os períodos de pausa. |
| Pausa                  | Intervalo de tempo em que a tarefa deixou de estar "em andamento" sem ter sido concluída — o registro de tempo é interrompido até a tarefa voltar a "em andamento". |
| Tempo Total Investido  | Tempo real de trabalho na tarefa, sem contar duplicado o período em que humano e IA atuaram simultaneamente.   |

### 1.5. Referências

- Roadmap do produto: fase V1 (fluxo manual, sem inteligência artificial atuante e sem sincronização em nuvem).
- Normas aplicadas: ISO/IEC/IEEE 29148.

---

## 2. Descrição Geral do Sistema

### 2.1. Perspectiva do Produto

O módulo de Tarefas é o núcleo funcional do TaskEngine. Ele depende do módulo de Monitoramento para obter os dados de atividade (arquivos e navegação) e do módulo de Configurações para os dados de expediente e a conexão com o provedor externo.

### 2.2. Suposições e Dependências

- **Suposições:** o usuário já conectou pelo menos um provedor de tarefas antes de utilizar este módulo; o computador permanece com o sistema em execução durante o período de trabalho para que a atividade seja capturada; como apenas um provedor é conectado por vez nesta versão (ver módulo de Configurações), o campo Prioridade do Schema-001 — quando vem de um schema dinâmico do provedor — não precisa de normalização entre provedores diferentes ainda; isso será avaliado quando houver suporte a múltiplos provedores simultâneos.
- **Dependências:** disponibilidade do provedor externo para sincronização; módulo de Monitoramento em execução; módulo de Configurações com expediente definido (necessário para o cálculo automático de tempo não mapeado).

---

## 3. Requisitos Funcionais (RF) e Critérios de Aceite (CA)

### Módulo: Tarefas

#### RF-001: Sincronizar tarefas atribuídas ao usuário

- **Descrição:** O sistema deve permitir que o usuário sincronize, sob demanda ou automaticamente, as tarefas atribuídas a ele no provedor conectado, excluindo tarefas que já estavam concluídas antes da conexão.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Regras de Negócio Associadas:** RN-001
- **Eventos Disparados (EVT):** EVT-001 - Sincronização concluída
- **Schema de Dados de Entrada/Saída:** Schema-001 - Tarefa Sincronizada

**Critérios de Aceite (CA):**

- **CA-001.1 - Sincronização bem-sucedida**
  - Dado que o agente [AGT-001] tem um provedor conectado
  - Quando ele aciona a sincronização
  - Então o sistema deve buscar e exibir as tarefas atribuídas a ele que ainda não estavam concluídas antes da conexão.
- **CA-001.2 - Provedor indisponível**
  - Dado que o provedor externo está indisponível
  - Quando o agente [AGT-001] aciona a sincronização
  - Então o sistema deve exibir uma mensagem de erro e manter os dados já existentes inalterados.

#### RF-002: Sincronizar automaticamente em intervalo periódico

- **Descrição:** O sistema deve sincronizar automaticamente as tarefas em um intervalo de tempo configurável (padrão: a cada 1 hora), sem exigir ação do usuário.
- **Agente(s) (AGT):** AGT-002
- **Regras de Negócio Associadas:** RN-002
- **Eventos Disparados (EVT):** EVT-001

**Critérios de Aceite (CA):**

- **CA-002.1 - Sincronização automática executada**
  - Dado que o sistema está em execução
  - Quando o intervalo de sincronização automática é atingido
  - Então o sistema deve sincronizar as tarefas sem interromper o uso do agente [AGT-001].

#### RF-003: Buscar e listar tarefas sincronizadas

- **Descrição:** O sistema deve permitir localizar tarefas por busca textual e visualizar todas as tarefas sincronizadas em uma lista com suas informações essenciais.
- **Agente(s) (AGT):** AGT-001

**Critérios de Aceite (CA):**

- **CA-003.1 - Busca filtra corretamente**
  - Dado que existem tarefas sincronizadas
  - Quando o agente [AGT-001] digita um termo de busca
  - Então a lista deve exibir apenas as tarefas correspondentes ao termo.

#### RF-004: Iniciar acompanhamento de tempo de uma tarefa

- **Descrição:** O sistema deve permitir que o usuário inicie o registro de tempo de uma tarefa que ainda não foi iniciada. Para retomar uma tarefa já pausada, ver RF-015.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-003
- **Eventos Disparados (EVT):** EVT-002 - Tarefa iniciada

**Critérios de Aceite (CA):**

- **CA-004.1 - Início bem-sucedido**
  - Dado que uma tarefa está com status "a fazer"
  - Quando o agente [AGT-001] inicia o acompanhamento
  - Então o status da tarefa deve mudar para "em andamento" e o sistema deve passar a registrar o tempo.
- **CA-004.2 - Tarefa já em andamento**
  - Dado que uma tarefa já está com status "em andamento"
  - Quando o agente [AGT-001] tenta iniciá-la novamente
  - Então o sistema deve impedir a ação e informar que já existe um acompanhamento aberto para essa tarefa.

#### RF-005: Visualizar progresso em tempo real de uma tarefa em andamento

- **Descrição:** O sistema deve exibir, enquanto a tarefa está em andamento, o tempo decorrido e os valores estimados de tempo humano, de IA, de expediente e não mapeado, atualizados continuamente.
- **Agente(s) (AGT):** AGT-001
- **Schema de Dados de Entrada/Saída:** Schema-001

**Critérios de Aceite (CA):**

- **CA-005.1 - Painel de progresso atualizado**
  - Dado que uma tarefa está em andamento
  - Quando o agente [AGT-001] visualiza o painel de progresso
  - Então os valores exibidos devem refletir a atividade registrada até o momento.

#### RF-006: Registrar tempo não mapeado manualmente

- **Descrição:** O sistema deve permitir que o usuário registre, a qualquer momento enquanto a tarefa está aberta, um período de tempo trabalhado fora do computador, informando data/horário de início, duração e justificativa.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-004
- **Eventos Disparados (EVT):** EVT-004 - Tempo não mapeado registrado
- **Schema de Dados de Entrada/Saída:** Schema-002 - Registro de Tempo Não Mapeado

**Critérios de Aceite (CA):**

- **CA-006.1 - Registro bem-sucedido**
  - Dado que uma tarefa está em andamento
  - Quando o agente [AGT-001] informa início, duração e justificativa e confirma
  - Então o sistema deve registrar esse tempo associado à tarefa.
- **CA-006.2 - Justificativa ausente**
  - Dado que o agente [AGT-001] não informa uma justificativa
  - Quando ele tenta confirmar o registro
  - Então o sistema deve impedir o registro e solicitar a justificativa.

#### RF-007: Concluir tarefa com seleção de atividades relevantes

- **Descrição:** Ao concluir uma tarefa, o sistema deve apresentar uma lista de todos os arquivos e páginas de navegador alterados/visitados durante os períodos ativos da tarefa (excluindo qualquer período de pausa, ver RF-015), permitindo ao usuário selecionar quais realmente pertencem à tarefa. O tempo final de humano/IA é calculado somente com base nos itens selecionados.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-005, RN-007, RN-012
- **Eventos Disparados (EVT):** EVT-003 - Tarefa concluída
- **Schema de Dados de Entrada/Saída:** Schema-003 - Item de Atividade

**Critérios de Aceite (CA):**

- **CA-007.1 - Conclusão com seleção**
  - Dado que o agente [AGT-001] seleciona os itens de atividade relevantes
  - Quando ele confirma a conclusão
  - Então o sistema deve calcular o tempo final de humano/IA com base apenas nos itens selecionados, mudar o status da tarefa para concluída e disparar a sincronização com o provedor.
- **CA-007.2 - Conclusão sem nenhum item selecionado**
  - Dado que o agente [AGT-001] não seleciona nenhum item de atividade
  - Quando ele tenta concluir a tarefa
  - Então o sistema deve alertar que nenhum tempo humano/IA será contabilizado e solicitar confirmação explícita antes de prosseguir.

#### RF-008: Congelar tarefa concluída

- **Descrição:** Após a conclusão, a tarefa não pode mais ser reaberta, ter tempo adicionado ou qualquer dado alterado dentro do sistema. Qualquer alteração posterior só pode ocorrer diretamente no provedor, e não é refletida de volta ao sistema.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-006

**Critérios de Aceite (CA):**

- **CA-008.1 - Nenhuma ação disponível**
  - Dado que uma tarefa está concluída
  - Quando o agente [AGT-001] acessa seus detalhes
  - Então nenhuma ação de edição, reabertura ou adição de tempo deve estar disponível.

#### RF-009: Sincronizar status e tempo final com o provedor

- **Descrição:** Ao concluir uma tarefa, o sistema deve atualizar o status e registrar o tempo total investido no provedor externo.
- **Agente(s) (AGT):** AGT-002

**Critérios de Aceite (CA):**

- **CA-009.1 - Sincronização de conclusão bem-sucedida**
  - Dado que uma tarefa foi concluída localmente
  - Quando o sistema executa a sincronização com o provedor
  - Então o status e o tempo devem ser atualizados no provedor.
- **CA-009.2 - Provedor indisponível na conclusão**
  - Dado que o provedor está indisponível no momento da conclusão
  - Quando o sistema tenta sincronizar
  - Então a conclusão deve ser mantida localmente e o sistema deve informar que a sincronização não foi concluída.

#### RF-010: Visualizar relatório de linha do tempo de tarefa concluída

- **Descrição:** Para tarefas concluídas, o sistema deve exibir uma visualização temporal mostrando quando cada item de atividade selecionado foi trabalhado, incluindo períodos sobrepostos entre itens diferentes.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-007
- **Schema de Dados de Entrada/Saída:** Schema-003

**Critérios de Aceite (CA):**

- **CA-010.1 - Visualização com sobreposição preservada**
  - Dado que uma tarefa está concluída
  - Quando o agente [AGT-001] acessa o relatório de linha do tempo
  - Então a visualização deve exibir cada item com seu respectivo período de atividade, preservando sobreposições entre itens distintos.

#### RF-011: Exportar relatório de linha do tempo

- **Descrição:** O sistema deve permitir exportar o relatório de linha do tempo de uma tarefa concluída em formato de planilha (CSV/Excel).
- **Agente(s) (AGT):** AGT-001

**Critérios de Aceite (CA):**

- **CA-011.1 - Exportação bem-sucedida**
  - Dado que o agente [AGT-001] está visualizando o relatório de uma tarefa concluída
  - Quando ele aciona a exportação
  - Então o sistema deve gerar um arquivo no formato selecionado contendo os dados exibidos.

#### RF-012: Acessar a tarefa diretamente no provedor

- **Descrição:** O sistema deve permitir que o usuário abra, em seu navegador padrão, a página da tarefa no provedor externo, para consultar detalhes que o sistema não replica.
- **Agente(s) (AGT):** AGT-001

**Critérios de Aceite (CA):**

- **CA-012.1 - Abertura do link do provedor**
  - Dado que uma tarefa sincronizada existe
  - Quando o agente [AGT-001] aciona o link do provedor
  - Então o navegador padrão do sistema operacional deve abrir na página correspondente da tarefa.

#### RF-013: Congelar tarefas quando o acesso ao provedor é revogado

- **Descrição:** Se o acesso a um provedor for revogado — seja pela desconexão explícita do usuário, seja por revogação feita diretamente no próprio provedor — o sistema deve bloquear a sincronização e a conclusão de todas as tarefas daquele provedor. O tempo continua sendo registrado normalmente e o registro manual de tempo não mapeado continua disponível; apenas a conclusão e a sincronização ficam bloqueadas até o provedor ser reconectado.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Regras de Negócio Associadas:** RN-008

**Critérios de Aceite (CA):**

- **CA-013.1 - Bloqueio ao detectar acesso revogado**
  - Dado que o acesso a um provedor conectado foi revogado
  - Quando o sistema tenta sincronizar ou o usuário tenta concluir uma tarefa desse provedor
  - Então o sistema deve impedir a ação, manter a tarefa como está e informar que o provedor precisa ser reconectado.
- **CA-013.2 - Tempo continua sendo registrado**
  - Dado que o acesso ao provedor de uma tarefa em andamento foi revogado
  - Quando o tempo de trabalho continua sendo monitorado
  - Então o sistema deve continuar registrando o tempo e permitir o registro manual de tempo não mapeado normalmente.

#### RF-014: Concluir tarefa offline com sincronização pendente

- **Descrição:** O sistema deve funcionar sem conexão à internet, exceto pelas operações que dependem diretamente do provedor externo (sincronização de tarefas, envio de conclusão). A conclusão de uma tarefa é permitida mesmo offline; nesse caso, a tarefa fica com status de "pendente de sincronização" até que a conexão seja restabelecida e o envio ao provedor seja concluído com sucesso.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Regras de Negócio Associadas:** RN-009
- **Eventos Disparados (EVT):** EVT-005 - Conclusão pendente de sincronização

**Critérios de Aceite (CA):**

- **CA-014.1 - Conclusão offline aceita**
  - Dado que o computador está sem conexão à internet
  - Quando o agente [AGT-001] conclui uma tarefa
  - Então o sistema deve congelar a tarefa localmente com status "pendente de sincronização", sem bloquear a conclusão.
- **CA-014.2 - Sincronização retomada ao reconectar**
  - Dado que existe uma tarefa concluída localmente com status "pendente de sincronização"
  - Quando a conexão à internet é restabelecida
  - Então o sistema deve enviar o status e o tempo final ao provedor e atualizar a tarefa para "sincronizada".

#### RF-015: Pausar e retomar o acompanhamento de tempo

- **Descrição:** As opções de status de uma tarefa vêm do provedor conectado, não são fixas no sistema. Qualquer status diferente de "em andamento" e de "concluído", assumido por uma tarefa que já teve um registro de início, deve pausar o registro de tempo — interrompendo a contagem até a tarefa voltar para "em andamento" (retomada). Isso evita que uma tarefa pausada (ex.: bloqueada, aguardando revisão, voltou para a fila, cancelada) continue contando tempo indevidamente. Não há tratamento especial por nome de status — inclusive um status como "cancelada" é tratado como qualquer outra pausa: o tempo para de contar, mas se o status da tarefa mudar de novo para "em andamento" (no sistema ou no provedor), o registro é retomado normalmente. A marcação de status pode partir tanto do sistema quanto do provedor: se a sincronização identificar que o status mudou no provedor para algo diferente de "em andamento"/"concluído", o sistema deve criar o registro de pausa correspondente automaticamente.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Regras de Negócio Associadas:** RN-010, RN-011, RN-012
- **Eventos Disparados (EVT):** EVT-006 - Tarefa pausada, EVT-007 - Tarefa retomada
- **Schema de Dados de Entrada/Saída:** Schema-004 - Período de Acompanhamento

**Critérios de Aceite (CA):**

- **CA-015.1 - Pausa por mudança de status no próprio sistema**
  - Dado que uma tarefa está "em andamento"
  - Quando o agente [AGT-001] muda o status dela para qualquer valor diferente de "em andamento" ou "concluído"
  - Então o sistema deve encerrar o período ativo corrente e parar de contar tempo para essa tarefa.
- **CA-015.2 - Pausa detectada na sincronização**
  - Dado que uma tarefa está "em andamento" no sistema
  - Quando a sincronização identifica que o status no provedor mudou para algo diferente de "em andamento"/"concluído"
  - Então o sistema deve registrar a pausa localmente, com a mesma consequência de uma pausa feita diretamente no sistema.
- **CA-015.3 - Retomada**
  - Dado que uma tarefa está pausada
  - Quando o status dela volta para "em andamento" (no sistema ou detectado via sincronização)
  - Então o sistema deve abrir um novo período ativo e voltar a contar tempo normalmente.

#### RF-016: Gerar e exportar relatório geral consolidado de tarefas

- **Descrição:** O sistema deve permitir gerar um relatório com uma linha por tarefa sincronizada, contendo início, fim, provedor, tempo humano, tempo de IA, tempo de expediente, tempo não mapeado (com as respectivas justificativas) e o tempo total investido sem duplicidade. O usuário pode opcionalmente informar um período (data inicial e final) para restringir quais tarefas entram no relatório, exportando todas as tarefas sincronizadas quando nenhum período é informado. Diferente do relatório do RF-010/RF-011 (que detalha, para uma única tarefa concluída, a linha do tempo de cada arquivo/página trabalhado), este relatório é uma visão geral entre tarefas e é gerado exclusivamente a partir dos dados já salvos localmente, sem consultar o provedor no momento da geração.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-007, RN-013
- **Schema de Dados de Entrada/Saída:** Schema-005 - Linha de Relatório Geral

**Critérios de Aceite (CA):**

- **CA-016.1 - Geração e exportação bem-sucedida**
  - Dado que existe ao menos uma tarefa sincronizada
  - Quando o agente [AGT-001] gera o relatório geral e exporta em CSV/Excel
  - Então o sistema deve gerar um arquivo com uma linha por tarefa, contendo os tempos calculados a partir dos dados locais.
- **CA-016.2 - Nenhuma tarefa sincronizada**
  - Dado que nenhuma tarefa foi sincronizada ainda
  - Quando o agente [AGT-001] tenta gerar o relatório geral
  - Então o sistema deve informar que não há dados para o relatório, sem gerar um arquivo vazio.
- **CA-016.3 - Filtragem por período**
  - Dado que o agente [AGT-001] informa uma data inicial e final antes de exportar
  - Quando ele confirma a exportação
  - Então o relatório deve conter apenas as tarefas cujo período se enquadra no intervalo informado.

---

## 4. Requisitos Não Funcionais (RNF)

| ID      | Categoria     | Descrição do Requisito                                                                       | Métrica/Critério de Teste                                                                     |
| ------- | ------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| RNF-001 | Desempenho    | A sincronização automática não deve prejudicar o desempenho do computador do usuário.           | Impacto perceptível de CPU/memória mínimo durante a sincronização em segundo plano.               |
| RNF-002 | Segurança     | Nenhum dado de uma tarefa concluída deve ser alterável após a conclusão.                        | Bloqueio de edição garantido tanto na interface quanto nas regras internas do sistema.             |
| RNF-003 | Confiabilidade | Os dados locais devem permanecer consistentes mesmo se a sincronização com o provedor falhar.  | Nenhuma perda de dados de tempo já registrados localmente em caso de falha de sincronização.       |
| RNF-004 | Isolamento    | Dados de usuários diferentes do sistema operacional no mesmo computador não podem se misturar.  | Cada conta operacional distinta deve ter seu conjunto de dados totalmente separado.                |

---

## 5. Regras de Negócio (RN)

| ID     | Título da Regra                              | Descrição                                                                                                                                                                       |
| ------ | ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RN-001 | Exclusão de tarefas pré-concluídas             | Tarefas já concluídas no provedor antes da conexão inicial não são sincronizadas para o sistema.                                                                                |
| RN-002 | Intervalo padrão de sincronização automática   | A sincronização automática ocorre a cada 1 hora por padrão, complementável por sincronização manual a qualquer momento.                                                        |
| RN-003 | Um acompanhamento por tarefa                   | Uma tarefa não pode ter mais de um período de acompanhamento de tempo aberto simultaneamente.                                                                                  |
| RN-004 | Justificativa obrigatória                      | Todo registro de tempo não mapeado exige uma justificativa textual.                                                                                                             |
| RN-005 | Tempo final depende da seleção na conclusão    | O tempo humano/IA definitivo de uma tarefa é calculado exclusivamente com base nos itens selecionados no momento da conclusão, podendo ser menor que o valor estimado durante o andamento. |
| RN-006 | Imutabilidade pós-conclusão                    | Uma tarefa concluída não pode ser reaberta, alterada ou receber novos registros de tempo dentro do sistema.                                                                    |
| RN-007 | Tempo total sem duplicidade                    | Quando atividade humana e de IA ocorrem no mesmo período (sobreposição), o tempo total investido considera esse período uma única vez; a divisão por origem continua disponível separadamente para fins de relatório. |
| RN-008 | Congelamento por acesso revogado               | Se o acesso a um provedor for revogado (pelo usuário ou externamente pelo provedor), as tarefas desse provedor não podem ser concluídas nem sincronizadas até a reconexão; o tempo continua sendo registrado normalmente. |
| RN-009 | Funcionamento offline                          | O sistema deve operar sem conexão à internet, exceto pelas operações que dependem diretamente do provedor externo. A conclusão de uma tarefa offline é permitida e fica pendente de sincronização até a conexão ser restabelecida. |
| RN-010 | Status vem do provedor                         | As opções de status de uma tarefa são definidas pelo provedor conectado, não são fixas no sistema.                                                                                                                             |
| RN-011 | Qualquer status fora de "em andamento"/"concluído" pausa | Uma tarefa que já teve um registro de início e assume qualquer status diferente de "em andamento" e de "concluído" tem seu registro de tempo pausado, até retornar para "em andamento". Vale para qualquer status vindo do provedor (ex.: pausada, bloqueada, cancelada) — não há exceção por nome de status. |
| RN-012 | Período de pausa excluído do cálculo final     | O tempo e os itens de atividade considerados na conclusão de uma tarefa consideram apenas os períodos ativos, excluindo qualquer intervalo em que a tarefa esteve pausada.                                                     |
| RN-013 | Relatório geral usa apenas dados locais        | O relatório geral consolidado (RF-016) é gerado exclusivamente a partir dos dados já salvos localmente, sem consultar o provedor no momento da geração — mesmo princípio do relatório por tarefa (RF-010/011).                 |

---

## 6. Eventos do Sistema (EVT)

| ID      | Evento (EVT)                    | Gatilho (O que causa o evento)                                    | Ação / Consequência                                                                    |
| ------- | ---------------------------------| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| EVT-001 | Sincronização concluída          | Sincronização manual ou automática chega ao fim.                      | Atualiza a lista de tarefas exibida e registra o horário da última sincronização.          |
| EVT-002 | Tarefa iniciada                  | Agente [AGT-001] inicia o acompanhamento de uma tarefa.               | Inicia o registro contínuo de tempo para aquela tarefa.                                    |
| EVT-003 | Tarefa concluída                 | Agente [AGT-001] confirma a conclusão de uma tarefa.                  | Congela o registro local e dispara a sincronização de status/tempo com o provedor.          |
| EVT-004 | Tempo não mapeado registrado     | Agente [AGT-001] confirma um registro manual de tempo offline.        | Associa a entrada de tempo à tarefa correspondente.                                        |
| EVT-005 | Conclusão pendente de sincronização | Agente [AGT-001] conclui uma tarefa sem conexão à internet.        | Marca a tarefa como concluída localmente com status "pendente de sincronização", a ser enviada ao provedor assim que a conexão for restabelecida. |
| EVT-006 | Tarefa pausada                   | Status da tarefa muda para algo diferente de "em andamento"/"concluído", no sistema ou detectado via sincronização com o provedor. | Encerra o período ativo corrente; o tempo deixa de ser contado para essa tarefa. |
| EVT-007 | Tarefa retomada                  | Status da tarefa volta para "em andamento", no sistema ou detectado via sincronização com o provedor. | Abre um novo período ativo; o tempo volta a ser contado para essa tarefa.        |

---

## 7. Schemas de Dados (Estruturação)

_Descrição funcional dos dados manipulados pelo módulo — sem notação de código, para manter o documento independente de tecnologia._

### Schema-001: Tarefa Sincronizada

**Descrição:** Informações de uma tarefa exibida no sistema, obtidas a partir do provedor externo.

| Campo                          | Tipo                                      | Descrição                                                              |
| ------------------------------- | ------------------------------------------ | ------------------------------------------------------------------------- |
| Identificador                   | Código único                                | Identifica a tarefa de forma inequívoca dentro do sistema.                |
| Título                          | Texto                                       | Nome da tarefa, conforme cadastrado no provedor.                          |
| Provedor de origem               | Texto/categoria                             | Qual provedor externo é a origem da tarefa.                               |
| Prioridade                      | Texto/categoria (quando disponível)         | Prioridade atribuída à tarefa no provedor.                                |
| Status                          | Categoria: a fazer, em andamento, concluída, concluída (pendente de sincronização) | Estado atual da tarefa dentro do sistema. |
| Data de criação                 | Data                                        | Quando a tarefa foi criada no provedor.                                   |
| Data de início do acompanhamento | Data (opcional)                             | Quando o usuário iniciou o acompanhamento de tempo dentro do sistema.     |
| Data de conclusão               | Data (opcional)                             | Quando a tarefa foi concluída dentro do sistema.                          |
| Tempo humano                    | Duração                                     | Tempo total atribuído à interação humana.                                 |
| Tempo de IA                     | Duração                                     | Tempo total atribuído a um agente de IA.                                  |
| Tempo de expediente              | Duração                                     | Tempo de expediente esperado no período da tarefa.                        |
| Tempo não mapeado                | Duração                                     | Tempo declarado manualmente como trabalhado fora do computador.           |
| Link para o provedor            | Endereço externo                            | Endereço da página da tarefa no provedor externo.                         |

### Schema-002: Registro de Tempo Não Mapeado

**Descrição:** Estrutura de um registro manual de tempo trabalhado fora do computador.

| Campo               | Tipo             | Descrição                                          |
| --------------------- | ------------------ | ----------------------------------------------------- |
| Tarefa associada       | Referência          | Tarefa à qual o registro pertence.                     |
| Data/hora de início    | Data e hora         | Quando o tempo declarado efetivamente ocorreu.         |
| Duração                | Duração             | Quanto tempo foi investido.                            |
| Justificativa          | Texto curto         | Contexto/motivo do tempo declarado.                    |

### Schema-003: Item de Atividade

**Descrição:** Um arquivo ou página de navegador registrado como atividade durante o período de uma tarefa.

| Campo                   | Tipo                        | Descrição                                                       |
| ------------------------- | ----------------------------- | -------------------------------------------------------------------|
| Tarefa associada           | Referência                    | Tarefa à qual a atividade pertence.                                |
| Tipo                       | Categoria: arquivo, navegador | Natureza do item de atividade.                                     |
| Caminho/endereço            | Texto                          | Caminho do arquivo ou endereço da página visitada.                  |
| Origem                      | Categoria: humano, IA          | Quem gerou a atividade.                                             |
| Data/hora de início          | Data e hora                    | Quando a atividade começou.                                        |
| Data/hora de fim              | Data e hora                    | Quando a atividade terminou.                                       |
| Selecionado na conclusão    | Sim/Não                        | Se o item foi marcado como pertencente à tarefa no momento da conclusão. |

### Schema-004: Período de Acompanhamento

**Descrição:** Um intervalo de tempo em que uma tarefa esteve ativa ou pausada, delimitando quando o registro de tempo estava ligado ou desligado para ela.

| Campo               | Tipo                        | Descrição                                                        |
| --------------------- | ----------------------------- | --------------------------------------------------------------------|
| Tarefa associada       | Referência                    | Tarefa à qual o período pertence.                                    |
| Tipo                   | Categoria: ativo, pausa        | Se o período representa tempo contado ou tempo pausado.              |
| Data/hora de início     | Data e hora                    | Quando o período começou.                                           |
| Data/hora de fim         | Data e hora (opcional)         | Quando o período terminou — vazio se o período ainda estiver em curso. |
| Origem da marcação      | Categoria: sistema, provedor    | Se a mudança de status que originou o período veio do sistema ou foi detectada na sincronização com o provedor. |

### Schema-005: Linha de Relatório Geral

**Descrição:** Uma linha do relatório geral consolidado (RF-016), referente a uma única tarefa.

| Campo                          | Tipo         | Descrição                                                          |
| -------------------------------- | -------------- | -----------------------------------------------------------------------|
| Tarefa associada                  | Referência      | Tarefa à qual a linha se refere.                                       |
| Data/hora de início                | Data e hora     | Quando o acompanhamento da tarefa começou.                             |
| Data/hora de fim                    | Data e hora (opcional) | Quando a tarefa foi concluída — vazio se ainda estiver em aberto. |
| Provedor de origem                  | Texto/categoria | Provedor externo de onde a tarefa veio.                                |
| Tempo humano                        | Duração         | Tempo total atribuído à interação humana.                              |
| Tempo de IA                         | Duração         | Tempo total atribuído a um agente de IA.                               |
| Tempo de expediente                 | Duração         | Tempo de expediente esperado no período da tarefa.                     |
| Tempo não mapeado                    | Duração         | Soma dos registros manuais de tempo não mapeado da tarefa.             |
| Justificativas de tempo não mapeado  | Lista de texto  | Justificativas de cada registro manual de tempo não mapeado da tarefa. |
| Tempo total investido               | Duração         | Tempo real de trabalho, sem contar duplicado o período de sobreposição entre humano e IA (RN-007). |

---

## 8. Requisitos de Interfaces Externas

### 8.1. Interfaces de Usuário (UI)

- O sistema seguirá o padrão visual definido no protótipo de referência da V1 (ver Seção 10), buscando aparência nativa do sistema operacional Windows.
- As decisões finas de layout serão validadas na versão final do protótipo, mantendo a estrutura (ordem de componentes, conceito de cada tela) já definida.

### 8.2. Interfaces de Software (APIs e Integrações)

- **Provedor de Tarefas Externo:** o sistema se integra a um provedor externo de gestão de tarefas para obter as tarefas atribuídas ao usuário e enviar de volta status/tempo de tarefas concluídas.
  - **Autenticação:** iniciada pelo próprio usuário através do provedor — sem necessidade de inserir credenciais manualmente no sistema.
  - **Formato de dados:** conforme Schema-001.

---

## 9. Matriz de Rastreabilidade de Requisitos

| ID Requisito | Agente (AGT)     | Regras de Negócio (RN) | Eventos (EVT) | Schema de Dados | Critérios de Aceite (CA) |
| ------------- | ----------------- | ------------------------ | --------------- | ------------------ | --------------------------- |
| RF-001        | AGT-001, AGT-002  | RN-001                   | EVT-001         | Schema-001         | CA-001.1, CA-001.2          |
| RF-002        | AGT-002           | RN-002                   | EVT-001         | —                  | CA-002.1                    |
| RF-003        | AGT-001           | —                        | —               | —                  | CA-003.1                    |
| RF-004        | AGT-001           | RN-003                   | EVT-002         | —                  | CA-004.1, CA-004.2          |
| RF-005        | AGT-001           | —                        | —               | Schema-001         | CA-005.1                    |
| RF-006        | AGT-001           | RN-004                   | EVT-004         | Schema-002         | CA-006.1, CA-006.2          |
| RF-007        | AGT-001           | RN-005, RN-007           | EVT-003         | Schema-003         | CA-007.1, CA-007.2          |
| RF-008        | AGT-001           | RN-006                   | —               | —                  | CA-008.1                    |
| RF-009        | AGT-002           | —                        | —               | —                  | CA-009.1, CA-009.2          |
| RF-010        | AGT-001           | RN-007                   | —               | Schema-003         | CA-010.1                    |
| RF-011        | AGT-001           | —                        | —               | —                  | CA-011.1                    |
| RF-012        | AGT-001           | —                        | —               | —                  | CA-012.1                    |
| RF-013        | AGT-001, AGT-002  | RN-008                   | —               | —                  | CA-013.1, CA-013.2          |
| RF-014        | AGT-001, AGT-002  | RN-009                   | EVT-005         | —                  | CA-014.1, CA-014.2          |
| RF-015        | AGT-001, AGT-002  | RN-010, RN-011, RN-012   | EVT-006, EVT-007 | Schema-004        | CA-015.1, CA-015.2, CA-015.3 |
| RF-016        | AGT-001           | RN-007, RN-013           | —               | Schema-005         | CA-016.1, CA-016.2, CA-016.3 |

---

## 10. Anexos e Modelos Visuais

- **Anexo A:** Diagrama de Casos de Uso — pendente.
- **Anexo B:** Fluxograma de Processos de Negócio — pendente.
- **Anexo C:** Protótipo de referência da V1 — versão final consolidada, disponível em `prototipo/` na raiz do repositório.
