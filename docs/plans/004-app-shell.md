# Plano: Spec 004 — Base do app Flutter

**Spec:** [004-app-shell](../specs/004-app-shell.md) (Approved) · **ADRs:** 0005, 0006, 0016, 0021
Inclui as telas de app das specs 002 (pareamento) e 003 (Segurança → Aparelhos e Auditoria).

## Decisões de implementação

- **Projeto:** `app/`, pacote `my_remote_controll`, Flutter 3.47.2 / Dart 3.13.2, `material_ui` 1.7.0 (Material
  standalone; `MaterialUiCompatibilityBridge` só em volta do `mobile_scanner`). Android `minSdk 26` com
  `FlutterFragmentActivity` (exigência do `local_auth`); iOS 16. Versões conferidas no pub.dev (ver `pubspec.yaml`).
- **Camadas (ADR-0016):** `lib/app` (bootstrap, tema, rotas), `lib/core` (contratos, rede, armazenamento, segurança,
  componentes), `lib/features/<feature>/{data,logic,ui}`. Riverpod 3.4 com geração e `riverpod_lint`; `go_router` 18.
- **JSON (decisão adiada da spec):** `fromJson` escrito à mão em `core/contracts/contracts.dart`, espelho do
  `MyRemoteControll.Contracts`. Os contratos da fase 1 são poucos e planos; sem `freezed`/`json_serializable` não há
  passo de geração extra. Revisitar quando os contratos crescerem (painel da spec 007 em diante).
- **Idioma fixo:** `locale: Locale('pt')` no `MaterialApp`, textos em `l10n/app_pt.arb` (AC-4).
- **Design system:** tokens em `app/theme/tokens.dart`, tema escuro padrão e claro completo, destaque `#3D7BFF`, cor
  dinâmica opcional no Android (`dynamic_color`, só a paleta principal).
- **Componentes base** (`core/widgets/`): `DashCard`, `Gauge`, `Sparkline`, `StatusBadge`, `LiveIndicator`, `StaleBadge`,
  `Skeleton`, `SlideToConfirm`, com previews nos estados normal, atenção, crítico, carregando, sem permissão e offline.
  Todos consultam `MediaQuery.disableAnimations` (AC-6).
- **Conexão:** `AgentConnectionController` (um por app, para o PC ativo), com estados `NoPc`, `Connecting`, `Online`,
  `Reconnecting` (com "offline desde" e o último estado conhecido: é o "Offline" da spec, porque o app nunca para de
  tentar), `Revoked` (o "Unauthorized" da spec) e `CertificateMismatch`.
  - Testa todos os endereços do PC em paralelo (`GET /api/v1/info` com o token), usa o primeiro que responde e lembra dele.
  - Reconexão com espera de 1, 2, 4, 8, 16 e 30 s mais 20% de variação aleatória. Volta da rede (`connectivity_plus`)
    reconecta na hora (AC-2).
  - Contador de geração e `ref.mounted` descartam resultados de tentativas antigas (troca de PC no meio, AC-1).
  - `401` com `auth.revoked`/`auth.unauthorized` vai para `Revoked`, sem novas tentativas (AC-5). `DeviceRevoked` e
    `DevicePermissionsChanged` chegam pelo Hub e atualizam o estado na hora.
  - `AgentShuttingDown` marca a queda seguinte como "Agente encerrado no PC" (não é falha de rede), e o app continua
    tentando, porque o agente volta sozinho no próximo logon.
  - Uma sessão que caiu é fechada (REST e `HttpClient` do Hub), sem deixar conexões abertas.
- **Eventos do Hub:** `AgentHub.events(nome)` devolve um fluxo por evento, com vários ouvintes, encerrado quando a
  conexão para. Assim cada feature assina o que precisa sem registrar callbacks que nunca saem (o painel da 007 usa o
  mesmo caminho).
