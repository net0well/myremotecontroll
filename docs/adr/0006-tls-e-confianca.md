# ADR-0006: TLS e confiança (autoassinado + pinning)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Toda comunicação entre app e agente precisa ser cifrada e resistente a interceptação, inclusive na rede local.
Não há domínio público nem CA própria.

## Decisão

- **Opção A (padrão):** certificado autoassinado **ECDSA P-256**, validade 5 anos, gerado na primeira execução e
  guardado no repositório de certificados do usuário (`CurrentUser\My`) com chave não exportável.
  O QR de pareamento leva o **SHA-256 da chave pública (SPKI)**; o app valida esse pin em REST, SignalR e WebSocket
  e recusa qualquer outro certificado (alerta de possível ataque).
  Pin da SPKI em vez do certificado inteiro: permite reemitir o certificado com a mesma chave sem reparear.
- **Opção B:** certificado do **Tailscale** (`tailscale cert`, domínio `*.ts.net`, Let's Encrypt) quando o PC estiver
  na tailnet. Validação normal pela cadeia pública + hostname; renovação a cada ~90 dias pelo próprio Tailscale.
- HTTP puro é proibido, inclusive na LAN. O listener loopback (MCP e hooks, `127.0.0.1:47801`) usa HTTP sem TLS porque
  não sai da máquina; é protegido por token por sessão e `AllowedHosts`.

## Alternativas consideradas

- **HTTP puro:** proibido.
- **CA própria instalada no celular:** instalação de CA no Android/iOS é trabalhosa e amplia o risco (a CA assina qualquer domínio).
- **Pin do certificado inteiro:** quebra a cada reemissão.

## Consequências (positivas / negativas / riscos)

- (+) Funciona sem internet e sem domínio.
- (−) Componentes que usam a pilha TLS da plataforma não aceitam o certificado autoassinado sem código extra:
  WebView do preview (ADR-0017) e `signalr_netcore` (R4, resolvido na spec 004 com transporte próprio; ver ADR-0005).
- (risco) Perda do certificado (reinstalação do Windows) exige reparear todos os aparelhos.

## Resolução dos pontos a validar (aceite em 2026-10-18)

- **ECDSA P-256.**
- **Opção A é o padrão**; a opção B (certificado do Tailscale) pode ser ativada nas configurações da bandeja.
- **App (spec 004):** todo TLS com um PC usa `pinnedHttpClient`, um `HttpClient` sem raízes confiáveis em que só passa
  o certificado com o pin do host:porta. Assim o pin vale mesmo quando o sistema confia no certificado (o Windows
  confia no certificado do agente no próprio PC). O REST (dio) ainda confere o pin em `validateCertificate`.

## Specs afetadas

002, 004, 015, 018, 019.
