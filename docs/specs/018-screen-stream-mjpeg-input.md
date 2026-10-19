# Spec 018: Espelhamento MJPEG e controle remoto

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 4 — Tela · **ADRs:** 0012, 0005, 0009 · **Depende de:** 017

## Problema

Quero ver a tela do PC ao vivo e, quando necessário, controlar mouse e teclado pelo celular, com latência baixa na LAN
e uso aceitável em 4G.

## Escopo

### Dentro
- Captura contínua (WGC com fallback DXGI) do monitor escolhido; detecção de mudança (só envia quadros alterados).
- Codificação JPEG com qualidade e FPS **adaptativos** (alvo 15–30 FPS na LAN, 5–10 em 4G), redução de resolução
  configurável, **nunca acumula fila** (descarta quadros antigos).
- Transporte: WebSocket binário `/ws/screen` (ADR-0005), controle pelo Hub.
- Modo **somente visualização** (`screen.view`) e **modo controle** (`screen.control`), um controlador por vez.
- Entrada via `SendInput` (`IInputInjector`): mover, clique esquerdo/direito/meio, duplo clique, arrastar, rolagem,
  texto Unicode, atalhos (Ctrl/Alt/Shift/Win), teclas especiais; coordenadas convertidas considerando DPI/escala.
- **App:** modo touchpad e modo toque direto, pinça para zoom, teclado virtual com barra de teclas especiais
  (Esc, Tab, Ctrl, Alt, Win, setas, F1–F12, Ctrl+Alt+Del indisponível), indicador de FPS/latência, botão de screenshot.
- **Privacidade:** borda colorida no PC enquanto houver espectador e ícone da bandeja alterado; `ScreenViewersChanged`.
- App em segundo plano pausa o stream.

### Fora
- WebRTC (019); áudio; área de transferência sincronizada.

## Modelo de domínio

Estado em memória: espectadores, controlador atual, parâmetros por espectador.

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| Hub | `StartScreenStream(options)` → URL do `/ws/screen` | `screen.view` | `screen.secureDesktop`, `screen.monitorNotFound` |
| Hub | `UpdateScreenStream(options)`, `StopScreenStream()` | `screen.view` | |
| Hub | `RequestScreenControl()`, `ReleaseScreenControl()` | `screen.control` | `screen.controlTaken` |
| Hub | `SendInput(batch)` | `screen.control` + ser o controlador | `auth.forbidden` |
| WS | `/ws/screen?monitor=&quality=&maxFps=` (quadros JPEG com cabeçalho mínimo: seq, timestamp, dimensões) | `screen.view` | |
| Evento | `ScreenViewersChanged` | `perm:screen.view` | |

## Autorização

Ver: todos os papéis. Controlar: só Administrador (seção 9) + biometria ao entrar no modo controle.

## Casos de borda e falhas

- Perda de pacotes/rede lenta → descarta quadros antigos, reduz qualidade/FPS.
- Mudança de resolução ou de monitores no PC → reinicia a captura e avisa o app.
- Múltiplos espectadores → codificação compartilhada por perfil de qualidade.
- Tela bloqueada durante o stream → quadro de aviso "Tela bloqueada" + `screen.secureDesktop`.
- Janelas elevadas (admin) não recebem entrada de um processo não elevado (UIPI) → documentar.

## Requisitos não funcionais

- Latência ponta a ponta < 200 ms na LAN.
- CPU do agente < 15% em 1080p/15 FPS.

## Integrações

WGC, DXGI, WIC (JPEG) ou biblioteca equivalente, Win32 `SendInput`.

## Critérios de aceite

- **AC-1:** Dado o espelhamento ativo na LAN, então vejo a tela com no mínimo 15 FPS.
- **AC-2:** Dado o modo controle, quando toco num ponto no modo toque direto, então o clique ocorre na coordenada correspondente considerando escala/DPI.
- **AC-3:** Dado alguém assistindo, então a borda de aviso aparece no PC e some ao encerrar.
- **AC-4:** Dado um aparelho com papel Leitura, quando envia `SendInput`, então recebe `auth.forbidden`.
- **AC-5:** Dado um controlador ativo, quando outro aparelho pede controle, então recebe `screen.controlTaken`.

## Decisões adiadas

- Biblioteca de JPEG (WIC vs. libjpeg-turbo): pela medição de CPU no `/plan`.

## Perguntas em aberto

1. **Cor da borda** de privacidade e espessura (ex.: vermelha, 4 px)?
2. Qualidade padrão em 4G (ex.: 720p, 8 FPS)?
