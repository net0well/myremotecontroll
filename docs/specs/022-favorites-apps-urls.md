# Spec 022: Favoritos — abrir apps e URLs

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 5 — Sistema · **ADRs:** 0009 · **Depende de:** 003, 004

## Problema

Quero abrir no PC, pelo celular, os programas e sites que mais uso, sem que o app consiga executar qualquer coisa.

## Escopo

### Dentro
- Favoritos cadastrados na bandeja: nome, executável/atalho (`.lnk` resolvido), argumentos, diretório de trabalho,
  ícone extraído do executável.
- Abrir favorito com seus parâmetros; abrir URL `http`/`https` no navegador padrão.
- **Proposta pelo app** (`ProposeFavorite`) só vale após confirmação no PC (diálogo da bandeja); `FavoriteProposalResolved`.
- Histórico de aberturas (pela auditoria).
- **App:** grade de favoritos com ícones, campo para abrir URL, histórico.

### Fora
- Executar caminhos ou comandos arbitrários (proibido).

## Modelo de domínio

`Favorite` com `Status` (`Active`, `PendingConfirmation`) (ver `data-model.md`).

## Contrato de API

| Hub | Permissão | Erros |
|---|---|---|
| `ListFavorites()`, `OpenFavorite(id)`, `OpenUrl(url)` | `favorites.open` | `favorites.notFound`, `favorites.invalidUrl` |
| `ProposeFavorite(dto)`, `DeleteFavorite(id)` | `favorites.manage` | |
| Evento `FavoriteProposalResolved` | aparelho que propôs | |

## Autorização

Abrir: Operador+; gerenciar/propor: Administrador.

## Casos de borda e falhas

- URL `file://`, `javascript:`, `ms-settings:` etc. → `favorites.invalidUrl`.
- Executável removido → erro claro; favorito marcado como quebrado.
- Proposta ignorada no PC → expira em 24 h.

## Requisitos não funcionais

- Abertura < 1 s após o toque (além do tempo do próprio programa).

## Integrações

Shell do Windows (`ShellExecute`), extração de ícones.

## Critérios de aceite

- **AC-1:** Dado uma URL `file://` ou `javascript:`, então recebo `favorites.invalidUrl`.
- **AC-2:** Dado um favorito proposto pelo app, então ele só aparece após eu confirmar no PC.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. Abrir URLs sempre no **navegador padrão** ou permitir escolher o navegador por favorito?
