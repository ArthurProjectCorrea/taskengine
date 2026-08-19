# Especificação de Requisitos de Software (ERS)

**Projeto:** TaskEngine — Módulo de Configurações
**Cliente/Órgão:** Uso pessoal/interno (Arthur)
**Data:** 19/08/2026
**Versão:** 1.0

## Histórico de Revisões

| Versão | Data       | Autor  | Descrição das Alterações                                                                                                       |
| ------ | ---------- | ------ | ----------------------------------------------------------------------------------------------------------------------------- |
| 1.0    | 19/08/2026 | Arthur | Criação do documento inicial, consolidando o escopo da V1 discutido antes da versão final do protótipo visual.                |
| 1.1    | 19/08/2026 | Arthur | Revisão a partir da versão final do protótipo (`docs/prototipo/`): esclarece que o relatório geral consolidado (módulo de Tarefas, RF-016) é acessado a partir desta tela. Nenhum requisito novo próprio deste módulo. |

---

## 1. Introdução

_(Em conformidade com a norma ISO/IEC/IEEE 29148)_

### 1.1. Propósito do Documento

Este documento descreve as especificações de requisitos de software para o módulo de Configurações do TaskEngine. O objetivo é fornecer uma visão clara, precisa e testável de todas as funcionalidades e restrições desse módulo, servindo como base técnica de referência independente de linguagem ou tecnologia de implementação.

### 1.2. Escopo do Produto

O módulo de Configurações concentra tudo que não faz parte do fluxo diário de acompanhamento de tarefas: conexão com o provedor externo, definição do expediente de trabalho, backup/restauração dos dados locais e informações sobre o funcionamento do sistema.

O que o sistema fará:

- Permitir a conexão inicial (e reconexão) com um provedor externo de tarefas, sem exigir inserção manual de credenciais.
- Bloquear o acesso ao restante do sistema até que ao menos um provedor esteja conectado.
- Permitir configurar o expediente de trabalho do usuário (dias da semana, horário de início/fim, intervalo de almoço).
- Permitir exportar um backup dos dados locais e importar um backup para restaurar/migrar os dados.
- Dar acesso, a partir desta tela, ao relatório geral consolidado de tarefas (módulo de Tarefas, RF-016), com filtro opcional por período.
- Exibir o nome do usuário atualmente logado no sistema operacional.
- Exibir informações sobre o funcionamento, limites e política de dados/segurança do sistema.

O que o sistema **NÃO** fará (Fora do Escopo):

- Permitir múltiplos provedores conectados simultaneamente nesta versão.
- Incluir credenciais de acesso ao provedor externo no arquivo de backup.
- Sincronizar configurações entre computadores diferentes automaticamente (a migração ocorre apenas via backup/restauração manual).

### 1.3. Agentes (AGT)

| ID      | Agente (AGT)               | Descrição                                                                       | Nível de Acesso                  |
| ------- | --------------------------- | ---------------------------------------------------------------------------------- | ----------------------------------- |
| AGT-001 | Usuário Padrão               | Pessoa que configura e utiliza o sistema.                                          | Acesso total (local)                |
| AGT-002 | Provedor de Tarefas Externo | Sistema de gestão de tarefas de terceiros ao qual o sistema se conecta.            | Acesso via integração autenticada    |
| AGT-003 | Sistema Operacional          | Fornece a identificação do usuário atualmente logado no computador.                | Fonte de informação, sem interação direta |

### 1.4. Definições, Acrônimos e Abreviações

| Termo/Acrônimo | Definição                                                                                          |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| ERS                | Especificação de Requisitos de Software.                                                              |
| Expediente         | Período de trabalho do usuário (dias, horário de início/fim, intervalo de almoço).                    |
| Backup             | Arquivo contendo os dados locais do sistema (tarefas, histórico e configurações), sem credenciais.     |

### 1.5. Referências

- Roadmap do produto: fase V1 (fluxo manual, sem nuvem — configurações e dados residem exclusivamente na máquina do usuário).
- Normas aplicadas: ISO/IEC/IEEE 29148.

---

## 2. Descrição Geral do Sistema

### 2.1. Perspectiva do Produto

O módulo de Configurações é um módulo de apoio: não participa do fluxo diário de acompanhamento de tarefas, mas fornece os dados (expediente, conexão com o provedor) dos quais o módulo de Tarefas depende, além das funções de continuidade dos dados (backup/restauração).

