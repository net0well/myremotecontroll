# ADR-0007: Persistência (SQLite + EF Core 10)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O agente guarda dispositivos, auditoria, agendamentos, histórico de métricas, eventos do Claude e configurações.
É um app local de um usuário. O padrão do dotnet-claude-kit é PostgreSQL.

## Decisão

- **SQLite + EF Core 10**, arquivo em `%LOCALAPPDATA%\MyRemoteControll\data.db`, modo **WAL**.
- Migrations versionadas no projeto `Agent`, aplicadas na inicialização, com **backup automático** do arquivo antes de
  aplicar uma migration.
- Banco corrompido: renomear para `data.corrupt-<data>.db`, recriar e avisar na bandeja (spec 001).
- Datas como `DateTimeOffset` UTC via `TimeProvider` (armazenadas como ticks/ISO para ordenação correta no SQLite).
- Escritas de alto volume (auditoria, métricas, eventos do Claude) em lote, por `Channel` + `BackgroundService`.
- Retenção por tarefa periódica (auditoria 90 dias, métricas brutas 24 h / minuto 7 dias / hora 90 dias, eventos do
  Claude: últimos 2.000 por sessão).

## Alternativas consideradas

- **PostgreSQL:** exigiria instalar e manter um servidor no PC; desnecessário.
- **LiteDB:** sem EF Core, migrations e LINQ maduros.
- **Arquivos JSON:** sem consultas, concorrência frágil.

## Consequências (positivas / negativas / riscos)

- (+) Zero instalação; testes de integração com SQLite em arquivo temporário (sem Testcontainers).
- (−) Limitações do provedor SQLite do EF (por exemplo, ordenação de `DateTimeOffset`, algumas alterações de esquema
  exigem recriar tabela): tratadas com conversores e migrations revisadas.
- (risco) Crescimento do histórico de métricas: mitigado pela agregação e retenção.

## Specs afetadas

001 em diante.
