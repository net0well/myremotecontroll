# ADR-0009: Autorização (papéis + permissões granulares)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Aparelhos diferentes devem ter poderes diferentes (seção 9 do documento base): um celular principal com tudo, um tablet
operador, um aparelho só de leitura. Algumas ações são destrutivas e precisam de confirmação local.

## Decisão

- Três **papéis** (`ReadOnly`, `Operator`, `Administrator`) com o conjunto de permissões da seção 9.
- **Permissões granulares** extras por aparelho (`DevicePermission`). Permissões efetivas = permissões do papel ∪ extras.
- Cada permissão vira uma policy do ASP.NET Core (`perm:<chave>`), aplicada:
  - nos endpoints REST com `.RequireAuthorization("perm:files.read")`;
  - nos métodos do Hub com um atributo `[RequirePermission("power.shutdown")]` lido por um **`IHubFilter`**.
- As permissões são resolvidas **a cada chamada** a partir de um cache invalidado quando mudam (não ficam congeladas nas
  claims da conexão). Assim, uma mudança vale na próxima chamada (AC-4 da spec 003).
- Toda negação gera `AuditEntry` com resultado `Denied` e erro `auth.forbidden`.
- Regra de proteção: é proibido remover ou rebaixar o último Administrador (`security.lastAdmin`).
- Ações destrutivas exigem biometria/PIN no app; o app envia `localConfirmation=true`, que o agente registra na auditoria
  (é um sinal do app, não uma garantia de segurança).

## Alternativas consideradas

- **Só papéis:** não permite, por exemplo, um aparelho de leitura que também possa acionar o pânico ou ver arquivos.
- **Permissões negativas (revogar algo do papel):** mais flexível, mas mais difícil de entender; fica para depois, se necessário.

## Consequências (positivas / negativas / riscos)

- (+) Uma única fonte de verdade para REST, Hub e painel (o agregador filtra cards pela mesma checagem).
- (−) Cada método novo precisa declarar a permissão: um teste de arquitetura garante que nenhum método do Hub fica sem atributo.

## Specs afetadas

003, 028 e todas que expõem operações.
