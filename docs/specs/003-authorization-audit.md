# Spec 003: Papéis, permissões e auditoria

**Status:** Approved (2026-10-18)
**Data:** 2026-10-18
**Fase:** 0 — Fundação · **ADRs:** 0009, 0020 · **Depende de:** 002

## Problema

Aparelhos diferentes devem ter poderes diferentes (celular principal, tablet operador, aparelho só de leitura), e
tudo o que acontece no PC por acesso remoto precisa ficar registrado e consultável.

## Escopo

### Dentro
- Papéis `ReadOnly`, `Operator`, `Administrator` e as permissões da seção 9 do documento base, em
  `Contracts/Security/PermissionKeys.cs` e `Roles.cs`.
- Permissões extras por aparelho (`DevicePermission`); efetivas = papel ∪ extras.
- Policies `perm:<chave>` em endpoints REST; atributo `[RequirePermission]` + `IHubFilter` nos métodos do Hub;
  teste de arquitetura: nenhum método do Hub sem atributo.
- Permissões resolvidas a cada chamada (cache invalidado na mudança); grupos SignalR `perm:*` recalculados e evento
  `DevicePermissionsChanged` para o aparelho afetado.
- `AuditEntry` para toda ação mutável e toda negação (`auth.forbidden`), com dispositivo, ação, alvo sanitizado,
  resultado, código de erro, `localConfirmation` e correlation ID. `Source` = `Device`, `Tray`, `Scheduler`, `System`.
- Também são auditadas as **leituras sensíveis**: ver a tela (screenshot e início de espelhamento), download e
  pré-visualização de arquivos. Status, métricas e listagens não são auditados.
- Escrita em lote assíncrona (`Channel` + `BackgroundService`) com flush garantido no desligamento.
- Consulta com filtros e cursor; exportação CSV; retenção configurável (padrão 90 dias).
- **Bandeja:** janela "Aparelhos" (listar, renomear, mudar papel, revogar) e "Auditoria" (últimos registros, exportar CSV).
- **App:** Segurança → Aparelhos (admin) e Auditoria (com filtros). Implementado junto com a spec 004.
- Evento `AuditAppended` para `perm:security.viewAudit`.

### Fora
- Botão de pânico e toasts no PC (spec 026).
- Permissões negativas (remover algo do papel) — fora desta versão (ADR-0009).

## Modelo de domínio

`DevicePermission`, `AuditEntry` (ver `docs/architecture/data-model.md`).

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| REST GET | `/api/v1/devices` | `security.manageDevices` | 403 |
| REST DELETE | `/api/v1/devices/{id}` | `security.manageDevices` | 403, 404, `409 security.lastAdmin` |
| REST GET | `/api/v1/audit?from=&to=&deviceId=&action=&result=&cursor=&limit=` | `security.viewAudit` | 403 |
| REST GET | `/api/v1/audit/export.csv?...` | `security.viewAudit` | 403 |
| Hub | `ListDevices()` → `DeviceDto[]` (com "conectado agora") | `security.manageDevices` | `auth.forbidden` |
| Hub | `UpdateDevicePermissions(id, { role, extraPermissions[] , name? })` | `security.manageDevices` | `security.lastAdmin` |
| Evento | `DevicePermissionsChanged`, `AuditAppended` | — | — |

## Autorização

Matriz da seção 9 do documento base. Ações destrutivas carregam `localConfirmation`, registrado na auditoria.

## Casos de borda e falhas

- Remover/rebaixar/revogar o último Administrador → `security.lastAdmin` (também pela bandeja).
- Mudança de permissão com o aparelho conectado → vale na próxima chamada; o app atualiza a UI pelo evento.
- Volume alto de auditoria → lote; fila limitada com contrapressão (nunca descartar registros).
- Desligamento com fila pendente → flush antes de sair (timeout de 5 s, depois log de erro).
- Alvo com dado sensível (caminho completo, conteúdo, prompt) → sanitizado antes de gravar.

## Requisitos não funcionais

- Gravação de auditoria adiciona < 5 ms à operação.
- Consulta de 90 dias com filtros < 500 ms (índices em `Timestamp`, `DeviceId`, `Action`).

## Integrações

Nenhuma externa.

## Critérios de aceite

- **AC-1:** Dado um aparelho com papel Leitura, quando chama `PowerAction`, então recebe `auth.forbidden` e a negação é auditada.
- **AC-2:** Dado qualquer ação mutável bem-sucedida, então existe um `AuditEntry` com dispositivo, ação, alvo, resultado e correlation ID.
- **AC-3:** Dado que tento rebaixar o único administrador, então a operação é recusada com `security.lastAdmin`.
- **AC-4:** Dado uma alteração de permissões, quando o aparelho afetado está conectado, então a nova política vale na próxima chamada.
- **AC-5:** Dado o Hub, então todo método público declara uma permissão (teste de arquitetura).
- **AC-6:** Dado registros de auditoria, quando exporto CSV com filtro de período, então o arquivo contém só os registros do período.
- **AC-7:** Dado o agente encerrando com registros na fila, então nenhum registro é perdido.

## Decisões adiadas

- Assinatura/encadeamento de hash dos registros (auditoria à prova de adulteração): fora desta versão.

## Respostas do /spec (2026-10-18)

1. Retenção da auditoria: **90 dias** (configurável).
2. Leituras sensíveis auditadas: **ver tela, download e preview de arquivos**; status e métricas não.

## Perguntas em aberto

Nenhuma.