### 2.2. Suposições e Dependências

- **Suposições:** o computador do usuário permite acesso à internet no momento da conexão com o provedor; cada conta do sistema operacional corresponde a uma pessoa/uso distinto.
- **Dependências:** disponibilidade do provedor externo para autenticação; sistema operacional Windows para identificação do usuário logado e para os mecanismos de proteção de dados locais.

---

## 3. Requisitos Funcionais (RF) e Critérios de Aceite (CA)

### Módulo: Configurações

#### RF-001: Conectar provedor de tarefas

- **Descrição:** O sistema deve permitir que o usuário autentique e conecte um provedor externo de tarefas através do próprio provedor, sem inserir credenciais manualmente no sistema. Enquanto nenhum provedor estiver conectado, o acesso às demais funcionalidades do sistema deve ficar bloqueado.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Regras de Negócio Associadas:** RN-001
- **Eventos Disparados (EVT):** EVT-001 - Provedor conectado

**Critérios de Aceite (CA):**

- **CA-001.1 - Conexão bem-sucedida**
  - Dado que o agente [AGT-001] não possui nenhum provedor conectado
  - Quando ele conclui a autenticação com o provedor externo
  - Então o sistema deve liberar o acesso às demais funcionalidades.
- **CA-001.2 - Autorização negada ou cancelada**
  - Dado que o agente [AGT-001] inicia a conexão com um provedor
  - Quando ele cancela ou nega a autorização
  - Então o sistema deve exibir uma mensagem informativa e permanecer na tela de conexão.

#### RF-002: Configurar expediente de trabalho

- **Descrição:** O sistema deve permitir definir os dias da semana trabalhados, o horário de início e término do expediente, e o intervalo de almoço.
- **Agente(s) (AGT):** AGT-001
- **Schema de Dados de Entrada/Saída:** Schema-001 - Configuração de Expediente

**Critérios de Aceite (CA):**

- **CA-002.1 - Configuração salva com sucesso**
  - Dado que o agente [AGT-001] informa dias da semana, horário de início/fim e intervalo de almoço válidos
  - Quando ele confirma
  - Então o sistema deve salvar a configuração e utilizá-la nos cálculos de tempo de expediente.
- **CA-002.2 - Horários inválidos**
  - Dado que o horário de término é anterior ao horário de início, ou o intervalo de almoço está fora do expediente informado
  - Quando o agente [AGT-001] tenta confirmar
  - Então o sistema deve rejeitar a configuração e informar o motivo.

#### RF-003: Exportar backup dos dados locais

- **Descrição:** O sistema deve permitir gerar um arquivo de backup contendo as tarefas, o histórico de tempo e as configurações do usuário, excluindo as credenciais de acesso ao provedor.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-002
- **Eventos Disparados (EVT):** EVT-002 - Backup exportado
- **Schema de Dados de Entrada/Saída:** Schema-002 - Arquivo de Backup

**Critérios de Aceite (CA):**

- **CA-003.1 - Exportação bem-sucedida**
  - Dado que o agente [AGT-001] aciona a exportação de backup
  - Quando ele escolhe um local válido para salvar
  - Então o sistema deve gerar o arquivo de backup nesse local.
- **CA-003.2 - Local de destino inválido**
  - Dado que o local escolhido para salvar não é acessível (ex.: sem espaço em disco)
  - Quando o agente [AGT-001] confirma a exportação
  - Então o sistema deve exibir uma mensagem de erro e não gerar um arquivo parcial/corrompido.

#### RF-004: Importar backup

- **Descrição:** O sistema deve permitir restaurar os dados a partir de um arquivo de backup válido, sobrescrevendo integralmente os dados locais existentes. Após a importação, o usuário precisa reconectar o provedor, já que credenciais não fazem parte do backup.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-002, RN-003
- **Eventos Disparados (EVT):** EVT-003 - Backup importado
- **Schema de Dados de Entrada/Saída:** Schema-002

**Critérios de Aceite (CA):**

- **CA-004.1 - Importação bem-sucedida**
  - Dado que o agente [AGT-001] seleciona um arquivo de backup válido
  - Quando ele confirma a importação
  - Então o sistema deve substituir os dados locais pelos dados do backup e solicitar a reconexão do provedor.
- **CA-004.2 - Arquivo de backup incompatível**
  - Dado que o arquivo selecionado é inválido, corrompido ou de um formato de backup incompatível
  - Quando o agente [AGT-001] tenta importá-lo
  - Então o sistema deve rejeitar a importação e manter os dados atuais intactos.

