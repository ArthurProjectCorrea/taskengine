# Especificação de Requisitos de Software (ERS)

**Projeto:** TaskEngine — Módulo de Monitoramento
**Cliente/Órgão:** Uso pessoal/interno (Arthur)
**Data:** 19/08/2026
**Versão:** 1.0

## Histórico de Revisões

| Versão | Data       | Autor  | Descrição das Alterações                                                                                                       |
| ------ | ---------- | ------ | ----------------------------------------------------------------------------------------------------------------------------- |
| 1.0    | 19/08/2026 | Arthur | Criação do documento inicial, consolidando o escopo da V1 discutido antes da versão final do protótipo visual.                |

---

## 1. Introdução

_(Em conformidade com a norma ISO/IEC/IEEE 29148)_

### 1.1. Propósito do Documento

Este documento descreve as especificações de requisitos de software para o módulo de Monitoramento do TaskEngine. O objetivo é fornecer uma visão clara, precisa e testável de todas as funcionalidades e restrições desse módulo, servindo como base técnica de referência independente de linguagem ou tecnologia de implementação.

### 1.2. Escopo do Produto

O módulo de Monitoramento é um módulo oculto (sem interface própria visível ao usuário) responsável por observar continuamente a atividade do computador enquanto o sistema estiver em execução, independentemente de existir uma tarefa em andamento no momento. Esse histórico de atividade alimenta o módulo de Tarefas.

O que o sistema fará:

- Monitorar continuamente, enquanto o sistema estiver em execução, as alterações de arquivos realizadas no computador.
- Monitorar continuamente a atividade de navegação (páginas/abas visitadas).
- Classificar a origem de cada atividade registrada como humana ou de um agente de inteligência artificial.
- Manter esse histórico de atividade disponível para consulta posterior por qualquer tarefa — inclusive tarefas cujo período de trabalho é anterior à criação/registro da própria tarefa no sistema.

O que o sistema **NÃO** fará (Fora do Escopo):

- Interromper o monitoramento pelo simples fato de não haver nenhuma tarefa em andamento no momento.
- Enviar os dados monitorados para qualquer destino fora do computador do usuário, exceto quando explicitamente associados a uma tarefa concluída pelo usuário.

### 1.3. Agentes (AGT)

| ID      | Agente (AGT)     | Descrição                                                                                | Nível de Acesso                          |
| ------- | ------------------ | -------------------------------------------------------------------------------------------- | -------------------------------------------- |
| AGT-001 | Usuário Padrão     | Pessoa cuja interação direta com arquivos e navegador gera atividade classificada como humana. | Fonte de atividade monitorada                |
| AGT-002 | Agente de IA       | Ferramenta de inteligência artificial cuja atuação sobre arquivos gera atividade classificada como IA. | Fonte de atividade monitorada                |
| AGT-003 | Sistema Operacional | Fornece ao módulo as informações de arquivos alterados e janelas/abas ativas.                | Fonte de informação, sem interação direta    |

### 1.4. Definições, Acrônimos e Abreviações

| Termo/Acrônimo     | Definição                                                                                  |
| --------------------- | ----------------------------------------------------------------------------------------------|
| ERS                    | Especificação de Requisitos de Software.                                                       |
| Atividade              | Um período de tempo em que um arquivo foi alterado ou uma página de navegador esteve em foco.  |
| Origem da atividade    | Classificação de uma atividade como humana ou de agente de IA.                                 |

### 1.5. Referências

- Roadmap do produto: fase V1 (fluxo manual). A classificação de origem (humano/IA) já é necessária nesta fase porque o usuário pode acionar um agente de IA para trabalhar concorrentemente com ele em uma mesma tarefa.
- Normas aplicadas: ISO/IEC/IEEE 29148.

---

## 2. Descrição Geral do Sistema

### 2.1. Perspectiva do Produto

O módulo de Monitoramento é um módulo de infraestrutura interna, sem tela própria. Ele roda continuamente em segundo plano enquanto o sistema estiver em execução e alimenta o módulo de Tarefas com o histórico de atividade necessário para calcular tempo investido e para o checklist de conclusão de tarefas.

