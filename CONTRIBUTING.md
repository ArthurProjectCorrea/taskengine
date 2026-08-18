# Contribuindo

## Princípio arquitetural

Todo código em `src/Backend/**` segue **Clean Architecture** com regra de dependência estrita (`Domain ← Application ← Infrastructure ← Api`). Leia [ARCHITECTURE.md](./ARCHITECTURE.md) antes de abrir um PR. Trabalho de backend deve ser feito com o agente `clean-architecture-guardian`; trabalho de UI/MAUI em `src/Frontend/TaskEngine.Desktop/**` deve ser feito com o agente `maui-frontend` (definidos em `.claude/agents/`).

PRs que violem a regra de dependência (ex.: `Domain` referenciando um pacote de infraestrutura, `Application` chamando `HttpClient` diretamente em vez de um port) serão rejeitados.

## Branches

- `main` — sempre estável e buildável.
- `feature/<descrição-curta>` — novas funcionalidades.
- `fix/<descrição-curta>` — correções de bug.
- `chore/<descrição-curta>` — manutenção, dependências, configuração.

## Commits semânticos

Este projeto usa [Conventional Commits](https://www.conventionalcommits.org/):

```
<tipo>(<escopo opcional>): <descrição curta no imperativo>

[corpo opcional explicando o porquê]
```

Tipos aceitos:

| Tipo | Uso |
|---|---|
| `feat` | nova funcionalidade |
| `fix` | correção de bug |
| `docs` | documentação (README, ARCHITECTURE, comentários) |
| `style` | formatação, sem mudança de comportamento |
| `refactor` | mudança de código sem alterar comportamento externo |
| `perf` | melhoria de performance |
| `test` | adição/ajuste de testes |
| `build` | build system, dependências, csproj |
| `ci` | pipelines de integração contínua |
| `chore` | manutenção geral, tooling |

Escopo sugerido = camada afetada: `domain`, `application`, `infrastructure`, `api`, `desktop`.

Exemplos:

```
feat(application): adiciona caso de uso de criação de tarefa via IA
fix(infrastructure): corrige serialização de worklog no cliente Jira
docs: adiciona ARCHITECTURE.md e CONTRIBUTING.md
```

## Qualidade de código antes do commit

O Husky.Net está configurado (`.husky/task-runner.json`) e roda `dotnet format` automaticamente no `pre-commit` sobre os arquivos `.cs` staged. Para rodar manualmente:

```bash
dotnet husky run --group pre-commit
```

Antes de abrir um PR, garanta que:

- `dotnet build TaskEngine.slnx` builda com **0 erros e 0 avisos**.
- `dotnet format TaskEngine.slnx --verify-no-changes` não acusa diffs pendentes.
- `dotnet test TaskEngine.slnx` passa (lógica de Domain/Application deve ter testes unitários — projetos `tests/*.Tests`).
- Novas dependências não introduzem vulnerabilidades conhecidas (`dotnet list package --vulnerable --include-transitive`).

## Checklist de PR

- [ ] Build limpo (0 erros, 0 avisos).
- [ ] Testes unitários passando (`dotnet test`), com cobertura para as invariantes novas/alteradas.
- [ ] Regra de dependência da Clean Architecture respeitada.
- [ ] Commits seguem Conventional Commits.
- [ ] Código de UI (`TaskEngine.Desktop`) não contém lógica de negócio.
- [ ] Documentação atualizada quando a mudança afeta arquitetura ou fluxo do MVP.
