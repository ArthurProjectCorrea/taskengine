# TaskEngine

> Super-App / Command Bar desktop para gestão de tarefas e tempo (Humano + IA).

TaskEngine é um utilitário desktop de alta performance, uso individual, no estilo barra flutuante (*Raycast / Spotlight*), que atua como uma **camada de inteligência e produtividade sobre ferramentas de gestão existentes** (Jira, GitHub Projects, ClickUp).

Ele elimina o trabalho burocrático de criar tarefas detalhadas, preencher *timesheets* e registrar manualmente o tempo gasto em cada atividade, oferecendo **criação acelerada por IA** e **rastreamento semântico invisível de tempo de trabalho** — diferenciando o esforço humano da atuação de agentes de IA.

A proposta central é **não substituir** os gerenciadores de projetos das empresas, mas servir como uma ferramenta de auxílio de uso individual que reflete tudo de forma transparente nos provedores oficiais.

## O problema

- **Fricção na criação de tarefas** — abrir a interface web do Jira/ClickUp/GitHub Projects só para detalhar e registrar um card consome foco.
- **Imprecisão no time-tracking** — cronômetros manuais são esquecidos; *timesheets* são preenchidos de forma retroativa e imprecisa.
- **Ponto cego da era da IA** — ferramentas tradicionais não capturam o uso de agentes de IA (Claude Code, Copilot, scripts locais) que alteram arquivos em background sem foco direto de janela por parte do usuário.

## A solução

Uma barra flutuante nativa no desktop que permite:

1. Criar e estruturar tarefas em segundos via prompt de voz/texto.
2. Gerenciar o fluxo de status das tarefas com atalhos globais de teclado, sem trocar de contexto.
3. Rastrear o tempo de trabalho ativo no sistema operacional de forma automatizada.
4. Distinguir o tempo de foco ativo humano da atuação de agentes de IA em arquivos locais.
5. Aprovar (*human-in-the-loop*) e sincronizar os *worklogs* diretamente no provedor corporativo.

## Fluxo end-to-end (MVP 1.0)

```
[1. Criação da Tarefa] (IA ou manual)
        │
        ▼
[2. Disparo & Registro] ──> Cria o card via API no provedor (ex.: Jira)
        │
        ▼
[3. Andamento Local] ──────> Troca de status ──> Reflete no provedor
        │                    (inicia rastreamento invisível de tempo no SO)
        ▼
[4. Encerramento] ─────────> Manual pelo usuário, ou sugerido pela IA
        ▼
[5. Cálculo Automático] ───> Cruza logs de foco (humano) + edições de arquivos (IA)
        ▼
[6. Aprovação / Edição] ───> Modal rápido de confirmação das métricas
        ▼
[7. Sincronização Final] ──> Conclui no provedor + adiciona o worklog de horas
```

## Roadmap: V1 (manual) → V2 (IA) → V3 (nuvem)

O desenvolvimento é dividido em fases:

- **V1 — fluxo manual (fase atual):** criação de tarefa, integração com provedor e monitoramento de tempo funcionando de ponta a ponta **sem nenhuma IA envolvida**. Ao concluir uma tarefa, o usuário revisa manualmente tudo que foi modificado durante o período (arquivos, navegador etc.) e escolhe item por item o que conta para o tempo registrado.
- **V2 — fluxo assistido por IA (só depois da V1 estar 100% funcional):** a IA passa a estruturar tarefas a partir de texto informal e a pré-filtrar/sugerir os itens da tela de revisão, para o usuário só validar em vez de escolher tudo manualmente.
- **V3 — sincronização em nuvem (depois da V2):** guardar métricas/configurações do usuário em nuvem para recuperar dados e logins já configurados em outro computador. Fora de escopo até lá.

A IA é deliberadamente a **última** peça do produto a ser construída, não a primeira — e a nuvem vem depois da IA.

## Escopo fechado do MVP 1.0

