# ADR-0013: Notificações push (FCM ou ntfy)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O SignalR cai quando o app vai para segundo plano (principalmente no iOS, R6). Avisos importantes (permissão do Claude,
tarefa concluída, build, alertas, agendamentos) precisam chegar mesmo assim.

## Decisão

- **FCM (principal):** projeto Firebase do próprio usuário; o agente usa `FirebaseAdmin` (FCM HTTP v1) com a
  service account em arquivo local fora do repositório (protegido por DPAPI ou ACL).
- **ntfy (alternativa):** servidor público ou auto-hospedado, com tópico secreto de alta entropia.
- **Payload mínimo:** apenas `type` + IDs (ex.: `{"type":"claude.permission","requestId":"..."}`); nunca caminhos,
  código, comandos ou conteúdo de mensagens. O app busca os detalhes pelo canal autenticado ao abrir.
- Ações na notificação de permissão (**Aprovar / Negar**) exigem desbloqueio do aparelho (iOS: ação com
  autenticação exigida; Android: a ação abre o app, que pede biometria se configurado).
- Preferências por tipo ficam no agente (por aparelho), para que tipos desativados nem sejam enviados.
- Deduplicação e limite de frequência; tokens inválidos são removidos.

## Alternativas consideradas

- **Sem push:** perde avisos com o app em segundo plano.
- **Só notificação local com serviço em segundo plano no app:** o iOS não permite manter conexão persistente.

## Consequências (positivas / negativas / riscos)

- (+) Avisos chegam com o app fechado, sem vazar dados sensíveis.
- (−) Depende de serviço externo (Google/ntfy) e de internet no PC.
- (risco) **iOS exige conta Apple Developer (paga) e chave APNs** para receber push, tanto via FCM quanto via app
  próprio; sem ela, o push funciona só no Android.

## Resolução dos pontos a validar (aceite em 2026-10-18)

- **FCM é o padrão**; ntfy é a alternativa configurável.
- **Conta Apple Developer:** confirmar no `/spec` da spec 011. Sem ela, o iOS fica sem push (e sem TestFlight,
  ADR-0019) até haver.

## Specs afetadas

011, 009, 010, 014, 023, 024.
