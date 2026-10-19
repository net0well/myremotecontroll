# Spec 004: Base do app Flutter

**Status:** Approved (2026-10-18)
**Data:** 2026-10-18
**Fase:** 0 — Fundação · **ADRs:** 0005, 0006, 0016, 0021 · **Depende de:** 002 e 003 (lado do agente)

## Problema

O app precisa de uma base sólida antes das funcionalidades: arquitetura, navegação, conexão segura com vários PCs,
segurança local e o design system do painel. Esta spec também entrega as telas de app das specs 002 e 003.

## Escopo

### Dentro
- Projeto `app/` com pacote Dart `my_remote_controll`, identificador `com.net0well.myremotecontroll` (Android e iOS),
  Flutter 3.47.x, `material_ui`, Material 3. Mínimos: Android 8 (API 26) e iOS 16.
- Idioma **fixo em pt-BR**, independente do idioma do sistema.
- Arquitetura em camadas (ADR-0016), feature-first, Riverpod 3 (com geração e `riverpod_lint`), `go_router`.
- Localização pt-BR com `.arb` (`flutter_localizations` + `intl`).
- **Design system** em `lib/app/theme/` (tokens de cor, tipografia, espaçamento, raios, elevação, durações) com
  **tema escuro como padrão**, tema claro completo, cor de destaque **azul elétrico `#3D7BFF`** e cor dinâmica
  (Material You) opcional no Android.
- **Componentes base do painel** em `lib/core/widgets/`: `DashCard` (título, valor grande, detalhe, estados),
  `Gauge` circular animado, `Sparkline`, `StatusBadge` (normal/atenção/crítico com ícone + texto), `LiveIndicator`
  pulsante, `StaleBadge` ("há 12 s"), `SlideToConfirm`, skeleton; previews de widget para todos os estados
  (normal, atenção, crítico, carregando, sem permissão, offline). Respeitam "reduzir movimento".
- **Conexão:** `ConnectionManager` por PC com estados `Connecting`, `Online`, `Reconnecting`, `Offline`,
  `Unauthorized`, `CertificateMismatch`; reconexão exponencial com jitter; tentativa paralela dos hosts; cliente REST
  (dio) e cliente SignalR com **pinning SPKI**; troca rápida de PC (seletor no topo).
- **Pareamento** (app da spec 002): escanear QR, entrada manual, lista de PCs pareados, reparear, remover.
- **Segurança local:** bloqueio por biometria/PIN do aparelho, **opcional e ligado por padrão**, ao abrir e após
  **5 min** em segundo plano (configurável); confirmação de ações destrutivas (`SlideToConfirm` + biometria) reutilizável.
- **Telas da spec 003:** Segurança → Aparelhos (admin) e Auditoria.
- **Painel inicial vazio** com a grade responsiva e cards "em breve" por área; Configurações (PCs, tema, biometria,
  timeout de bloqueio).
- Telemetria zero.

### Fora
- Cards com dados reais (a partir da spec 007) e demais funcionalidades.
- Push (spec 011), widget e atalhos (spec 025).

## Modelo de domínio (app)

- `PairedPc { id, name, hosts[], port, spkiPin, deviceId, role }` em preferências; token por PC no
  `flutter_secure_storage`.
- `AppSettings { themeMode, dynamicColor, biometricLock, lockTimeoutMinutes }`.

## Contrato de API

Consome `GET /api/v1/info`, `POST /api/v1/pairing/complete`, `POST /api/v1/devices/me/rotate-token`,
`/api/v1/devices`, `/api/v1/audit`, Hub `ListDevices`, `UpdateDevicePermissions`, eventos `DeviceRevoked`,
`DevicePermissionsChanged`, `AuditAppended`, `AgentShuttingDown`.

## Autorização

O app esconde ações sem permissão (role e permissões vindas de `/info` e de `DevicePermissionsChanged`), mas a
autoridade é sempre o agente.

## Casos de borda e falhas

- Sem rede; troca Wi-Fi ↔ dados móveis (reconecta e reavalia o melhor host).
- PC dormindo ou desligado → estado Offline com "visto há X".
- Vários PCs com o mesmo nome → diferenciados pelo `id` e por um apelido editável.
- App reinstalado (tokens perdidos) → PC aparece como "Reparear".
- Pin divergente → tela de alerta de segurança, sem opção de prosseguir.
- Token revogado → estado Unauthorized com opção de reparear.

## Requisitos não funcionais

- Abertura a frio < 2 s em aparelho intermediário.
- Acessibilidade: fontes dinâmicas, rótulos semânticos, contraste AA, alvos ≥ 48 dp.
- 60 fps na rolagem do painel.

## Integrações

Skills Flutter: `flutter-apply-architecture-best-practices`, `flutter-setup-declarative-routing`,
`flutter-setup-localization`, `flutter-implement-json-serialization`, `flutter-use-http-package`,
`flutter-build-responsive-layout`, `flutter-add-widget-test`, `flutter-add-widget-preview`.

## Critérios de aceite

- **AC-1:** Dado 2 PCs pareados, quando troco de PC no seletor, então o app reconecta ao novo em até 3 s.
- **AC-2:** Dado a conexão perdida, quando a rede volta, então o app reconecta sozinho e reassina os eventos.
- **AC-3:** Dado biometria ativada, quando abro o app após 5 min em segundo plano, então ele pede biometria antes de mostrar dados.
- **AC-4:** Dado o sistema do celular em inglês, então os textos continuam em pt-BR.
- **AC-5:** Dado um token revogado, quando o app conecta, então mostra "Aparelho revogado" com a opção de reparear.
- **AC-6:** Dado "reduzir movimento" ativo, então os componentes base não animam.
- **AC-7:** Dado um aparelho com papel Leitura, então a tela Segurança → Aparelhos não aparece.

## Decisões adiadas

- `freezed` ou `json_serializable`: decidido no `/plan` com a skill `flutter-implement-json-serialization`.

## Respostas do /spec (2026-10-18)

1. Idioma **fixo em pt-BR**.
2. Identificador **`com.net0well.myremotecontroll`**.
3. **Tema escuro como padrão**; cor de destaque **azul elétrico `#3D7BFF`**.
4. Biometria **opcional, ligada por padrão**; bloqueio após **5 min** em segundo plano.
5. Mínimos **Android 8 (API 26)** e **iOS 16**.

## Perguntas em aberto

Nenhuma.
