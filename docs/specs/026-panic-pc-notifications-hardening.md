# Spec 026: Botão de pânico, avisos no PC e endurecimento

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 7 — Segurança final e entrega · **ADRs:** 0008, 0009, 0020 · **Depende de:** todas as anteriores

## Problema

Se eu perder o celular ou suspeitar de acesso indevido, preciso cortar tudo de uma vez. E quero ver no PC, na hora,
o que está sendo feito remotamente.

## Escopo

### Dentro
- **Botão de pânico** (app, widget e atalho global de teclado no PC): bloquear a tela, encerrar todas as sessões do
  Claude, cancelar builds, parar espelhamento, negar permissões pendentes, desconectar todos os aparelhos e **pausar o
  acesso remoto até liberação manual na bandeja**. Evento `PanicActivated`.
- **Toasts no PC** para ações remotas relevantes (energia, arquivos, processos, tela, Git destrutivo, pareamento),
  configuráveis por tipo, com o aparelho de origem.
- Timeout de sessão inativa no app; **rate limiting** por aparelho e por IP (ASP.NET Core rate limiter).
- **Revisão final:** `/security-scan` (OWASP, segredos, dependências vulneráveis) e checklist: cabeçalhos de segurança,
  limites do Kestrel, tamanho máximo de mensagens do Hub, validação de entrada em todos os métodos, redação de logs.

### Fora
- Apagar dados remotamente.

## Modelo de domínio

Estado de pânico persistido em `AgentSettings` (sobrevive a reinício até liberação manual).

## Contrato de API

| Tipo | Nome | Permissão |
|---|---|---|
| Hub | `Panic()` | `security.panic` (todos os papéis) |
| Evento | `PanicActivated` | todos |
| Resposta enquanto pausado | `503 agent.paused` | — |

## Autorização

Qualquer aparelho pareado pode acionar o pânico; só o PC libera.

## Casos de borda e falhas

- Pânico acionado sem conexão com o PC → indisponível pelo app (documentar: usar o atalho do PC ou revogar pelo Tailscale).
- Pânico durante contagem de energia → contagem cancelada.
- Agente reiniciado após pânico → continua pausado.

## Requisitos não funcionais

- Pânico efetivo em < 2 s.

## Integrações

Toasts do Windows, atalho global (`RegisterHotKey`).

## Critérios de aceite

- **AC-1:** Dado o pânico acionado, então em até 2 s o PC bloqueia, as sessões do Claude encerram e todos os aparelhos são desconectados.
- **AC-2:** Dado o acesso pausado após pânico, então nenhuma chamada remota é aceita até eu liberar na bandeja.
- **AC-3:** Dado um desligamento remoto, então aparece um toast no PC indicando o aparelho de origem.
- **AC-4:** Dado o agente reiniciado após um pânico, então o acesso remoto continua pausado.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. Qual **atalho global** de pânico no PC (ex.: `Ctrl+Alt+Shift+P`)?
2. O pânico também deve **revogar** os tokens (exigindo reparear) ou só pausar?
