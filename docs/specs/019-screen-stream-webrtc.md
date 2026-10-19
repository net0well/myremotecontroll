# Spec 019: Espelhamento por WebRTC

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 4 — Tela · **ADRs:** 0012 · **Depende de:** 018

## Problema

O MJPEG funciona, mas consome muita banda e tem latência maior. Quero vídeo H.264/VP8 por WebRTC como modo padrão,
com volta automática para MJPEG se algo falhar.

## Escopo

### Dentro
- WebRTC com **SIPSorcery** no agente e **`flutter_webrtc`** no app; sinalização (SDP/ICE) pelo Hub (`WebRtcSignal`).
- Codificação **H.264 por hardware** (Media Foundation) quando disponível; senão VP8 por software.
- Canal de dados para entrada (substitui o Hub para `SendInput` neste modo).
- STUN opcional; **sem TURN externo** por padrão (Tailscale resolve a conectividade).
- **Fallback automático** para MJPEG em até 5 s se a negociação falhar.
- Indicador do modo (WebRTC/MJPEG) no app.

### Fora
- Áudio; TURN próprio.

## Modelo de domínio

Estado em memória da conexão WebRTC por espectador.

## Contrato de API

| Tipo | Nome | Permissão |
|---|---|---|
| Hub | `StartScreenStream({ mode: "webrtc" | "mjpeg" | "auto", ... })` | `screen.view` |
| Hub/Evento | `WebRtcSignal(payload)` | `screen.view` |
| Canal de dados | eventos de entrada (mesmo formato do `SendInput`) | `screen.control` |

## Autorização

Como na 018.

## Casos de borda e falhas

- Sem encoder de hardware → VP8 software (CPU maior; avisar).
- ICE falha (rede restritiva) → MJPEG.
- Troca de rede no meio → renegociação ou fallback.

## Requisitos não funcionais

- Latência < 150 ms na LAN; consumo de banda ao menos 50% menor que o MJPEG na mesma qualidade percebida.

## Integrações

SIPSorcery, Media Foundation, `flutter_webrtc`.

## Critérios de aceite

- **AC-1:** Dado WebRTC disponível, então o app usa WebRTC por padrão e mostra o modo no indicador.
- **AC-2:** Dado falha na negociação, então o app volta para MJPEG em até 5 s sem ação manual.

## Decisões adiadas

- Conferir licença e maturidade do SIPSorcery e dos pacotes de codec antes do `/plan` (R8, R9).

## Perguntas em aberto

Nenhuma por enquanto. As perguntas desta spec dependem dos resultados da spec 018 e serão levantadas no `/spec` antes
da implementação.