### 2.2. Suposições e Dependências

- **Suposições:** o sistema permanece em execução (mesmo residente em segundo plano) durante o período em que o usuário deseja que a atividade seja capturada; o monitoramento é local, sem depender de conexão com a internet.
- **Dependências:** recursos do sistema operacional para observar alterações de arquivos e atividade de navegação.

---

## 3. Requisitos Funcionais (RF) e Critérios de Aceite (CA)

### Módulo: Monitoramento

#### RF-001: Monitorar continuamente alterações de arquivos

- **Descrição:** Enquanto o sistema estiver em execução, deve registrar toda alteração de arquivo realizada no computador, independentemente de existir uma tarefa em andamento no momento.
- **Agente(s) (AGT):** AGT-001, AGT-002, AGT-003
- **Regras de Negócio Associadas:** RN-001
- **Eventos Disparados (EVT):** EVT-001 - Alteração de arquivo detectada
- **Schema de Dados de Entrada/Saída:** Schema-001 - Registro de Atividade

**Critérios de Aceite (CA):**

- **CA-001.1 - Alteração registrada**
  - Dado que o sistema está em execução
  - Quando um arquivo é alterado no computador
  - Então o sistema deve registrar o caminho do arquivo, o período da alteração e a origem.
- **CA-001.2 - Nenhuma tarefa em andamento**
  - Dado que não existe nenhuma tarefa em andamento no momento
  - Quando um arquivo é alterado
  - Então o sistema deve registrar a atividade normalmente, sem exigir uma tarefa ativa.

#### RF-002: Monitorar continuamente atividade de navegador

- **Descrição:** Enquanto o sistema estiver em execução, deve registrar as páginas/abas de navegador visitadas, independentemente de existir uma tarefa em andamento no momento.
- **Agente(s) (AGT):** AGT-001, AGT-003
- **Regras de Negócio Associadas:** RN-001
- **Eventos Disparados (EVT):** EVT-002 - Atividade de navegador detectada
- **Schema de Dados de Entrada/Saída:** Schema-001

**Critérios de Aceite (CA):**

- **CA-002.1 - Atividade de navegador registrada**
  - Dado que o sistema está em execução
  - Quando uma página de navegador é visitada
  - Então o sistema deve registrar o endereço, o período de atividade e a origem.

#### RF-003: Classificar a origem da atividade

- **Descrição:** Toda atividade registrada deve ser classificada como originada por interação humana direta ou por um agente de inteligência artificial.
- **Agente(s) (AGT):** AGT-001, AGT-002
- **Schema de Dados de Entrada/Saída:** Schema-001

**Critérios de Aceite (CA):**

- **CA-003.1 - Classificação registrada**
  - Dado que uma atividade é detectada
  - Quando o sistema identifica sua origem
  - Então a atividade deve ser marcada como humana ou como de IA.

#### RF-004: Disponibilizar histórico de atividade para associação retroativa a tarefas

- **Descrição:** O sistema deve permitir que, ao registrar ou concluir uma tarefa, sejam recuperadas as atividades monitoradas dentro do período informado para aquela tarefa — mesmo que a tarefa tenha sido criada no provedor ou reconhecida pelo sistema em um momento posterior ao período de trabalho real.
- **Agente(s) (AGT):** AGT-001
- **Regras de Negócio Associadas:** RN-001
- **Schema de Dados de Entrada/Saída:** Schema-001

**Critérios de Aceite (CA):**

- **CA-004.1 - Recuperação retroativa de atividade**
  - Dado que existem atividades monitoradas em um período anterior à criação/registro de uma tarefa no sistema
  - Quando essa tarefa é registrada informando um período que cobre essas atividades
  - Então o sistema deve recuperar e disponibilizar as atividades correspondentes a esse período.

---

## 4. Requisitos Não Funcionais (RNF)