#### RF-005: Exibir identificação do usuário do sistema operacional

- **Descrição:** O sistema deve exibir o nome do usuário atualmente logado no sistema operacional, de forma que fique visível a quem pertencem os dados exibidos — especialmente relevante em computadores compartilhados por múltiplas contas.
- **Agente(s) (AGT):** AGT-001, AGT-003
- **Regras de Negócio Associadas:** RN-004

**Critérios de Aceite (CA):**

- **CA-005.1 - Identificação visível**
  - Dado que o agente [AGT-001] abre o sistema
  - Quando ele acessa a tela principal
  - Então o nome do usuário do sistema operacional deve estar visível.

#### RF-006: Exibir informações sobre o sistema

- **Descrição:** O sistema deve apresentar uma seção explicando o que o sistema faz, o que não faz, e sua política de dados/segurança.
- **Agente(s) (AGT):** AGT-001

**Critérios de Aceite (CA):**

- **CA-006.1 - Seção acessível**
  - Dado que o agente [AGT-001] acessa a seção "Sobre"
  - Quando a tela é exibida
  - Então as informações sobre funcionamento, limites e política de dados devem estar visíveis.

#### RF-007: Desconectar (revogar acesso a) um provedor

- **Descrição:** O sistema deve permitir que o usuário desconecte um provedor previamente conectado. Ao desconectar — ou ao detectar que o acesso foi revogado diretamente no próprio provedor —, as tarefas daquele provedor passam a ficar congeladas para conclusão e sincronização (ver módulo de Tarefas, RF-013), sem afetar o tempo já em registro.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Regras de Negócio Associadas:** RN-005
- **Eventos Disparados (EVT):** EVT-004 - Provedor desconectado

**Critérios de Aceite (CA):**

- **CA-007.1 - Desconexão bem-sucedida**
  - Dado que o agente [AGT-001] tem um provedor conectado
  - Quando ele confirma a desconexão
  - Então o sistema deve remover o acesso local e passar a bloquear sincronização/conclusão das tarefas daquele provedor.
- **CA-007.2 - Revogação detectada externamente**
  - Dado que o acesso foi revogado diretamente no provedor (fora do sistema)
  - Quando o sistema tenta usar esse acesso e recebe uma recusa do provedor
  - Então o sistema deve tratar o provedor como desconectado, com o mesmo efeito de uma desconexão manual.

---

## 4. Requisitos Não Funcionais (RNF)

| ID      | Categoria   | Descrição do Requisito                                                             | Métrica/Critério de Teste                                                              |
| ------- | ----------- | -------------------------------------------------------------------------------------| -------------------------------------------------------------------------------------- |
| RNF-001 | Segurança   | Credenciais de acesso ao provedor nunca devem ser incluídas em um arquivo de backup. | Verificação de que nenhum campo de credencial está presente no arquivo exportado.       |
| RNF-002 | Segurança   | Dados de usuários diferentes do sistema operacional não podem se misturar.           | Cada conta operacional distinta deve ter seu conjunto de dados totalmente separado.     |
| RNF-003 | Usabilidade | A conexão com o provedor não deve exigir conhecimento técnico do usuário.            | Fluxo de conexão limitado a autenticação via navegador, sem campos de token manual.     |

---

## 5. Regras de Negócio (RN)

| ID     | Título da Regra                                    | Descrição                                                                                                          |
| ------ | ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| RN-001 | Bloqueio sem provedor conectado                       | O sistema não libera nenhuma funcionalidade além da conexão de provedor enquanto nenhum provedor estiver conectado. |
| RN-002 | Backup nunca inclui credenciais                       | O arquivo de backup exportado nunca contém credenciais de acesso ao provedor externo.                              |
| RN-003 | Importação sobrescreve integralmente                  | A importação de um backup substitui por completo os dados locais existentes, sem mesclagem.                        |
| RN-004 | Isolamento por usuário do sistema operacional         | Dados de diferentes usuários do sistema operacional no mesmo computador nunca são compartilhados entre si.         |
| RN-005 | Desconexão congela tarefas do provedor                | Ao desconectar um provedor (manualmente ou por revogação externa), as tarefas desse provedor ficam impedidas de ser concluídas ou sincronizadas até a reconexão — ver módulo de Tarefas, RN-008. |

---

