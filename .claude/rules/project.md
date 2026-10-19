---
alwaysApply: true
description: >
  Regras específicas do MyRemoteControll que complementam ou substituem as regras
  genéricas do dotnet-claude-kit.
---

# Regras do projeto MyRemoteControll

Estas regras prevalecem sobre as regras genéricas do kit quando houver conflito.

## Git (substitui partes de `git-workflow.md`)

- Repositório: https://github.com/net0well/myremotecontroll (privado). `main` só recebe código por PR.
- **Primeiro PR:** Fases 0 e 1 juntas (specs 001–007), no branch `fase/1-fundacao-energia-status`. Nada é commitado
  nem enviado ao GitHub antes do fim dessa entrega (pedido do usuário em 2026-10-18).
- **Um branch e um PR por fase:** `fase/N-slug` (ex.: `fase/2-claude-code`), criado a partir da `main`.
  Dentro do branch, commits atômicos por spec. Ao fim da fase: `/security-scan`, `/arch-check`, `/outdated`,
  revisão dos ACs, push e PR para a `main` com o checklist de ACs de cada spec. O usuário aprova e faz o merge.
- Depois do merge, tag `fase-N` na `main`. A fase seguinte só começa a partir da `main` atualizada.
- Commits: Conventional Commits em português, referenciando a spec e/ou o ADR:
  `feat(claude): permissões remotas via MCP (spec 009, ADR-0004)`.
- Nunca force-push na `main`.

## Persistência (substitui o padrão PostgreSQL do kit)

- SQLite + EF Core 10 (ADR-0007). Testcontainers não é necessário; testes de integração usam
  `WebApplicationFactory` + SQLite em arquivo temporário.

## Segurança

- O app nunca envia comandos de shell; o agente só executa operações do contrato.
- Processos externos (`git`, `claude`, builds, `shutdown`) sempre com `ProcessStartInfo.ArgumentList`, nunca com string de shell.
- Logs nunca contêm tokens, conteúdo de arquivos nem conteúdo de mensagens do Claude.

## Testes

- AC-n de uma spec vira teste nomeado `AC{n}_<Descricao>` (.NET) e `ac{n}_<descricao>` (Dart).
- Toda interação com o SO fica atrás de interfaces (`IPowerService`, `IScreenCapture`, `IInputInjector`,
  `IProcessRunner`, `IClaudeCli`) com fakes; `TimeProvider` em tudo que envolve tempo.

## Idioma

- Specs, ADRs, comentários de código e textos de interface em português do Brasil; identificadores em inglês.
