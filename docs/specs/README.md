# Specs — MyRemoteControll

Specs geradas a partir do backlog do documento base (seção 10) no formato do `/spec` do dotnet-claude-kit.
Ciclo: **Draft → In Review → Approved**. Só specs **Approved** são implementadas; "Perguntas em aberto" precisa estar
vazio para aprovar. Se a implementação revelar uma premissa errada, a spec volta para In Review.

| Spec | Título | Fase | Status | Perguntas em aberto |
|---|---|---|---|---|
| [001](001-agent-host.md) | Host do agente, bandeja, configuração, logs e banco | 0 | Approved | 0 |
| [002](002-pairing-auth.md) | Pareamento, TLS e autenticação | 0 | Approved | 0 |
| [003](003-authorization-audit.md) | Papéis, permissões e auditoria | 0 | Approved | 0 |
| [004](004-app-shell.md) | Base do app Flutter | 0 | Approved | 0 |
| [005](005-power-actions.md) | Ações de energia | 1 | Approved | 0 |
| [006](006-removida.md) | Removida (Wake-on-LAN) | 1 | Removida | — |
| [007](007-system-status-dashboard-v1.md) | Status em tempo real e painel v1 | 1 | Approved | 0 |
| [008](008-claude-sessions.md) | Sessões gerenciadas e chat em streaming | 2 | Draft | 3 |
| [009](009-claude-permissions.md) | Permissões do Claude pelo celular | 2 | Draft | 4 |
| [010](010-claude-external-sessions.md) | Sessões do terminal via hooks | 2 | Draft | 2 |
| [011](011-push-notifications.md) | Notificações push | 2 | Draft | 3 |
| [012](012-git-integration.md) | Integração com Git | 3 | Draft | 2 |
| [013](013-claude-quick-commands-voice-images.md) | Comandos rápidos, voz e imagens | 3 | Draft | 2 |
| [014](014-build-test-monitor.md) | Monitor de builds e testes | 3 | Draft | 2 |
| [015](015-localhost-preview.md) | Preview do localhost | 3 | Draft | 1 |
| [016](016-claude-usage-cost.md) | Custo e uso do Claude | 3 | Draft | 2 |
| [017](017-screenshot.md) | Screenshot sob demanda | 4 | Draft | 1 |
| [018](018-screen-stream-mjpeg-input.md) | Espelhamento MJPEG e controle remoto | 4 | Draft | 2 |
| [019](019-screen-stream-webrtc.md) | Espelhamento por WebRTC | 4 | Draft | 0 |
| [020](020-file-manager.md) | Gerenciador de arquivos | 5 | Draft | 2 |
| [021](021-process-manager.md) | Gerenciador de processos | 5 | Draft | 1 |
| [022](022-favorites-apps-urls.md) | Favoritos (apps e URLs) | 5 | Draft | 1 |
| [023](023-scheduler.md) | Agendador | 5 | Draft | 2 |
| [024](024-monitoring-history-alerts.md) | Monitoramento, histórico e alertas | 6 | Draft | 3 |
| [028](028-realtime-dashboard.md) | Painel completo em tempo real | 6 | Draft | 2 |
| [025](025-widgets-shortcuts-voice-assistants.md) | Widget, atalhos e assistentes de voz | 6 | Draft | 1 |
| [026](026-panic-pc-notifications-hardening.md) | Pânico, avisos no PC e endurecimento | 7 | Draft | 2 |
| [027](027-packaging-release.md) | Empacotamento e entrega | 7 | Draft | 1 |

## Ordem de execução

- **Fase 0:** 001 → 002 (agente) → 003 (agente) → 004 (app base + telas de app das specs 002 e 003).
- **Fase 1:** 005 → 007.
- **Fase 2:** 008 → 009 → 010 → 011.
- **Fase 3:** 012, 013, 014, 015, 016.
- **Fase 4:** 017 → 018 → 019.
- **Fase 5:** 020, 021, 022 → 023.
- **Fase 6:** 024 → 028 → 025.
- **Fase 7:** 026 → 027.

As specs de fases futuras ficam em Draft; as perguntas delas são feitas no início de cada fase, porque podem mudar com
o que for aprendido nas anteriores.
