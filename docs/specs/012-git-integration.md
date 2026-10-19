# Spec 012: Integração com Git

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 3 — Ferramentas de desenvolvimento · **ADRs:** 0015, 0004 · **Depende de:** 008

## Problema

Depois que o Claude altera o código, quero revisar o diff, fazer commit, push e pull pelo celular, sem abrir o PC.

## Escopo

### Dentro
- Por projeto da whitelist: branch atual, ahead/behind, `status` (porcelain v2), arquivos alterados (staged/unstaged/
  untracked), diff por arquivo e total (unificado, com limite de tamanho e marcação de binários), log recente, branches.
- Ações: commit (seleção de arquivos; mensagem digitada ou **gerada pelo Claude** por sessão efêmera
  `claude -p --output-format json` sem ferramentas de escrita), push, pull (`--ff-only`), criar/trocar branch,
  descartar arquivo/tudo (confirmação forte + biometria).
- Execução via `git` com `ArgumentList`, `-C <projeto>`, `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`, timeout.
- **App:** tela Git com lista de arquivos, visualizador de diff (lado a lado no tablet, unificado no celular, com
  destaque de sintaxe), histórico, ações.
- Card "Git" no painel (por projeto: branch, alterados, ahead/behind).

### Fora
- Merge/rebase interativos, resolução de conflitos, stash, tags.

## Modelo de domínio

Sem entidades novas (estado lido do repositório a cada consulta, com cache curto).

## Contrato de API

| Hub | Permissão | Erros |
|---|---|---|
| `GitStatus(projectId)`, `GitDiff(projectId, path?, staged)`, `GitLog(projectId, limit)`, `GitListBranches(projectId)` | `dev.git.read` | `git.notARepository` |
| `GitGenerateCommitMessage(projectId)` | `dev.git.write` | `claude.cliNotFound` |
| `GitCommit(projectId, message, paths?)`, `GitPush`, `GitPull`, `GitCreateBranch`, `GitCheckout` | `dev.git.write` | `git.conflict`, `git.pushRejected`, `git.authRequired`, `git.operationInProgress` |
| `GitDiscard(projectId, paths?, localConfirmation)` | `dev.git.discard` | |

## Autorização

Leitura: todos os papéis; escrita: Operador+; descartar: Administrador.

## Casos de borda e falhas

- Repositório em merge/rebase → `git.operationInProgress` (só leitura permitida).
- Pull com conflito → nada alterado (`--ff-only`), `git.conflict` com a lista.
- Push rejeitado → `git.pushRejected` com sugestão de pull.
- Credencial exigindo interação → `git.authRequired`, nunca trava.
- Binários e arquivos enormes → diff resumido ("binário, 2,3 MB").
- Hooks de pre-commit do usuário falhando → erro com a saída do hook.

## Requisitos não funcionais

- `GitStatus` < 1 s em repositórios médios; diff paginado/limitado (padrão 1 MB).

## Integrações

Git instalado (verificação na inicialização), Claude Code CLI (mensagem de commit).

## Critérios de aceite

- **AC-1:** Dado alterações feitas pelo Claude, quando abro Git, então vejo os arquivos e o diff de cada um.
- **AC-2:** Dado "gerar mensagem", então recebo uma sugestão no padrão Conventional Commits que posso editar antes de commitar.
- **AC-3:** Dado um pull com conflito, então nada é alterado e recebo `git.conflict` com a lista de arquivos.
- **AC-4:** Dado "descartar tudo", então o app exige biometria e a ação é auditada.

## Decisões adiadas

- Assinatura de commits (GPG/SSH): usa a configuração do usuário, sem tratamento especial.

## Perguntas em aberto

1. Mensagem de commit gerada em **português** (Conventional Commits pt-BR, como no projeto) ou no idioma do repositório?
2. Permitir **commit com push automático** num só botão?
