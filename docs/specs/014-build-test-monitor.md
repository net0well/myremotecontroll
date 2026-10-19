# Spec 014: Monitor de builds e testes

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 3 — Ferramentas de desenvolvimento · **ADRs:** 0004, 0013 · **Depende de:** 008, 011

## Problema

Quero rodar build e testes dos projetos pelo celular, acompanhar a saída, ser avisado do resultado e, se falhar,
pedir ao Claude para corrigir com um toque.

## Escopo

### Dentro
- **Perfis** por projeto: detecção automática (`*.slnx`/`*.sln` → `dotnet build`/`dotnet test`; `pubspec.yaml` →
  `flutter test`/`flutter analyze`; `package.json` → scripts `build`/`test`) e perfis cadastrados na bandeja.
  O app não cria perfis (executável arbitrário).
- Execução sem shell (`ArgumentList`), saída em streaming (`BuildOutput`), log completo em arquivo com rotação.
- Resumo de erros com parsers: MSBuild, `dotnet test`, `flutter test`/`analyze`, genérico (`arquivo:linha`).
- Cancelamento encerra a árvore de processos; histórico de execuções.
- Notificação ao terminar (Hub + push) e botão **"Pedir para o Claude corrigir"** (sessão nova ou existente, com a
  saída resumida).
- Fonte de "trabalho ativo" para a spec 005.
- Card "Builds e testes" no painel.

### Fora
- CI remoto; cobertura de testes.

## Modelo de domínio

`BuildProfile`, `BuildRun` (ver `data-model.md`).

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| Hub | `ListBuildProfiles(projectId)`, `ListBuildRuns(projectId, limit)` | `dev.builds` | |
| Hub | `RunBuild(profileId)` | `dev.builds` | `builds.alreadyRunning` |
| Hub | `CancelBuild(runId)` | `dev.builds` | |
| Hub | `AskClaudeToFix(runId, sessionId?)` | `dev.builds` + `claude.chat` | |
| REST | `GET /api/v1/builds/{runId}/log` | `dev.builds` | |
| Evento | `BuildOutput(runId, chunk)`, `BuildFinished(run)` | `perm:dev.builds` | |

## Autorização

Operador+.

## Casos de borda e falhas

- Mesmo perfil já rodando → `builds.alreadyRunning`.
- Saída enorme → streaming com limite de taxa; log completo só por REST.
- Processo que não termina → timeout configurável por perfil.
- Ferramenta ausente (`flutter` não instalado) → erro claro.

## Requisitos não funcionais

- Saída chega ao app em < 500 ms; nenhuma saída sensível vai para o push (só "Build falhou: N erros").

## Integrações

`dotnet`, `flutter`, `npm`; Claude Code (correção); push.

## Critérios de aceite

- **AC-1:** Dado um build com erro, então recebo push "Build falhou: N erros" e vejo o resumo com arquivo:linha.
- **AC-2:** Dado "Pedir para o Claude corrigir", então uma mensagem com os erros é enviada para a sessão escolhida.
- **AC-3:** Dado um build em execução, quando cancelo, então a árvore de processos é encerrada.

## Decisões adiadas

- Execuções paralelas de perfis diferentes no mesmo projeto: permitidas; reavaliar se houver conflito de arquivos.

## Perguntas em aberto

1. Retenção dos **logs de build** (ex.: últimos 50 por projeto)?
2. Timeout padrão por execução (ex.: 30 min)?
