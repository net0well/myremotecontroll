# Spec 011: Notificações push com o app em segundo plano

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 2 — Claude Code · **ADRs:** 0013, 0020 · **Depende de:** 008, 009, 010

## Problema

Com o app fechado ou em segundo plano o SignalR cai, e eu perco avisos importantes (permissão do Claude, tarefa
concluída, build, alertas). Quero ser avisado em segundos, sem expor dados sensíveis.

## Escopo

### Dentro
- Provedores: **FCM** (padrão; `FirebaseAdmin`, FCM HTTP v1, service account do meu projeto Firebase em arquivo local
  protegido, fora do repositório) e **ntfy** (URL + tópico secreto), configurados na bandeja.
- `RegisterPushToken(provider, token)`, `UnregisterPushToken()`; tokens inválidos removidos automaticamente.
- Tipos: permissão do Claude; sessão concluída/falhou; build passou/falhou; alerta; agendamento prestes a executar;
  agente iniciou (PC online); aparelho novo pareado.
- **Payload mínimo:** `type` + IDs (sem caminhos, código, comandos ou conteúdo); texto genérico ("O Claude precisa da
  sua aprovação em <projeto>" usa só o nome curto do projeto — ver Pergunta 3).
- Ações rápidas **Aprovar / Negar** na notificação de permissão, exigindo desbloqueio (iOS: ação com autenticação;
  Android: abre o app, que pede biometria se configurada).
- Preferências por tipo e por aparelho guardadas no agente (`DeviceNotificationPreference`); tela no app.
- Deduplicação e limite de frequência; app em primeiro plano não recebe push duplicado (usa o Hub).
- Tocar na notificação abre a tela certa (deep link com o ID).
- **App:** `firebase_messaging` + `flutter_local_notifications`; ntfy via assinatura do tópico.

### Fora
- Alertas de hardware em si (spec 024) e agendamentos (spec 023) — aqui só o canal.

## Modelo de domínio

`Device.PushProvider/PushToken`, `DeviceNotificationPreference`.

## Contrato de API

| Tipo | Nome | Permissão |
|---|---|---|
| Hub | `RegisterPushToken(provider, token)`, `UnregisterPushToken()` | autenticado |
| Hub | `GetNotificationPreferences()`, `UpdateNotificationPreferences(dto)` | autenticado |
| Push | `{ type, ids… }` | — |

## Autorização

O aparelho só recebe push de tipos que sua permissão permite ver (ex.: permissões do Claude exigem `claude.view`).

## Casos de borda e falhas

- Token inválido → removido; app re-registra ao abrir.
- PC sem internet → fila curta (TTL 5 min) e descarte.
- App em primeiro plano → sem push (evento pelo Hub).
- Muitos eventos seguidos → agrupamento (ex.: "3 builds falharam").
- Service account ausente/inválida → aviso na bandeja; push desativado sem afetar o resto.

## Requisitos não funcionais

- Evento → push no aparelho < 5 s (dependente do provedor).
- Nenhum dado sensível no payload (teste automatizado sobre todos os tipos).

## Integrações

Firebase Cloud Messaging (APNs no iOS), ntfy.

## Critérios de aceite

- **AC-1:** Dado o app fechado, quando o Claude pede permissão, então recebo push em até 5 s e, ao tocar, abro o card do pedido.
- **AC-2:** Dado o tipo "build" desativado nas preferências, então nenhum push de build é enviado.
- **AC-3:** Dado qualquer push, então o payload não contém caminhos, código ou conteúdo de mensagens.
- **AC-4:** Dado a ação "Aprovar" na notificação, então o aparelho exige desbloqueio antes de enviar a decisão.

## Decisões adiadas

- Servidor ntfy auto-hospedado: documentado no README, sem automação.

## Perguntas em aberto

1. Você tem **conta Apple Developer**? Sem ela, push no iOS fica para depois (só Android).
2. Você já tem (ou vai criar) um **projeto Firebase**? Ou prefere começar com **ntfy**?
3. O texto do push pode conter o **nome do projeto** (ex.: "myremotecontroll")? Recomendado: sim, só o nome curto.
