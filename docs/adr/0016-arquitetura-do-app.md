# ADR-0016: Arquitetura do app Flutter

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O app tem ~15 áreas, conexão em tempo real com vários PCs, segurança local e um painel exigente em desempenho.
O ambiente tem Flutter 3.47.2 / Dart 3.13.2 e o plugin oficial `dart-flutter` com a skill
`flutter-apply-architecture-best-practices`.

## Decisão

- Camadas **UI → Lógica (ViewModels/Notifiers Riverpod) → Dados (Repositórios → Serviços)**, conforme a skill oficial.
- Organização **feature-first**: `lib/app/` (bootstrap, tema, roteador, l10n), `lib/core/` (network, storage, security,
  logging, errors), `lib/features/<feature>/{ui,logic,data}`.
- Estado: **Riverpod 3** (`flutter_riverpod`, `riverpod_generator`, `riverpod_lint`).
- Navegação: **`go_router`**. Design: **`material_ui`** (pacote standalone a partir do Flutter 3.47), Material 3,
  tokens de tema definidos uma vez em `lib/app/theme/`.
- JSON: `json_serializable` ou `freezed` (decidir na spec 004 pela skill `flutter-implement-json-serialization`).
- Textos em pt-BR por `.arb` (`flutter_localizations` + `intl`).
- Um `ConnectionManager` por PC; repositórios expõem streams; cada card do painel tem seu provider (ADR-0021).

## Alternativas consideradas

- **BLoC:** mais cerimônia (eventos/estados) para o mesmo resultado.
- **GetX:** acoplamento global e testabilidade fraca.

## Consequências (positivas / negativas / riscos)

- (+) Alinhado às skills oficiais; providers finos permitem rebuilds mínimos no painel.
- (risco R10) Pacotes de terceiros podem ainda depender do Material do SDK: checar compatibilidade de cada pacote.

## Specs afetadas

004 em diante.
