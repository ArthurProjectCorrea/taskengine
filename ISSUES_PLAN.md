# Plano de Issues — TaskEngine

Este documento existe para outro agente/pessoa criar as issues no GitHub a partir dele. Ele não é documentação permanente do repositório — depois que as issues forem criadas, pode ser apagado.

## Antes de criar as issues

Ainda **não temos acesso confirmado** ao GitHub Project via `gh` CLI: o token atual não tem os escopos `project`/`read:project`. Quem for criar as issues precisa:

1. Rodar `gh auth refresh -h github.com -s project,read:project` (fluxo de device code, exige confirmação manual no navegador).
2. Inspecionar os campos reais do projeto antes de classificar qualquer issue:
   ```
   gh project field-list 13 --owner ArthurProjectCorrea
   ```
   Isso mostra os campos disponíveis (ex.: `Status`, `Priority`, `Módulo`/`Area`, etc.) e as opções válidas de cada um — **não presuma os nomes**, use exatamente o que o comando retornar.
3. Conferir as labels já existentes no repositório:
   ```
   gh label list --repo ArthurProjectCorrea/taskengine
   ```
   Hoje existem: `accessibility`, `bug`, `documentation`, `duplicate`, `enhancement`, `good first issue`, `help wanted`, `invalid`, `question`, `wontfix`. A maioria das issues abaixo é `enhancement` (funcionalidade nova); crie labels de módulo se fizer sentido (ex.: `backend`, `frontend`, `ai`, `infra`) — elas não existem ainda.

## Escala de prioridade sugerida (mapear para o campo real do projeto)

Esta é uma prioridade lógica baseada no fluxo end-to-end do produto — mapeie para as opções reais do campo `Priority` do Project 13 (ex.: se o projeto usa `High/Medium/Low`, `P0`→`High`, `P1`→`High` ou `Medium`, `P2`→`Medium`, `P3`→`Low`).

- **P0 — Fundação**: sem isso, nenhuma outra parte do produto funciona ou pode ser demonstrada.
- **P1 — Caminho crítico do MVP**: completa o fluxo end-to-end descrito no README (criação → sync → tracking → worklog).
- **P2 — Completa a experiência do MVP**: importante para o produto ser usável no dia a dia, mas não bloqueia a primeira demonstração do fluxo.
- **P3 — Pós-MVP / evolução**: mencionado na visão do produto como alternativa futura, não é escopo fechado do MVP 1.0.

## Como criar cada issue

Para cada item abaixo: título, módulo, prioridade sugerida e objetivo (o "porquê"/resultado esperado — **sem entrar em detalhes de implementação/infra**, isso fica para quando a issue for pega). Depois de criar, vincular ao Project 13:

```
gh issue create --repo ArthurProjectCorrea/taskengine --title "<título>" --label enhancement --body "<objetivo>"
gh project item-add 13 --owner ArthurProjectCorrea --url <url-da-issue-criada>
```

Em seguida, definir os campos `Priority`/`Status`/`Módulo` do item recém-adicionado ao project com os valores reais obtidos no passo de inspeção.

---

## Módulo: Fundação Desktop (Shell MAUI)

Base da barra flutuante — sem isso não existe app para o usuário interagir.

1. **P0 — Janela flutuante frameless com atalho global**
   Objetivo: o usuário consegue invocar a barra (ex. `Alt+Espaço`) de qualquer lugar do SO e ela aparece como uma janela flutuante sem moldura, no estilo Raycast/Spotlight. Fechar/ocultar também é global.

2. **P1 — Shell de navegação/comandos da barra**
   Objetivo: existe uma estrutura mínima de "modos" dentro da barra (ex.: digitar para criar tarefa vs. digitar para buscar/mudar status) — o esqueleto de interação da command bar, sem telas finais ainda.

## Módulo: Backend Core (Domain & Application)

Regras de negócio puras do produto — o que é uma tarefa, uma sessão de trabalho, um worklog.

3. **P0 — Modelagem de Tarefa, Sessão de Trabalho e Worklog no Domain**
   Objetivo: as entidades centrais do produto existem e representam fielmente o fluxo descrito (tarefa, status, sessão de tempo humano, sessão de tempo de IA, worklog consolidado), prontas para os casos de uso usarem.

4. **P0 — Caso de uso: criar tarefa (estruturação assistida por IA)**
   Objetivo: a partir de um texto/prompt informal do usuário, o sistema produz uma tarefa estruturada (título, descrição, etc.) pronta para ser enviada ao provedor.

5. **P1 — Caso de uso: iniciar/encerrar sessão de trabalho de uma tarefa**
   Objetivo: ao mudar o status de uma tarefa para "em andamento"/"concluído", o sistema inicia ou encerra o rastreamento de tempo associado a ela.

