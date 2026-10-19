# MyRemoteControll App (Flutter)

App Android/iOS (`my_remote_controll`) que conecta ao agente do PC. Regras gerais em `../CLAUDE.md`.

## Skills obrigatórias
- Antes de cada tarefa, liste as skills de Flutter/Dart disponíveis (plugin `dart-flutter`) e carregue as que se aplicam:
  `flutter-apply-architecture-best-practices`, `flutter-setup-declarative-routing`, `flutter-setup-localization`,
  `flutter-implement-json-serialization`, `flutter-use-http-package`, `flutter-build-responsive-layout`,
  `flutter-fix-layout-issues`, `flutter-add-widget-test`, `flutter-add-integration-test`, `flutter-add-widget-preview`.
- Use o MCP do Dart/Flutter para análise, testes e pub.
- Nunca use versões de pacotes de memória: confira a última versão estável no pub.dev e a licença.

## Arquitetura (ADR-0016)
- Camadas UI → Lógica (ViewModels/Notifiers Riverpod 3) → Dados (Repositórios → Serviços), feature-first.
- `lib/app/` (bootstrap, tema, roteador, l10n), `lib/core/` (network, storage, security, logging, errors),
  `lib/features/<feature>/{ui,logic,data}`.
- Navegação com `go_router`; design com `material_ui` (Flutter 3.47+); tokens de tema só em `lib/app/theme/`.

## Painel (ADR-0021)
- Tela inicial. Um provider por card; cada card só reconstrói quando o seu pedaço muda.
- Todo card tem preview de widget nos estados normal, atenção, crítico, carregando, sem permissão e offline.

## Qualidade
- `flutter analyze` sem avisos, `riverpod_lint`, `dart format`.
- AC-n de uma spec vira teste `ac{n}_<descricao>`.
- Textos da interface em pt-BR (arquivos `.arb`); identificadores em inglês.
