# Especificação de Requisitos de Software (ERS)

**Projeto:** [Nome do Projeto/Sistema]
**Cliente/Órgão:** [Nome do Cliente]
**Data:** [Data de Criação]
**Versão:** [Versão Atual, ex: 1.1]

## Histórico de Revisões

| Versão | Data       | Autor           | Descrição das Alterações                                                               |
| ------ | ---------- | --------------- | -------------------------------------------------------------------------------------- |
| 1.0    | DD/MM/AAAA | [Nome do Autor] | Criação do documento inicial.                                                          |
| 1.1    | DD/MM/AAAA | [Nome do Autor] | Inclusão de Critérios de Aceite (CA), Eventos (EVT), Agentes (AGT) e Schemas de Dados. |

---

## 1. Introdução

_(Em conformidade com a norma ISO/IEC/IEEE 29148)_

### 1.1. Propósito do Documento

Este documento descreve as especificações de requisitos de software para o [Nome do Sistema]. O objetivo é fornecer uma visão clara, precisa e testável de todas as funcionalidades e restrições do sistema, servindo como base técnica e contratual para as equipes de desenvolvimento, design (UX/UI), testes e stakeholders.

### 1.2. Escopo do Produto

O [Nome do Sistema] é uma aplicação [web/mobile/desktop] destinada a [descrever brevemente o objetivo principal do sistema].

O que o sistema fará:

- [Funcionalidade principal 1]
- [Funcionalidade principal 2]

O que o sistema **NÃO** fará (Fora do Escopo):

- [Restrição de escopo 1]

### 1.3. Agentes (AGT)

Os Agentes representam as entidades (humanas ou sistêmicas) que interagem com o sistema, executando ações ou consumindo informações.

| ID      | Agente (AGT)          | Descrição                                                    | Nível de Acesso  |
| ------- | --------------------- | ------------------------------------------------------------ | ---------------- |
| AGT-001 | Administrador         | Responsável pela gestão global do sistema e configurações.   | Acesso Total     |
| AGT-002 | Usuário Padrão        | Utiliza as funcionalidades diárias da aplicação.             | Acesso Restrito  |
| AGT-003 | Sistema Externo (API) | Consome ou envia dados de forma automatizada via integração. | Acesso via Token |

### 1.4. Definições, Acrônimos e Abreviações

| Termo/Acrônimo     | Definição                                                                  |
| ------------------ | -------------------------------------------------------------------------- |
| ERS                | Especificação de Requisitos de Software.                                   |
| API                | Application Programming Interface - Interface para integração de sistemas. |
| [Termo do Negócio] | [Definição clara do termo para alinhamento de toda a equipe].              |

### 1.5. Referências

- [Referência 1: Documento de Visão, Contrato, Legislação específica]
- Normas aplicadas: ABNT NBR ISO/IEC/IEEE 12207, ABNT NBR ISO/IEC 25030, ISO/IEC/IEEE 29148.

---

## 2. Descrição Geral do Sistema

### 2.1. Perspectiva do Produto

Descreva o contexto do sistema. Ele é um produto independente ou faz parte de um ecossistema maior? Como ele se integra com sistemas legados ou externos?

### 2.2. Suposições e Dependências

- **Suposições:** Fatores assumidos como verdadeiros (ex: "Os usuários possuirão conexão estável à internet").
- **Dependências:** Fatores externos dos quais o projeto depende (ex: "Disponibilidade da API do sistema SIGADOC").

---

## 3. Requisitos Funcionais (RF) e Critérios de Aceite (CA)

_(O que o sistema deve fazer. Baseado na ABNT NBR ISO/IEC/IEEE 12207)_

Os requisitos funcionais detalham o comportamento do sistema. Para garantir testabilidade, cada requisito está associado a Critérios de Aceite (CA) formulados no padrão BDD (Behavior-Driven Development).

### Módulo: [Nome do Módulo, ex: Autenticação]

#### RF-001: [Título Curto do Requisito]

- **Descrição:** O sistema deve permitir que o usuário [ação específica].
- **Agente(s) (AGT):** [AGT-002 - Usuário Padrão]
- **Regras de Negócio Associadas:** [RN-001]
- **Eventos Disparados (EVT):** [EVT-001 - Registro de Login Bem-sucedido]
- **Schema de Dados de Entrada/Saída:** [Schema-001 - Autenticação]

**Critérios de Aceite (CA):**

- **CA-001.1 - [Cenário de Sucesso]**
  - Dado que o agente [AGT-002] está na tela de login
  - Quando ele preenche as credenciais válidas e submete
  - Então o sistema deve autenticar a sessão e redirecionar para o painel principal.
- **CA-001.2 - [Cenário de Falha]**
  - Dado que o agente [AGT-002] está na tela de login
  - Quando ele preenche uma senha incorreta
  - Então o sistema deve exibir a mensagem de erro "Credenciais inválidas" e bloquear o acesso.

_(Repita a estrutura para todos os módulos e funcionalidades do sistema)._

---

## 4. Requisitos Não Funcionais (RNF)

_(Como o sistema deve se comportar. Baseado na ABNT NBR ISO/IEC 25030 e SQuaRE 25000)_

Os requisitos de qualidade devem ser mensuráveis e objetivos.