## 6. Eventos do Sistema (EVT)

| ID      | Evento (EVT)          | Gatilho (O que causa o evento)                                | Ação / Consequência                                                    |
| ------- | ----------------------| ------------------------------------------------------------------| ---------------------------------------------------------------------- |
| EVT-001 | Provedor conectado     | Agente [AGT-001] conclui a autenticação com um provedor externo.  | Libera o acesso ao restante do sistema.                                |
| EVT-002 | Backup exportado       | Agente [AGT-001] confirma a exportação de um backup.               | Registra a data/hora do último backup realizado.                        |
| EVT-003 | Backup importado       | Agente [AGT-001] confirma a importação de um backup válido.        | Substitui os dados locais e reinicia a necessidade de conexão com o provedor. |
| EVT-004 | Provedor desconectado  | Agente [AGT-001] desconecta um provedor, ou o sistema detecta revogação externa. | Congela as tarefas desse provedor para conclusão/sincronização, sem afetar o tempo já em registro. |

---

## 7. Schemas de Dados (Estruturação)

_Descrição funcional dos dados manipulados pelo módulo — sem notação de código, para manter o documento independente de tecnologia._

### Schema-001: Configuração de Expediente

| Campo                             | Tipo               | Descrição                                       |
| ------------------------------------ | --------------------- | -----------------------------------------------------|
| Dias da semana trabalhados            | Lista de categorias    | Quais dias da semana fazem parte do expediente.       |
| Horário de início                     | Horário                | Início do expediente diário.                          |
| Horário de término                    | Horário                | Fim do expediente diário.                              |
| Horário de início do intervalo         | Horário (opcional)     | Início do intervalo de almoço.                          |
| Horário de término do intervalo        | Horário (opcional)     | Fim do intervalo de almoço.                             |

### Schema-002: Arquivo de Backup

| Campo                        | Tipo             | Descrição                                                         |
| ------------------------------- | ------------------- | ---------------------------------------------------------------------|
| Dados de tarefas                 | Coleção              | Tarefas sincronizadas e seu histórico de tempo.                       |
| Configurações                    | Coleção              | Expediente e demais preferências do usuário.                          |
| Identificador de versão do formato | Código               | Permite ao sistema validar a compatibilidade do backup na importação. |
| Data de geração                  | Data                 | Quando o backup foi criado.                                          |

---

## 8. Requisitos de Interfaces Externas

### 8.1. Interfaces de Usuário (UI)

- O sistema seguirá o padrão visual definido no protótipo de referência da V1 (ver Seção 10), buscando aparência nativa do sistema operacional Windows.

### 8.2. Interfaces de Software (APIs e Integrações)

- **Provedor de Tarefas Externo:** mesma integração descrita no módulo de Tarefas — autenticação iniciada pelo usuário através do provedor, sem inserção manual de credenciais no sistema.
- **Sistema Operacional:** consulta ao usuário atualmente logado, para fins de identificação e isolamento de dados.

---

## 9. Matriz de Rastreabilidade de Requisitos

| ID Requisito | Agente (AGT)     | Regras de Negócio (RN) | Eventos (EVT) | Schema de Dados | Critérios de Aceite (CA) |
| ------------- | ----------------- | ------------------------ | --------------- | ------------------ | --------------------------- |
| RF-001        | AGT-001, AGT-002  | RN-001                   | EVT-001         | —                  | CA-001.1, CA-001.2          |
| RF-002        | AGT-001           | —                        | —               | Schema-001         | CA-002.1, CA-002.2          |
| RF-003        | AGT-001           | RN-002                   | EVT-002         | Schema-002         | CA-003.1, CA-003.2          |
| RF-004        | AGT-001           | RN-002, RN-003           | EVT-003         | Schema-002         | CA-004.1, CA-004.2          |
| RF-005        | AGT-001, AGT-003  | RN-004                   | —               | —                  | CA-005.1                    |
| RF-006        | AGT-001           | —                        | —               | —                  | CA-006.1                    |
| RF-007        | AGT-001, AGT-002  | RN-005                   | EVT-004         | —                  | CA-007.1, CA-007.2          |

---

## 10. Anexos e Modelos Visuais

- **Anexo A:** Diagrama de Casos de Uso — pendente.
- **Anexo B:** Fluxograma de Processos de Negócio — pendente.
- **Anexo C:** Protótipo de referência da V1 — versão final consolidada, disponível em `docs/prototipo/`.
