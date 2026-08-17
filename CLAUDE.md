# TaskEngine — instruções para Claude Code

Contexto completo do produto: [README.md](./README.md). Regras de arquitetura: [ARCHITECTURE.md](./ARCHITECTURE.md). Convenções de commit/PR: [CONTRIBUTING.md](./CONTRIBUTING.md).

## Regra inegociável

O backend (`src/Backend/**`) segue Clean Architecture com regra de dependência estrita:

```
TaskEngine.Domain ← TaskEngine.Application ← TaskEngine.Infrastructure ← TaskEngine.Api
```

Nunca referencie uma camada mais externa a partir de uma mais interna (ex.: `Domain` nunca depende de `Infrastructure`).

## Delegação para agentes especializados

- Qualquer tarefa em `src/Backend/**` → use o agente `clean-architecture-guardian`.
- Qualquer tarefa em `src/Frontend/TaskEngine.Desktop/**` → use o agente `maui-frontend`.

## Antes de considerar uma tarefa concluída

- `dotnet build TaskEngine.slnx` com 0 erros e 0 avisos.
- `dotnet format` limpo (hook de pre-commit do Husky.Net cobre isso).
- Commit seguindo Conventional Commits (ver CONTRIBUTING.md).