| ID      | Categoria    | Descrição do Requisito                                                                      | Métrica/Critério de Teste                                                             |
| ------- | ------------ | ------------------------------------------------------------------------------------------------| ----------------------------------------------------------------------------------------|
| RNF-001 | Desempenho   | O monitoramento contínuo não deve gerar impacto perceptível no uso normal do computador.        | Consumo de CPU/memória do monitoramento mantido em nível mínimo durante uso contínuo.    |
| RNF-002 | Privacidade  | Os dados monitorados permanecem exclusivamente no computador do usuário até serem associados a uma tarefa concluída. | Nenhuma transmissão de dados de atividade ocorre fora do fluxo de sincronização de tarefas concluídas. |

---

## 5. Regras de Negócio (RN)

| ID     | Título da Regra                                | Descrição                                                                                                                     |
| ------ | -------------------------------------------------| ---------------------------------------------------------------------------------------------------------------------------- |
| RN-001 | Monitoramento contínuo e independente de tarefa   | O monitoramento ocorre sempre que o sistema estiver em execução, independentemente de existir uma tarefa em andamento no momento. |
| RN-002 | Retenção sem envio automático                     | Atividades monitoradas não são enviadas a nenhum provedor externo enquanto não forem associadas a uma tarefa concluída pelo usuário. |

---

## 6. Eventos do Sistema (EVT)

| ID      | Evento (EVT)                    | Gatilho (O que causa o evento)                     | Ação / Consequência                                          |
| ------- | ---------------------------------| -------------------------------------------------------| ------------------------------------------------------------------|
| EVT-001 | Alteração de arquivo detectada    | Um arquivo é criado, editado ou removido no computador. | Registra a atividade no histórico local, com origem classificada.  |
| EVT-002 | Atividade de navegador detectada  | Uma página/aba de navegador entra em foco.              | Registra a atividade no histórico local, com origem classificada.  |

---

## 7. Schemas de Dados (Estruturação)

_Descrição funcional dos dados manipulados pelo módulo — sem notação de código, para manter o documento independente de tecnologia._

### Schema-001: Registro de Atividade

| Campo                | Tipo                            | Descrição                                                  |
| ----------------------- | ---------------------------------- | ----------------------------------------------------------------|
| Tipo                     | Categoria: arquivo, navegador       | Natureza da atividade registrada.                                |
| Caminho/endereço          | Texto                               | Caminho do arquivo ou endereço da página visitada.                |
| Origem                    | Categoria: humano, IA               | Quem gerou a atividade.                                          |
| Data/hora de início        | Data e hora                         | Quando a atividade começou.                                      |
| Data/hora de fim            | Data e hora                         | Quando a atividade terminou.                                     |

---

## 8. Requisitos de Interfaces Externas

### 8.1. Interfaces de Usuário (UI)

- Este módulo não possui interface visual própria — é executado inteiramente em segundo plano.

### 8.2. Interfaces de Software (APIs e Integrações)

- Nenhuma integração externa direta. A fonte de dados é o próprio sistema operacional do computador do usuário.

---

## 9. Matriz de Rastreabilidade de Requisitos

| ID Requisito | Agente (AGT)              | Regras de Negócio (RN) | Eventos (EVT) | Schema de Dados | Critérios de Aceite (CA) |
| ------------- | --------------------------- | ------------------------ | --------------- | ------------------ | --------------------------- |
| RF-001        | AGT-001, AGT-002, AGT-003   | RN-001                   | EVT-001         | Schema-001         | CA-001.1, CA-001.2          |
| RF-002        | AGT-001, AGT-003            | RN-001                   | EVT-002         | Schema-001         | CA-002.1                    |
| RF-003        | AGT-001, AGT-002            | —                        | —               | Schema-001         | CA-003.1                    |
| RF-004        | AGT-001                     | RN-001                   | —               | Schema-001         | CA-004.1                    |

---

## 10. Anexos e Modelos Visuais

- **Anexo A:** Diagrama de Casos de Uso — pendente.
- **Anexo B:** Fluxograma de Processos de Negócio — pendente.
- **Anexo C:** Protótipo de referência da V1 — não aplicável a este módulo (sem interface própria).