6. **P1 — Caso de uso: calcular e consolidar tempo trabalhado**
   Objetivo: cruzar os logs de foco humano com os logs de atividade de IA de uma sessão e produzir um resumo de horas (humano vs. IA) pronto para aprovação do usuário.

7. **P1 — Caso de uso: sincronizar worklog aprovado com o provedor**
   Objetivo: depois da aprovação do usuário, o tempo consolidado é enviado como worklog/registro de horas no provedor de tarefas, concluindo o ciclo.

## Módulo: Integração com Provedor de Tarefas

Escopo do MVP: **um único provedor** (Jira, GitHub Projects ou ClickUp — decisão pendente).

8. **P0 — Definir provedor único do MVP e cliente de autenticação**
   Objetivo: decisão tomada e o app consegue autenticar-se contra a API do provedor escolhido e guardar a credencial com segurança.

9. **P0 — Criar/atualizar card de tarefa no provedor**
   Objetivo: uma tarefa criada no TaskEngine aparece automaticamente como card no provedor, e mudanças de status feitas no TaskEngine refletem lá.

10. **P1 — Registrar worklog/horas no provedor**
    Objetivo: o resumo de horas aprovado pelo usuário (issue 7) é gravado como *worklog* oficial no provedor, sem precisar abrir a interface web dele.

## Módulo: Rastreamento de Atividade (Time-Tracking Engine)

Captura invisível de tempo, diferenciando humano de IA.

11. **P1 — Monitoramento de foco de janela ativa (humano)**
    Objetivo: o sistema sabe, a qualquer momento, se o usuário está com foco ativo relacionado à tarefa em andamento, sem exigir play/stop manual.

12. **P1 — Monitoramento de atividade de agentes de IA no sistema de arquivos**
    Objetivo: o sistema detecta edições em arquivos do projeto feitas em background (por Claude Code, Copilot, scripts) mesmo sem o usuário estar com a janela em foco, e associa esse tempo à tarefa ativa.

## Módulo: Camada de Inteligência Artificial

13. **P0 — Integração com provedor de IA em nuvem (estruturação de texto)**
    Objetivo: o app consegue chamar um modelo de IA em nuvem para transformar texto/voz informal em dados estruturados de tarefa (suporta a issue 4).

14. **P2 — Classificação IA vs. humano nos logs de tempo**
    Objetivo: a IA analisa os eventos brutos capturados (issues 11 e 12) e ajuda a diferenciar/sugerir o que foi esforço humano e o que foi atuação de agente, alimentando o cálculo da issue 6.

15. **P3 — Suporte a provedor de IA local/offline**
    Objetivo: alternativa de privacidade — permitir trocar o provedor de IA em nuvem por um modelo local, sem alterar a arquitetura existente. Mencionado na visão do produto como evolução futura, fora do escopo fechado do MVP.

## Módulo: Armazenamento & Segurança Local

16. **P0 — Persistência local (fila de tarefas, histórico, configurações)**
    Objetivo: o app mantém estado entre reinicializações (tarefas, sessões, logs, preferências) inteiramente na máquina do usuário.

17. **P1 — Armazenamento seguro de credenciais/API keys**
    Objetivo: tokens do provedor de tarefas e chaves de API de IA nunca ficam em texto puro — usam o cofre de credenciais nativo do SO do usuário.

## Módulo: Aprovação & Dashboard (Human-in-the-loop)

18. **P1 — Modal de aprovação/edição do worklog antes de sincronizar**
    Objetivo: antes de qualquer envio ao provedor, o usuário vê um resumo rápido das horas calculadas e pode ajustar antes de confirmar — nada é sincronizado sem essa etapa.

19. **P2 — Dashboard individual de histórico de tarefas e horas**
    Objetivo: o usuário consegue olhar, na própria máquina, um painel com o histórico consolidado de tarefas e tempo — sem depender do provedor para isso.

## Módulo: Onboarding & Configuração

20. **P1 — Fluxo de configuração inicial (conectar provedor + IA)**
    Objetivo: na primeira execução, o usuário consegue conectar sua conta do provedor de tarefas e configurar a chave de IA sem precisar editar arquivo nenhum manualmente.

---

## Resumo de prioridade (ordem sugerida de execução)

**P0 (fazer primeiro, é a fundação):** 1, 3, 4, 8, 9, 13, 16
**P1 (fecha o fluxo end-to-end do MVP):** 2, 5, 6, 7, 10, 11, 12, 17, 18, 20
**P2 (completa a experiência do MVP):** 14, 19
**P3 (pós-MVP):** 15
