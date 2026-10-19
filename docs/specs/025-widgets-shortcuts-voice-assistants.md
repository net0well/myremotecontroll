# Spec 025: Widget, atalhos e assistentes de voz

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 6 — Monitoramento · **ADRs:** 0016, 0009 · **Depende de:** 005, 007, 011

## Problema

Para ações rápidas (bloquear, reiniciar, desligar) e para ver o status do PC, abrir o app é lento. Quero um widget na
tela inicial, atalhos no ícone e comandos de voz.

## Escopo

### Dentro
- **Widget** (Android e iOS, `home_widget`): status do PC (online/offline, CPU, RAM) e botões Bloquear, Reiniciar,
  Desligar; atualização periódica dentro dos limites do sistema.
- **Atalhos do ícone** (`quick_actions`).
- **Assistentes:** Siri (App Intents) e Google Assistente (atalhos/App Actions): "Desligar meu computador",
  "Reiniciar meu computador", "Bloquear meu computador".
- Ações destrutivas a partir de widget/voz exigem desbloqueio do app e confirmação.

### Fora
- Widget para relógio (Wear OS/watchOS).

## Modelo de domínio

Sem entidades novas (último status em cache no app).

## Contrato de API

Reutiliza `PowerAction` e `GetStatus`.

## Autorização

As permissões do aparelho valem igualmente para widget e voz.

## Casos de borda e falhas

- Vários PCs → o widget mostra o PC escolhido na configuração do widget.
- PC offline → widget mostra "offline há X" e desabilita os botões.
- Ação sem rede → mensagem de falha, sem enfileirar.

## Requisitos não funcionais

- Respeitar os limites de atualização de widgets de cada sistema.

## Integrações

`home_widget`, `quick_actions`, App Intents (iOS), atalhos do Android.

## Critérios de aceite

- **AC-1:** Dado o widget, quando toco em Bloquear, então o PC bloqueia sem abrir o app (se a política permitir sem biometria).
- **AC-2:** Dado "Reiniciar meu computador" pela voz, então o app pede desbloqueio e confirmação antes de enviar a ação.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. **Bloquear pelo widget sem biometria** é aceitável (bloquear não é destrutivo)?
