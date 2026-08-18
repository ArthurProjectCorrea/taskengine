# Arquitetura

O backend do TaskEngine segue **Clean Architecture** (Uncle Bob). O objetivo é manter a lógica de negócio isolada de detalhes técnicos (banco de dados, framework web, SDKs de IA, chamadas HTTP a provedores externos), para que esses detalhes possam mudar sem afetar as regras de negócio.

Esta política é obrigatória para qualquer código adicionado em `src/Backend/**`. Ela é reforçada pelo agente [`clean-architecture-guardian`](./.claude/agents/clean-architecture-guardian.md), que deve ser usado em qualquer tarefa de implementação, revisão ou refatoração nessa árvore.

## Regra de dependência

O código fonte só pode depender de camadas mais internas — nunca o contrário:

```
TaskEngine.Domain            (núcleo — não depende de nada)
      ▲
TaskEngine.Application       (depende de Domain)
      ▲
TaskEngine.Infrastructure    (depende de Application e Domain)
      ▲
TaskEngine.Api               (depende de Application e Infrastructure)

TaskEngine.Desktop           (frontend MAUI — camada de apresentação separada;
                               consome Application/Api, nunca o contrário)
```

Nenhum projeto interno pode referenciar um projeto mais externo. `TaskEngine.Domain` não pode referenciar `Microsoft.EntityFrameworkCore`, `Microsoft.Data.Sqlite`, `HttpClient` ou qualquer pacote de infraestrutura.

## Responsabilidade de cada camada

### `TaskEngine.Domain`

Entidades e regras de negócio puras (ex.: `Task`, `WorkSession`, `TimeLog`). Sem dependência de nenhum pacote externo além do BCL. Sem atributos de ORM, sem `[JsonPropertyName]`, sem referência a frameworks.

### `TaskEngine.Application`

Casos de uso (*use cases* / *application services*) que orquestram entidades do Domain para resolver uma operação do sistema (ex.: `CriarTarefaUseCase`, `EncerrarSessaoDeTrabalhoUseCase`, `SincronizarWorklogUseCase`).

Define **interfaces (ports)** para tudo que é externo — ex.: `ITaskProviderClient`, `ITimeTrackingRepository`, `IAiTaskStructurer`, `ISecretStore` — implementadas depois em Infrastructure. A Application nunca sabe *como* algo é persistido ou chamado via rede, só declara o contrato.

`ITaskProviderClient` (e afins) é desenhado desde o início para **múltiplas implementações**: o provedor de tarefas não é fixo no código — a primeira implementação é o GitHub, mas o contrato precisa comportar outros provedores (Jira, ClickUp etc.) depois, sem reestruturar a Application. Ver [ISSUES_PLAN.md]/issues #8 e #22 para o racional (arquitetura genérica + login OAuth em vez de token manual).

DTOs de entrada/saída dos casos de uso vivem aqui, separados das entidades de Domain.

### `TaskEngine.Infrastructure`

Implementações concretas dos ports definidos em Application:

- Persistência local em **SQLite** (`Microsoft.Data.Sqlite` / `sqlite-net-pcl`).
- Clientes HTTP para os provedores de tarefas — primeira implementação: GitHub, via `HttpClient` + `System.Text.Json`, com autenticação OAuth. Outros provedores (Jira, ClickUp etc.) são adicionados depois, implementando o mesmo port.
- Integração com IA (Semantic Kernel / `Microsoft.Extensions.AI`, provedor OpenAI `gpt-4o-mini`, com espaço para um provedor offline via LLamaSharp/Ollama).
- Monitoramento de SO: foco de janela ativa (Win32 `User32` / AppKit) e `FileSystemWatcher` para detectar edições de agentes de IA.
- Armazenamento seguro de credenciais via DPAPI (Windows) / Keychain (macOS).

### `TaskEngine.Api`

Camada de apresentação/host (ASP.NET Core Web API com controllers). Expõe os casos de uso da Application, faz o *wiring* de injeção de dependência (registrando as implementações de Infrastructure contra as interfaces de Application) e trata concerns HTTP (validação de request, serialização, status codes).

### `TaskEngine.Desktop` (frontend)

App MAUI — a barra flutuante em si. Consome a Application (diretamente via referência de projeto, ou via `TaskEngine.Api`, a depender da feature) e não deve conter lógica de negócio: apenas apresentação, view models (MVVM) e código específico de plataforma (`Platforms/Windows`, `Platforms/MacCatalyst`). Padrão reforçado pelo agente [`maui-frontend`](./.claude/agents/maui-frontend.md).

## Estrutura de pastas

```
TaskEngine.slnx
src/
  Backend/
    TaskEngine.Domain/
    TaskEngine.Application/
    TaskEngine.Infrastructure/
    TaskEngine.Api/
  Frontend/
    TaskEngine.Desktop/
```

## Adicionando uma nova feature

1. Modele/ajuste as entidades necessárias em `Domain` (sem dependências externas).
2. Crie o caso de uso em `Application`, declarando os ports (`interface`) que ele precisa.
3. Implemente os ports em `Infrastructure`.
4. Exponha o caso de uso via endpoint em `Api` (registrando a implementação de Infrastructure no DI) e/ou via tela em `Desktop`.
5. Rode `dotnet build TaskEngine.slnx` — o build deve permanecer com **0 erros e 0 avisos**.

## Qualidade e automação

- `dotnet format` roda automaticamente no pre-commit via Husky.Net (`.husky/task-runner.json`).
- Warnings de vulnerabilidade de pacotes (`dotnet list package --vulnerable`) devem ser resolvidos antes do merge.
- Veja [CONTRIBUTING.md](./CONTRIBUTING.md) para convenção de commits e checklist de PR.
