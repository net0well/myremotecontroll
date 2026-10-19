# ADR-0012: Captura e streaming de tela

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O app precisa ver a tela do PC ao vivo (multi-monitor), controlá-la e tirar screenshots, com latência baixa na LAN e
consumo aceitável em 4G. WebRTC dá a menor latência, mas é trabalhoso no .NET (R8).

## Decisão

- **Captura:** **Windows.Graphics.Capture** (WGC) por monitor, com fallback para **DXGI Desktop Duplication**
  (Vortice.Windows). Tudo atrás de `IScreenCapture`.
- **Fase 1 (spec 018): MJPEG** em WebSocket binário dedicado (ADR-0005): envia só quando há mudança, qualidade e FPS
  adaptativos (alvo 15–30 FPS na LAN, 5–10 em 4G), redução de resolução configurável, nunca acumula fila
  (descarta quadros antigos). Codificação JPEG pelo WIC (componente do Windows) ou biblioteca equivalente — escolher
  pela medição de CPU na spec 018.
- **Fase 2 (spec 019): WebRTC** com SIPSorcery no agente e `flutter_webrtc` no app, sinalização pelo Hub, H.264 por
  hardware (Media Foundation) quando disponível, senão VP8; canal de dados para entrada; fallback automático para MJPEG.
- **Entrada:** `SendInput` atrás de `IInputInjector`, com conversão de coordenadas considerando DPI e escala por monitor.
- **Privacidade:** borda colorida em janela sobreposta (transparente a cliques) enquanto houver espectador + ícone
  da bandeja alterado.
- **Limitação documentada:** a área de trabalho segura (tela de bloqueio, UAC) não é capturável por um app na sessão do
  usuário → erro `screen.secureDesktop`.

## Alternativas consideradas

- **VNC/RDP embutido:** protocolos próprios, clientes Flutter limitados, segurança separada.
- **H.264 cru por WebSocket:** exige decodificador no app; WebRTC já resolve isso.

## Consequências (positivas / negativas / riscos)

- (+) Entrega valor cedo (MJPEG) e evolui para WebRTC sem mudar a API de controle.
- (−) MJPEG consome mais banda que H.264.
- (risco) O WGC pode exibir a borda amarela do próprio Windows; no Windows 11 é possível pedir captura sem borda.
  Como já mostramos uma borda de privacidade, a borda do sistema é aceitável.

## Specs afetadas

017, 018, 019, 028 (miniatura da tela).
