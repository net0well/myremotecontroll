# Plano: Spec 002 — Pareamento, TLS e autenticação (lado do agente)

**Spec:** [002-pairing-auth](../specs/002-pairing-auth.md) (Approved) · **ADRs:** 0006, 0008, 0010, 0020
**Lado do app** (tela de pareamento, `flutter_secure_storage`, pinning): implementado com a spec 004.

## Decisões de implementação

- **Entidades** em `Agent/Domain/`: `Device`, `PairingSession` e `AuditEntry` (esta já com o escritor em lote, que a
  spec 003 completa com consulta e exportação). Migration `DevicesPairingAudit`.
- **Segredos:** token de 256 bits e código de 128 bits em base64url; no banco, `HMAC-SHA256(pepper, valor)`; pepper de
  256 bits em `pepper.dat`, protegido por DPAPI (escopo do usuário). Pepper ilegível é recriado com aviso na bandeja.
- **Consumo atômico do código** (`UPDATE ... WHERE ConsumedAt IS NULL`) antes da confirmação: um segundo aparelho com o
  mesmo QR recebe `pairing.consumed`.
- **Confirmação no PC:** `PairingConfirmationBroker` liga o pedido HTTP (que espera até 60 s) à bandeja; nos testes, um
  assinante com resposta fixa. Fechar o diálogo recusa; não há botão padrão.
- **Papel inicial:** sem administrador ativo, quem pareia vira Administrador; senão, Leitura (cobre o "primeiro
  aparelho" e evita ficar sem administrador depois de revogar todos).
- **Bloqueio:** contador global de falhas (5 em 10 min → 15 min). Por IP não adiantaria: o atacante troca de endereço.
- **Autenticação:** esquema `DeviceToken`; `Authorization: Bearer` no REST e na negociação; `access_token` na query só
  nas rotas do Hub. 401 em ProblemDetails com `auth.unauthorized` ou `auth.revoked`.
- **Revogação:** `DeviceRevocation` marca `RevokedAt`, envia `DeviceRevoked` ao grupo do aparelho e aborta as conexões
  registradas em `DeviceConnections`. As regras de quem pode revogar ficam na spec 003.
- **QR:** `mrc://pair?v=1&h=<hosts>&p=<porta>&c=<código>&f=<pin SPKI>&n=<nome do PC>`; hosts = IPv4 privados das
  interfaces ativas (sem adaptadores virtuais), IP do Tailscale e nome MagicDNS (via `tailscale status --json`).
- **Novo código de erro:** `pairing.invalidCode` (400) para código inexistente — registrado no contrato.

## Critérios de aceite → testes (`tests/.../Spec002/PairingAuthTests.cs`)

| AC | Teste |
|---|---|
| AC-1 | `AC1_ValidQr_GivesTokenThatAuthenticatesRestAndHub` |
| AC-2 | `AC2_ReusedCode_Returns410AndIsAudited` |
| AC-3 | `AC3_QrCarriesThePinOfTheServedCertificate` (recusa de pin divergente: teste do app, spec 004) |
| AC-4 | `AC4_RevokedDevice_Gets401AndIsDisconnectedWithinTwoSeconds` |
| AC-5 | `AC5_FiveInvalidAttemptsInTenMinutes_LockPairingForFifteenMinutes` |
| AC-6 | `AC6_FirstDeviceIsAdministrator_NextOnesAreReadOnly` |
| AC-7 | `AC7_RotatedToken_PreviousOneStillWorksFor24HoursOnly` |
| AC-8 | `AC8_DatabaseHoldsOnlyKeyedHashes_NeverTheToken` |
| AC-9 | `AC9_PairingCompletesOnlyAfterConfirmationOnThePc` |
| AC-10 | `AC10_RejectedOnThePc_Returns403AndInvalidatesTheCode`, `AC10_UnansweredFor60Seconds_Returns408` |
| NF | `TokensNeverReachTheLogFiles` |

## Resultado (2026-10-18)

- `dotnet build` sem avisos; `dotnet format` ok; 60 testes aprovados (unidade + integração).
- `detect_antipatterns`: só falsos positivos (propriedade `Result` da auditoria, consultas que alteram a entidade) e as
  duas fronteiras síncronas documentadas da 001.
- **Pendente de verificação manual:** janela de QR e diálogo de confirmação na bandeja (interface WinForms), junto com
  o app na spec 004.
