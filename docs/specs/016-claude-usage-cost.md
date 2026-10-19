# Spec 016: Custo e uso do Claude

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 3 — Ferramentas de desenvolvimento · **ADRs:** 0004 · **Depende de:** 008

## Problema

Quero saber quanto as sessões estão consumindo (tokens, custo estimado, duração) por sessão, dia e projeto, e ser
avisado se passar de um orçamento diário.

## Escopo

### Dentro
- Extrair `total_cost_usd`, uso de tokens e duração das mensagens `result` do stream-json.
- Como `--resume` reporta o **total acumulado** da conversa, guardar o último total por sessão e calcular o delta por
  turno (ADR-0004).
- Totais por sessão, dia e projeto; gráfico simples no app (`fl_chart`).
- Aviso de orçamento diário configurável (notificação, sem bloquear), uma vez por dia.
- Custo do dia no card "Claude Code" do painel.
- Rótulo claro de que os valores são **estimativas do cliente** e podem diferir da cobrança (ou do uso da assinatura).

### Fora
- Sessões externas (sem stream-json; sem custo), salvo se o hook `Stop`/`SessionEnd` trouxer dados no futuro.
- Integração com a API de cobrança da Anthropic.

## Modelo de domínio

Campos de custo/uso em `ClaudeSession`; tabela de agregação diária se necessário (decidir no `/plan`).

## Contrato de API

| Hub | Permissão |
|---|---|
| `GetClaudeUsage({ from, to, groupBy })` | `claude.view` |

## Autorização

`claude.view`.

## Casos de borda e falhas

- `result` sem custo (versão do CLI ou provedor) → "indisponível".
- Fuso horário do "dia" → fuso do PC.

## Requisitos não funcionais

- Consulta de 90 dias < 500 ms.

## Integrações

Stream-json do Claude Code; push (aviso de orçamento).

## Critérios de aceite

- **AC-1:** Dado uma sessão concluída, então vejo custo e tokens na lista de sessões.
- **AC-2:** Dado o orçamento diário excedido, então recebo um aviso uma única vez no dia.
- **AC-3:** Dado uma sessão retomada com `--resume`, então o custo do dia soma só o delta do novo turno.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. Você usa **assinatura (Pro/Max)** ou **API key**? Com assinatura, o "custo" é só uma referência; talvez o mais útil seja tokens e duração.
2. Valor padrão do orçamento diário (ou desligado por padrão)?
