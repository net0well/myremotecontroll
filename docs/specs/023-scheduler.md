# Spec 023: Agendador

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 5 — Sistema · **ADRs:** 0018 · **Depende de:** 005, 008, 014, 021

## Problema

Quero automatizar ações: desligar quando o Claude terminar, suspender após um tempo ocioso, abrir um app num horário,
com aviso antes e chance de cancelar.

## Escopo

### Dentro
- **Gatilhos:** data/hora única; recorrente (dias da semana + horário + fuso); "quando o processo X terminar";
  "quando a sessão do Claude terminar"; "quando o build/teste terminar"; "após N min de ociosidade do usuário";
  "quando a bateria chegar a X%".
- **Ações:** energia (spec 005); abrir favorito; enviar mensagem a uma sessão do Claude; rodar perfil de build; notificar.
- **Aviso antecipado** (padrão 60 s) com `ScheduleCountdown` no app e toast no PC, cancelável nos dois.
- **Execução perdida** com o PC desligado: por agendamento, pular e registrar ou executar ao iniciar.
- `NextRunAt` recalculado após cada execução e na inicialização; `TimeProvider` em tudo.
- **App:** lista, criar/editar (gatilho + ação), ativar/desativar, próximas execuções, histórico.
- Card "Agendamentos" no painel (próximos com contagem regressiva).

### Fora
- Expressões cron livres; encadeamento de ações.

## Modelo de domínio

`Schedule`, `ScheduleRun` (ver `data-model.md`).

## Contrato de API

| Hub | Permissão | Erros |
|---|---|---|
| `ListSchedules()`, `UpsertSchedule(dto)`, `DeleteSchedule(id)`, `RunScheduleNow(id)`, `CancelScheduleRun(id)` | `schedules.manage` (+ permissão da ação agendada) | `schedules.invalidTrigger` |
| Eventos `ScheduleCountdown`, `ScheduleFired` | `perm:schedules.manage` | |

## Autorização

Operador+; criar um agendamento de energia exige também `power.shutdown`/`power.basic`. A ação executa com as
permissões **de quem criou** (revogar o aparelho desativa seus agendamentos).

## Casos de borda e falhas

- Horário de verão (hora inexistente/duplicada) → regra definida e testada.
- Sessão/processo/build do gatilho já terminou ao criar → o gatilho não dispara retroativamente.
- Duas ações de energia ao mesmo tempo → a segunda recebe `power.alreadyScheduled` e é registrada como falha.
- Aparelho criador revogado → agendamentos dele desativados.

## Requisitos não funcionais

- Precisão de disparo de ±1 s para gatilhos de horário.

## Integrações

Barramento interno de eventos (Claude, Builds, Processos, Status), toasts.

## Critérios de aceite

- **AC-1:** Dado "desligar quando a sessão X terminar", quando a sessão conclui, então recebo aviso e o PC desliga após 60 s se eu não cancelar.
- **AC-2:** Dado um agendamento recorrente, então `NextRunAt` é recalculado corretamente após cada execução (testado com `FakeTimeProvider`).
- **AC-3:** Dado o agente reiniciado, então os agendamentos continuam válidos.
- **AC-4:** Dado o aparelho criador revogado, então seus agendamentos ficam desativados.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. Política padrão de **execução perdida**: pular (recomendado para energia) ou executar ao iniciar?
2. A ação roda com as permissões **de quem criou** (recomendado) ou de quem está conectado na hora?
