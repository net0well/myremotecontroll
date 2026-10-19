# ADR-0018: Agendador (`BackgroundService` próprio)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Agendamentos por horário (único ou recorrente com fuso) e por evento (processo terminou, sessão do Claude terminou,
build terminou, ociosidade, bateria), com aviso antecipado cancelável e persistência entre reinícios.

## Decisão

- `SchedulerService : BackgroundService` próprio, com `Schedule` persistido no SQLite e `NextRunAt` calculado.
- Todo cálculo de tempo usa **`TimeProvider`** (testado com `FakeTimeProvider`); recorrência com `TimeZoneInfo`
  (horário de verão tratado).
- Gatilhos por evento assinam um **barramento interno de eventos** em processo (`Channel<T>`) publicado pelas features
  (Claude, Builds, Processos, Status).
- Aviso antecipado (padrão 60 s) com `ScheduleCountdown` no app e toast no PC; cancelável nos dois.
- Execução perdida com o PC desligado: configurável por agendamento (pular e registrar, ou executar ao iniciar).

## Alternativas consideradas

- **Quartz.NET:** maduro, mas pesado para o volume (dezenas de agendamentos) e sem gatilhos por evento prontos.
  Reavaliar se a complexidade crescer.
- **Agendador de Tarefas do Windows:** não cobre gatilhos do Claude/builds e espalha estado fora do app.

## Consequências (positivas / negativas / riscos)

- (+) Totalmente testável; gatilhos por evento integrados às features.
- (−) Código próprio de recorrência para manter (coberto por testes).

## Specs afetadas

023, 005, 028.
