# ADR-0005: Transporte de tempo real (SignalR + REST)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O app precisa receber eventos em tempo real (painel, Claude, builds, contagens regressivas) e invocar operações.
Também precisa transferir arquivos grandes e, na fase 4, quadros de tela.

## Decisão

- **SignalR (WebSocket)** em `/hubs/remote` para estado, eventos e operações interativas. Hub fortemente tipado:
  `Hub<IRemoteClient>`, com `IRemoteHub` e `IRemoteClient` definidos em `MyRemoteControll.Contracts`.
- Protocolo **JSON** (System.Text.Json, camelCase). MessagePack fica como otimização opcional, se o cliente Dart suportar.
- **REST** (`/api/v1/...`, Minimal APIs) para pareamento, upload/download (streaming, HTTP Range), screenshot em PNG,
  log de build, histórico de métricas e auditoria. Erros em ProblemDetails com `code` estável.
- **Quadros MJPEG** (spec 018): endpoint WebSocket binário dedicado (`/ws/screen`), autenticado com o mesmo token, em
  vez de mandar JPEG como base64 dentro do JSON do SignalR (+33% de tráfego). O controle (iniciar/parar, monitor,
  qualidade) continua no Hub. Confirmar na spec 018.
- Versionamento: `GET /api/v1/info` expõe `apiVersion` e `capabilities[]`; mudanças incompatíveis sobem a versão.
- Buffers com número de sequência (`seq`/`version`) para reconexão sem perda (Claude, painel).

## Alternativas consideradas

- **gRPC / gRPC-Web:** contratos fortes, mas suporte Flutter mais trabalhoso (streams bidirecionais, TLS autoassinado).
- **WebSocket cru para tudo:** reinventaria reconexão, invocação com retorno e grupos.
- **Server-Sent Events:** só servidor → cliente.

## Consequências (positivas / negativas / riscos)

- (+) Reconexão, grupos e invocações com retorno prontos; integração nativa com autenticação do ASP.NET Core.
- (−) Dois canais (Hub + REST) para manter.
- (risco R4) `signalr_netcore` precisa aceitar `HttpClient`/WebSocket customizado para o pinning: verificar na spec 004;
  alternativa é o certificado do Tailscale (ADR-0006, opção B).

## Resolução do risco R4 (spec 004, 2026-10-18)

- O transporte WebSocket padrão do `signalr_netcore` 1.4.4 chama `WebSocket.connect` sem cliente próprio, e o
  `dart:io` usa então um `HttpClient` estático criado na primeira conexão do processo. Um `HttpOverrides` global só
  funcionaria se fosse instalado antes dela (o próprio `flutter test` abre um WebSocket antes), então não serve.
- **Solução:** o app passa ao `HttpConnectionOptions` um transporte próprio (`PinnedWebSocketTransport`, com
  `WebSocket.connect(customClient:)`) e um cliente de negociação próprio (`PinnedSignalRHttpClient`), os dois sobre o
  `HttpClient` com pinning. Com transporte próprio a negociação é obrigatória (`POST /hubs/remote/negotiate` com o
  Bearer), o que já era o previsto no contrato do Hub.
- **Achado:** o `signalr_netcore` 1.4.4 aplica uma política de reconexão padrão mesmo sem `withAutomaticReconnect` e
  só avisaria a queda depois de ~42 s. O app desliga essa reconexão; quem reconecta é o `AgentConnectionController`.
- Verificado por testes com servidor TLS local e contra o agente real (pin errado é recusado no TLS da negociação e do
  upgrade do WebSocket, mesmo com o Windows confiando no certificado).

## Specs afetadas

Todas.
