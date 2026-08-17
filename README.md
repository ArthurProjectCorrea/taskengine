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

## Escopo fechado do MVP 1.0

- **Provedor de tarefas integrado (1 apenas):** Jira, GitHub Projects ou ClickUp.
- **Provedor de IA:** API de nuvem (OpenAI / Claude).
- **Ambiente:** single-user / desktop individual — sem backend enterprise multi-usuário.
- **Monitoramento de atividade:** foco de janela ativa (humano) + eventos de edição no sistema de arquivos local (agentes de IA).
- **Dashboard:** painel individual de histórico de tarefas e horas, local à máquina do usuário.

## Stack tecnológica

| Camada | Tecnologia |
|---|---|
| Frontend desktop | .NET MAUI (XAML) + UraniumUI + Fluent Design (WinUI 3 / Mac Catalyst) |
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