| ID      | Categoria   | Descrição do Requisito                                            | Métrica/Critério de Teste                                                                                    |
| ------- | ----------- | ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| RNF-001 | Desempenho  | O sistema deve carregar a página inicial rapidamente.             | Tempo de resposta ≤ 3 segundos para 95% das requisições sob carga normal.                                    |
| RNF-002 | Segurança   | Todos os dados em trânsito devem ser criptografados.              | Utilização obrigatória do protocolo HTTPS com TLS 1.2 ou superior.                                           |
| RNF-003 | Usabilidade | O sistema deve ser responsivo e acessível em dispositivos móveis. | Conformidade com as diretrizes WCAG 2.1 (Nível AA) e adaptação fluida em telas a partir de 320px de largura. |

---

## 5. Regras de Negócio (RN)

As regras de negócio definem as políticas, restrições e cálculos inerentes ao domínio da organização, independentes da tecnologia utilizada.

| ID     | Título da Regra    | Descrição                                                                                                                |
| ------ | ------------------ | ------------------------------------------------------------------------------------------------------------------------ |
| RN-001 | Validação de Senha | A senha do usuário deve conter no mínimo 8 caracteres, incluindo uma letra maiúscula, um número e um caractere especial. |
| RN-002 | Prazo de Sessão    | A sessão do usuário deve expirar automaticamente após 30 minutos de inatividade por motivos de segurança.                |

---

## 6. Eventos do Sistema (EVT)

Os Eventos (EVT) representam ocorrências significativas dentro do sistema que desencadeiam ações automáticas, notificações ou registros de log.

| ID      | Evento (EVT)            | Gatilho (O que causa o evento)               | Ação / Consequência                                                            |
| ------- | ----------------------- | -------------------------------------------- | ------------------------------------------------------------------------------ |
| EVT-001 | Login Bem-sucedido      | Agente [AGT-002] autentica-se com sucesso.   | Registra log de acesso (IP, Data, Hora) e inicia sessão.                       |
| EVT-002 | Exportação de Relatório | Agente [AGT-001] solicita exportação em PDF. | Gera o arquivo PDF assincronamente e envia notificação de conclusão.           |
| EVT-003 | Falha Crítica de API    | Sistema perde comunicação com a API externa. | Dispara alerta para a equipe de infraestrutura e registra erro crítico no log. |

---

## 7. Schemas de Dados (Estruturação)

Os Schemas definem a estrutura, os tipos de dados e as restrições das entidades processadas pelo sistema, essenciais para a integração e validação de informações.

### Schema-001: Autenticação de Usuário

**Descrição:** Estrutura de dados esperada para a requisição de login. **Formato:** JSON

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "RequisicaoLogin",
  "type": "object",
  "properties": {
    "email": {
      "type": "string",
      "format": "email",
      "description": "E-mail corporativo do agente."
    },
    "senha": {
      "type": "string",
      "minLength": 8,
      "description": "Senha criptografada no frontend."
    }
  },
  "required": ["email", "senha"]
}
```

### Schema-002: [Nome da Entidade Principal]

**Descrição:** Estrutura do objeto de resposta contendo os dados do relatório. **Formato:** JSON

```json
{
  "type": "object",
  "properties": {
    "id_relatorio": { "type": "integer" },
    "titulo": { "type": "string", "maxLength": 150 },
    "data_geracao": { "type": "string", "format": "date-time" },
    "status": { "type": "string", "enum": ["PENDENTE", "CONCLUIDO", "ERRO"] }
  },
  "required": ["id_relatorio", "titulo", "status"]
}
```

---

## 8. Requisitos de Interfaces Externas

### 8.1. Interfaces de Usuário (UI)

- O sistema seguirá o Design System padrão da instituição.
- As decisões de layout (scroll, botões, cores) serão definidas nos protótipos de alta fidelidade anexados (Ver Seção 10).

### 8.2. Interfaces de Software (APIs e Integrações)

- **Integração 1 (ex: SIGADOC):** O sistema consumirá a API REST do SIGADOC.
  - **Endpoint:** `https://api.sigadoc.mt.gov.br/v1/documentos`
  - **Formato de Dados:** JSON (Ver Schema-002).
  - **Autenticação:** OAuth 2.0 (Token JWT).

---

## 9. Matriz de Rastreabilidade de Requisitos

Para garantir que todos os requisitos sejam implementados e testados, a matriz abaixo relaciona os elementos do sistema.

| ID Requisito | Agente (AGT) | Regras de Negócio (RN) | Eventos (EVT) | Schema de Dados | Critérios de Aceite (CA) |
| ------------ | ------------ | ---------------------- | ------------- | --------------- | ------------------------ |
| RF-001       | AGT-002      | RN-001, RN-002         | EVT-001       | Schema-001      | CA-001.1, CA-001.2       |
| RF-002       | AGT-001      | [RN associada]         | EVT-002       | Schema-002      | CA-002.1                 |

---

## 10. Anexos e Modelos Visuais

_(Nesta seção devem ser inseridos os artefatos visuais que complementam o entendimento dos requisitos)._

- **Anexo A:** Diagrama de Casos de Uso (UML).
- **Anexo B:** Fluxograma de Processos de Negócio (BPMN ou Diagrama de Atividades).
- **Anexo C:** Link para os Wireframes/Protótipos navegáveis (Figma, Adobe XD, etc.).
