# ADR-0008: Autenticação (pareamento + token de dispositivo)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Só aparelhos do dono podem acessar o agente. Não existe servidor de identidade nem conta em nuvem.
O app precisa de uma credencial de longa duração, revogável e que não exponha nada se o banco do PC vazar.

## Decisão

- **Pareamento:** o agente cria uma `PairingSession` com código de uso único de alta entropia (expira em 5 min) e mostra
  um QR na bandeja com `{hosts[], porta, código, spkiSha256, nomeDoPC}`. O app chama
  `POST /api/v1/pairing/complete {code, deviceName, platform}` sobre TLS já validado pelo pin.
- **Token de dispositivo:** 256 bits aleatórios (base64url), opaco, devolvido **uma única vez**.
  No banco fica `HMAC-SHA256(pepper, token)`; o pepper fica protegido por **DPAPI** (escopo do usuário), fora do banco.
  Comparação em tempo constante (`CryptographicOperations.FixedTimeEquals`).
- **Uso:** `Authorization: Bearer <token>` no REST e na negociação do Hub; no WebSocket, o `access_token` da query
  string (padrão do SignalR). O valor é removido de logs (ADR-0020).
- **Rotação:** a cada 30 dias, de forma transparente: o app pede um novo token por chamada autenticada; o anterior
  continua válido por 24 h (`PreviousTokenHash`) para cobrir reconexões.
- **Revogação:** lógica e imediata; conexões ativas do aparelho são derrubadas em até 2 s (`DeviceRevoked`).
- **Proteções:** 5 falhas de pareamento em 10 min bloqueiam o pareamento por 15 min; rate limiting por IP e por
  aparelho; timeout de sessão inativa no app.
- **Pareamento sem câmera (opcional):** código numérico de 8 dígitos + confirmação do pin exibido na bandeja.

## Alternativas consideradas

- **JWT com refresh token:** a revogação imediata exigiria consulta ao banco de qualquer forma; mais complexidade sem ganho.
- **mTLS (certificado de cliente):** forte, mas gestão de certificados no celular e no `signalr_netcore` é trabalhosa.

## Consequências (positivas / negativas / riscos)

- (+) Vazamento do banco não revela tokens utilizáveis (HMAC com pepper fora do banco).
- (+) Revogação imediata e simples.
- (−) Consulta ao banco (com cache curto) a cada requisição/conexão.
- (risco) Token no `flutter_secure_storage` de um celular desbloqueado e comprometido: mitigado por biometria no app,
  papéis e botão de pânico.

## Resolução dos pontos a validar (aceite em 2026-10-18)

- **Papel padrão:** o primeiro aparelho pareado vira Administrador; os demais entram como Leitura até serem promovidos.
- **Pareamento por código numérico:** escopo decidido no `/spec` da spec 002.

## Specs afetadas

002, 003, 004, 026.