- **Provedor de tarefas:** arquitetura **genérica desde o início** (porta/interface na Application, várias implementações possíveis) — não fica hard-coded a um único provedor. Primeira implementação: **GitHub** (mais acessível). Depois de validado, outros provedores (Jira, ClickUp etc.) são adicionados sem reestruturar.
- **Autenticação com o provedor:** login via **OAuth** sempre que o provedor suportar (ex.: "Entrar com GitHub" pelo navegador), em vez de exigir que o usuário gere e cole um token de API manualmente.
- **Provedor de IA (V2, não V1):** API de nuvem (OpenAI / Claude) — entra só depois da V1 estar completa.
- **Ambiente:** single-user / desktop individual — sem backend enterprise multi-usuário, **sem nuvem, sem sincronização entre computadores e sem identificação de usuário único** (V3, fora de escopo). Tudo fica no SQLite local da máquina. Se o usuário abrir o app em outro computador, não tem acesso ao histórico anterior (começa do zero ali); se abrir em dois computadores ao mesmo tempo, cada um conta o tempo de forma independente — esse cenário não é tratado.
- **Monitoramento de atividade:** foco de janela ativa (humano) + eventos de edição no sistema de arquivos local — alimentam a tela de revisão manual da V1; a atribuição automática humano/IA fica para a V2. Quando/como o monitoramento liga e desliga (consultar o provedor pelas tarefas em andamento do usuário logado vs. monitorar continuamente enquanto o app estiver aberto) é uma decisão em aberto — ver issue #11.
- **Dashboard:** painel individual de histórico de tarefas e horas, local à máquina do usuário.

## Stack tecnológica

| Camada | Tecnologia |
|---|---|
| Frontend desktop | .NET MAUI nativo (XAML) — apenas recursos e controles built-in, sem bibliotecas de UI de terceiros |
| Atalhos & janela | Hooks globais de teclado, janela flutuante transparente/*frameless* |
| Monitoramento humano | APIs Win32 (User32) / AppKit — janela ativa |
| Monitoramento de IA | `FileSystemWatcher` (.NET) sobre o diretório do projeto |
| Orquestração de IA | Microsoft Semantic Kernel / `Microsoft.Extensions.AI` |
| Provedor de IA principal | OpenAI API (`gpt-4o-mini`) |
| Provedor de IA alternativo (offline) | LLamaSharp + Phi-3-mini, ou Ollama |
| Armazenamento local | SQLite (`Microsoft.Data.Sqlite` / `sqlite-net-pcl`) |
| Segurança de credenciais | DPAPI (Windows) / Keychain (macOS) |
| Integração com provedores | `HttpClient` + `System.Text.Json` consumindo a API REST do provedor |

Ver detalhes de como isso mapeia para as camadas do backend em [ARCHITECTURE.md](./ARCHITECTURE.md).

## Estrutura do repositório

```
TaskEngine.slnx
src/
  Backend/                        Clean Architecture
    TaskEngine.Domain/            Entidades e regras de negócio puras
    TaskEngine.Application/       Casos de uso, interfaces (ports), DTOs
    TaskEngine.Infrastructure/    SQLite, HTTP dos provedores, IA, SO
    TaskEngine.Api/               Host/apresentação (ASP.NET Core)
  Frontend/
    TaskEngine.Desktop/           App MAUI (barra flutuante, UI, MVVM)
.husky/                           Git hooks (Husky.Net)
```

## Como rodar localmente

Pré-requisitos:

- [.NET SDK 10+](https://dotnet.microsoft.com/download)
- Workload do MAUI para desktop: `dotnet workload install maui-desktop`

Comandos:

```bash
# restaurar e buildar tudo
dotnet restore TaskEngine.slnx
dotnet build TaskEngine.slnx

# rodar o backend (API)
dotnet run --project src/Backend/TaskEngine.Api

# rodar o app desktop (Windows)
dotnet run --project src/Frontend/TaskEngine.Desktop -f net10.0-windows10.0.19041.0
```

## Documentação

- [ARCHITECTURE.md](./ARCHITECTURE.md) — princípios de Clean Architecture, regra de dependência e responsabilidade de cada camada.
- [CONTRIBUTING.md](./CONTRIBUTING.md) — como contribuir, convenção de commits semânticos e checklist de PR.

## Licença

Distribuído sob a licença [MIT](./LICENSE).
