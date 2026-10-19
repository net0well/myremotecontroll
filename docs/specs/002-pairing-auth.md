# Spec 002: Pareamento, TLS e autenticação de dispositivos

**Status:** Approved (2026-10-18)
**Data:** 2026-10-18
**Fase:** 0 — Fundação · **ADRs:** 0006, 0008, 0010, 0020 · **Depende de:** 001 (lado do app junto com a 004)

## Problema

Só os meus aparelhos podem controlar o PC, e a conexão precisa ser cifrada e resistente a interceptação, inclusive na
rede local e via Tailscale. Hoje o agente não sabe quem está do outro lado.

## Escopo

### Dentro
- **Certificado:** usa o certificado gerado na spec 001; pin = SHA-256 da SPKI (ADR-0006, opção A).
  Opção B (certificado do Tailscale) ativável nas configurações da bandeja.
- **QR de pareamento** no menu da bandeja ("Parear aparelho…"): janela com QR, contagem regressiva de 5 min e botão
  "Gerar novo". Conteúdo (versão 1):
  `mrc://pair?v=1&h=<host1>,<host2>,...&p=47800&c=<código>&f=<pin SPKI base64url>&n=<nome do PC>`.
  Hosts: IPv4 privados ativos, IP do Tailscale (`100.x`) e nome MagicDNS, quando houver.
- **Código de uso único:** 128 bits (base64url), hash no banco, expira em 5 min, consumido no primeiro uso.
- **Confirmação no PC:** ao receber um código válido, o agente mostra na bandeja "<nome do aparelho> (<plataforma>)
  quer parear — Permitir / Recusar". A requisição aguarda até 60 s; recusa → `403 pairing.rejected`; sem resposta →
  `408 pairing.confirmationTimeout`. Em ambos os casos o código é invalidado (gerar novo QR).
- `POST /api/v1/pairing/complete` → token de dispositivo de 256 bits, devolvido **uma única vez**; no banco,
  `HMAC-SHA256(pepper, token)` com pepper protegido por DPAPI.
- **Sem limite rígido de aparelhos**; acima de 5 aparelhos ativos, a janela de confirmação mostra um aviso.
- **Esquema de autenticação** `DeviceToken` (Bearer no REST e na negociação do Hub; `access_token` no WebSocket),
  com cache curto e invalidação na revogação.
- **Rotação** a cada 30 dias por `POST /api/v1/devices/me/rotate-token`; token anterior válido por 24 h.
- **Revogação:** `RevokedAt`; conexões do aparelho derrubadas em até 2 s com `DeviceRevoked`.
- **Proteção contra força bruta:** 5 falhas em 10 min bloqueiam o pareamento por 15 min (aviso na bandeja).
- **Papel inicial:** primeiro aparelho = Administrador; demais = Leitura (ADR-0008).
- **App:** tela de pareamento (QR com `mobile_scanner` e entrada manual do link), tentativa de todos os hosts,
  armazenamento do token em `flutter_secure_storage`, **pinning SPKI** em REST, SignalR e WebSocket,
  alerta de possível ataque quando o pin não confere. Implementado junto com a spec 004 (base do app).

### Fora
- Pareamento sem câmera (código numérico) — fica para uma versão futura; a entrada manual do link cobre a falta de câmera.
- Papéis e permissões finas, auditoria completa (spec 003).
- Telas de gerenciamento de aparelhos (spec 003).
- Toast no PC a cada pareamento (spec 026).

## Modelo de domínio

- `Device` e `PairingSession` conforme `docs/architecture/data-model.md`.
- Tentativas de pareamento em memória (janela deslizante por origem e global).

## Contrato de API

| Verbo | Rota | Requisição | Resposta | Erros |
|---|---|---|---|---|
| POST | `/api/v1/pairing/complete` | `{ code, deviceName, platform }` | `201 { deviceId, token, role, pcName }` (após confirmação no PC) | `410 pairing.expired`, `410 pairing.consumed`, `403 pairing.rejected`, `408 pairing.confirmationTimeout`, `429 pairing.locked`, `400 validation.failed` |
| POST | `/api/v1/devices/me/rotate-token` | — (autenticado) | `{ token, previousValidUntil }` | `401 auth.unauthorized` |
| GET | `/api/v1/info` (autenticado) | — | `{ apiVersion, agentVersion, pcName, capabilities[], deviceId, role }` | `401` |

Hub: evento `DeviceRevoked(deviceId)` para `device:{id}` antes de desconectar.

## Autorização

- `pairing/complete`: anônimo, mas só com código válido e dentro do limite de tentativas.
- Demais rotas: token válido, não revogado.

## Casos de borda e falhas

- QR expirado → `pairing.expired`; código reutilizado → `pairing.consumed` + auditoria.
- Fingerprint/pin diferente do pareado → o app recusa a conexão e mostra alerta de possível ataque; não há "aceitar mesmo assim".
- Relógio do celular errado: a validade do código é avaliada só pelo relógio do PC.
- IP do PC mudou: o app tenta todos os hosts do QR em paralelo e usa o primeiro que responder com o pin correto.
- Dois aparelhos escaneando o mesmo QR: o primeiro consome; o segundo recebe `pairing.consumed`.
- Token enviado em log: proibido (testes de redação, ADR-0020).
- Perda do certificado (reinstalação): todos os aparelhos precisam reparear; a bandeja explica.

## Requisitos não funcionais

- Comparação de hashes em tempo constante.
- Tokens, códigos e `access_token` nunca aparecem em logs.
- Pareamento completo (escanear → conectado) < 5 s na LAN.

## Integrações

- Tailscale (opcional): detectar IP `100.x` e nome MagicDNS para o QR; `tailscale cert` na opção B.

## Critérios de aceite

- **AC-1:** Dado um QR válido, quando o app o escaneia em até 5 min, então o app recebe um token e passa a conectar com sucesso.
- **AC-2:** Dado um código já usado, quando é reenviado, então o agente responde 410 e registra auditoria.
- **AC-3:** Dado um certificado com pin diferente do pareado, quando o app conecta, então a conexão é recusada e o app alerta.
- **AC-4:** Dado um dispositivo revogado, quando ele tenta qualquer chamada, então recebe 401 e, se conectado ao Hub, é desconectado em até 2 s.
- **AC-5:** Dado 5 tentativas inválidas em 10 min, quando ocorre a 6ª, então o pareamento fica bloqueado por 15 min.
- **AC-6:** Dado o primeiro aparelho pareado, então seu papel é Administrador; dado um segundo, então seu papel é Leitura.
- **AC-7:** Dado um token rotacionado, quando o app usa o token anterior dentro de 24 h, então a chamada é aceita; depois disso, 401.
- **AC-8:** Dado o banco do PC copiado por terceiros, então nenhum token utilizável pode ser extraído dele (só HMACs; pepper fora do banco).
- **AC-9:** Dado um código válido, quando o app o envia, então o pareamento só conclui depois que eu toco em Permitir na bandeja.
- **AC-10:** Dado o pedido de pareamento, quando toco em Recusar (ou não respondo em 60 s), então o app recebe `pairing.rejected` (ou `pairing.confirmationTimeout`) e o código deixa de valer.

## Decisões adiadas

- Opção B (certificado do Tailscale) como padrão automático: fica manual nesta versão; reavaliar se a WebView do
  preview (spec 015) exigir.

## Respostas do /spec (2026-10-18)

1. **Só QR** (com entrada manual do link); código numérico fica para depois.
2. **Confirmação no PC** antes de concluir o pareamento.
3. **Sem limite rígido** de aparelhos; aviso acima de 5.

## Perguntas em aberto

Nenhuma.