- **Pinning (ADR-0006, risco R4):**
  - `pinnedHttpClient` é um `HttpClient` sem raízes confiáveis, em que só passa o certificado com o pin do
    host:porta. Ele é usado no REST (dio, que também confere o pin em `validateCertificate`), na negociação do Hub e
    no WebSocket.
  - O SignalR usa transporte e cliente de negociação próprios (`PinnedWebSocketTransport`, `PinnedSignalRHttpClient`)
    porque o transporte padrão usa o `HttpClient` estático do `dart:io`, que não passa pelo pin. Detalhes no ADR-0005.
  - A reconexão própria do `signalr_netcore` fica desligada: ela atrasaria em ~42 s o aviso de queda.
  - A divergência de pin fica registrada no `PinRegistry` e vira a tela de alerta, sem opção de prosseguir.
- **Armazenamento:** `PairedPc` em `SharedPreferencesWithCache`, token por PC no `flutter_secure_storage`, PC ativo
  separado (mudar dados do PC não derruba a conexão). App reinstalado sem token → "Reparear". Apelido editável.
- **Segurança local:** `AppLock` bloqueia ao abrir e após o tempo configurado em segundo plano (padrão 5 min) se a
  biometria estiver ligada e o aparelho tiver biometria ou PIN (AC-3). `confirmDestructive` = `SlideToConfirm` +
  biometria/PIN, para as ações destrutivas das próximas specs.
- **Pareamento (app da 002):** QR (`mobile_scanner`) ou link colado (`mrc://pair?v=1&...`); aguarda a confirmação no
  PC (até 60 s); reparear e remover em Configurações.
- **Telas da 003:** Configurações → Segurança → Aparelhos (só com `security.manageDevices`, AC-7) e Auditoria (com
  `security.viewAudit`, atualização ao vivo por `AuditAppended`).
- **Painel:** grade responsiva com os cards "em breve"; o estado da conexão fica no topo com o seletor de PC.

## Critérios de aceite → testes

| AC | Teste |
|---|---|
| AC-1 | `ac1_trocar de PC reconecta ao novo em até 3 s` (`agent_connection_test.dart`) |
| AC-2 | `ac2_quando a rede volta, reconecta sozinho e reassina os eventos` |
| AC-3 | `ac3_com biometria ligada, pede desbloqueio ao abrir e após 5 min em segundo plano` (`app_shell_test.dart`) |
| AC-4 | `ac4_com o sistema em inglês, os textos continuam em pt-BR` |
| AC-5 | `ac5_token revogado leva ao estado de aparelho revogado, sem novas tentativas` e `ac5_token revogado mostra "Aparelho revogado" com opção de reparear` |
| AC-6 | `ac6_com "reduzir movimento", o gauge muda na hora e o indicador ao vivo não anima` |
| AC-7 | `ac7_aparelho somente leitura não vê Segurança → Aparelhos` |
| Eventos | `com AgentShuttingDown, a queda aparece como agente encerrado no PC e o app segue tentando`; `audit_controller_test.dart` (registros ao vivo no topo, sem repetir os que chegam durante a primeira página, sem perder os que chegam durante "carregar mais") |
| Pinning | `pinned_signalr_test.dart` (servidor TLS local: pin certo conecta, pin errado e host sem pin são recusados antes de qualquer requisição, o WebSocket confere o pin sozinho, queda avisa na hora); `spki_test.dart` (pin igual ao do OpenSSL e do agente) |
| e2e | `test/e2e/agent_e2e_test.dart` contra o agente real (`MRC_E2E_HOST`, `MRC_E2E_PORT`): REST, negociação e WebSocket só passam com o pin certo |

## Resultado (2026-10-18)

- `flutter analyze` sem avisos; `dart format` ok; 30 testes aprovados (os 2 e2e ficam desligados sem as variáveis).
- e2e contra o agente real no Windows: 2 aprovados. O Windows confia no certificado do agente nesse PC, e mesmo assim o
  pin errado é recusado.
- **Correção feita na spec 007 (bloqueio, AC-3):** na volta do segundo plano o sistema passa de novo por `hidden`
  (paused → hidden → inactive → resumed), e o app regravava ali o início do segundo plano: o tempo zerava e o
  bloqueio nunca acontecia num aparelho real. O teste antigo pulava direto de `paused` para `resumed` e não pegava
  isso; agora simula a sequência real do sistema, e só a primeira entrada em segundo plano é registrada.
- **Pendente de verificação manual:** app em aparelho real (escanear QR, biometria, troca Wi-Fi ↔ dados) e abertura a
  frio < 2 s.
