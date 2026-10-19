# Plano: Spec 003 — Papéis, permissões e auditoria (lado do agente)

**Spec:** [003-authorization-audit](../specs/003-authorization-audit.md) (Approved) · **ADRs:** 0009, 0020
**Lado do app** (Segurança → Aparelhos e Auditoria): implementado com a spec 004.

## Decisões de implementação

- **Catálogo** em `Contracts/Security`: `PermissionKeys` (26 chaves da seção 9) e `RolePermissions` (matriz por papel),
  compartilhados com o app. Permissões extras em `DevicePermission` (migration `DevicePermissions`).
- **Resolução a cada chamada:** `DevicePermissionResolver` (papel + extras do banco) com cache invalidado em mudança e
  revogação; as claims da conexão não decidem permissão. Por isso uma mudança vale na próxima chamada (AC-4).
- **REST:** policies `perm:<chave>` criadas sob demanda (`PermissionPolicyProvider`) e `.RequirePermission(...)` nos
  grupos de endpoints. Negação → `AuditingAuthorizationResultHandler`: auditoria + 403 `auth.forbidden`.
- **Hub:** `[RequirePermission]` ou `[AllowAnyDevice]` em todo método; `PermissionHubFilter` checa, audita a negação e
  devolve `HubResponse` com `auth.forbidden`. Teste de arquitetura garante que nenhum método fica sem atributo (AC-5).
- **`HubResponse<T>`** em `Contracts/Hub`, criado por `HubResponse.Success/Failure` (regra CA1000).
- **Grupos `perm:<chave>`** atualizados ao conectar e quando o papel muda; o aparelho recebe `DevicePermissionsChanged`.
- **Último administrador:** `DeviceManagement` serializa mudanças de papel e revogações (semáforo) e recusa com
  `security.lastAdmin` qualquer operação que deixaria o PC sem administrador ativo (REST, Hub e bandeja).
- **Auditoria:** fila limitada (10 mil) com contrapressão (`WriteAsync` espera se cheia, nunca descarta), gravação em
  lote, esvaziada no encerramento (AC-7); correlation ID automático (REST e cada invocação do Hub, via `AsyncLocal`);
  `AuditAppended` ao vivo para `perm:security.viewAudit`; negações registradas como `auth.denied` com operação e
  permissão no alvo.
- **Consulta:** `GET /api/v1/audit` (filtros + cursor, do mais recente ao mais antigo, máx. 200);
  `GET /api/v1/audit/export.csv` (RFC 4180, UTF-8 com BOM, datas UTC, proteção contra fórmulas no Excel).
- **Retenção:** `Audit:RetentionDays` (padrão 90), limpeza ao iniciar e diária.
- **Leituras sensíveis** (ver tela, download, preview): auditadas nas specs 017, 018 e 020, que criam essas operações.
- **Bandeja:** janelas "Aparelhos" (papel, renomear, revogar) e "Auditoria" (últimos 200, exportar CSV).
- **Novo código de erro:** `devices.notFound` (404).

## Critérios de aceite → testes

| AC | Teste |
|---|---|
| AC-1 | `AC1_ReadOnlyDevice_IsForbiddenAndTheDenialIsAudited` — com revogação e `UpdateDevicePermissions`, porque `PowerAction` só existe na spec 005, que repete o teste com ele |
| AC-2 | `AC2_SuccessfulChange_IsAuditedWithDeviceTargetResultAndCorrelationId` |
| AC-3 | `AC3_DemotingOrRevokingTheOnlyAdministrator_IsRefused` |
| AC-4 | `AC4_PermissionChange_AppliesOnTheNextCallOfTheConnectedDevice` |
| AC-5 | `AC5_EveryHubMethod_DeclaresItsPermission` (arquitetura) |
| AC-6 | `AC6_CsvExport_ContainsOnlyTheRequestedPeriod` |
| AC-7 | `AC7_QueuedAuditEntries_AreNotLostOnShutdown` |
| Extras | paginação por cursor, `AuditAppended` ao vivo, retenção de 90 dias, matriz da seção 9 |

## Resultado (2026-10-18)

- `dotnet build` sem avisos; `dotnet format` ok; 87 testes aprovados em três execuções seguidas.
- **Pendente de verificação manual:** janelas "Aparelhos" e "Auditoria" da bandeja.
