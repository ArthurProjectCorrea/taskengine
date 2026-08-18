# Contribuindo

## Princípio arquitetural

Todo código em `src/Backend/**` segue **Clean Architecture** com regra de dependência estrita (`Domain ← Application ← Infrastructure ← Api`). Leia [ARCHITECTURE.md](./ARCHITECTURE.md) antes de abrir um PR. Trabalho de backend deve ser feito com o agente `clean-architecture-guardian`; trabalho de UI/MAUI em `src/Frontend/TaskEngine.Desktop/**` deve ser feito com o agente `maui-frontend` (definidos em `.claude/agents/`).

PRs que violem a regra de dependência (ex.: `Domain` referenciando um pacote de infraestrutura, `Application` chamando `HttpClient` diretamente em vez de um port) serão rejeitados.

## Branches

O roadmap é fasado em **V1 → V2 → V3** (ver README.md). Cada fase tem sua própria branch de integração de longa duração, criada a partir da issue guarda-chuva da fase (ex.: `28-v1-fluxo-manual-completo-tarefa-provedor-tracking-humano` para a V1):

- `main` — sempre estável e buildável; só recebe merge da branch de versão ativa quando ela está **funcionalmente completa**. Todo push em `main` dispara o [release semântico](#release-semântico).
- `<n>-v1-...` / `<n>-v2-...` / `<n>-v3-...` — branch de integração da fase, uma por versão do roadmap.
- `<n>-<slug>` — branch de uma issue individual (criada com `gh issue develop <n> --checkout`), sempre a partir da branch de versão ativa, **nunca a partir de `main`**. O PR dessa branch aponta para a branch de versão, não para `main`.

Fluxo por issue: `gh issue develop <n> --checkout` (a partir da branch de versão) → implementar → commit com `Closes #<n>` → PR `(#<n>)` apontando para a branch de versão → merge. Só quando a versão inteira estiver pronta é que a branch de versão vira PR para `main`.

## Release semântico

`.github/workflows/release.yml` roda em todo push em `main`. Ele calcula a próxima versão a partir dos [Conventional Commits](#commits-semânticos) desde a última tag (`feat` → minor, `fix` → patch, `BREAKING CHANGE` → major; nenhum dos dois → nenhuma release), cria a tag, publica `TaskEngine.Desktop` (win-x64, self-contained) e anexa o artefato compactado a uma GitHub Release. Sem Node.js/npm — só GitHub Actions e `dotnet publish`, consistente com a política de não introduzir toolchains extras.

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
- Novas dependências não introduzem vulnerabilidades conhecidas (`dotnet list package --vulnerable --include-transitive`).

## Checklist de PR

- [ ] Build limpo (0 erros, 0 avisos).
- [ ] Regra de dependência da Clean Architecture respeitada.
- [ ] Commits seguem Conventional Commits.
- [ ] Código de UI (`TaskEngine.Desktop`) não contém lógica de negócio.
- [ ] Documentação atualizada quando a mudança afeta arquitetura ou fluxo do MVP.
