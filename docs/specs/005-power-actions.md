# Spec 005: Ações de energia

**Status:** Approved (2026-10-18; revisada em 2026-10-18 no /plan, ver "Revisão do /plan")
**Data:** 2026-10-18
**Fase:** 1 — Energia e status · **ADRs:** 0009, 0011, 0021 · **Depende de:** 003, 004

## Problema

Quero desligar, reiniciar, suspender, hibernar e bloquear o PC pelo celular, com atraso e cancelamento, sem risco de
desligar por engano (não há como ligar o PC remotamente, ADR-0011).

## Escopo

### Dentro
- Ações: `Shutdown`, `Restart`, `Sleep`, `Hibernate`, `Lock`. **Sem "Sair da conta"**: depois de sair, o agente
  para e o PC fica inacessível até alguém fazer logon presencialmente.
- Atraso: 0, 30 s, 1 min, 5 min ou personalizado até 24 h; opção "forçar fechamento de apps" (padrão desligada).
- Implementação atrás de `IPowerService` (projeto `Windows`) com fake para testes:
  - **A contagem é sempre do agente** (timer com `TimeProvider`), para todas as ações. No prazo:
    `Shutdown`/`Restart` → `shutdown /s|/r /t 0` (com `/f` só se "forçar"); `Sleep`/`Hibernate` → `SetSuspendState`;
    `Lock` → `LockWorkStation`. Motivo: `shutdown /t N` com N > 0 implica `/f` (doc da Microsoft), o que tornaria
    impossível o "forçar" desligado.
  - Cancelar = parar o timer do agente (não há `shutdown /a`).
- **Uma ação pendente por vez:** um novo pedido com ação pendente é recusado com `power.alreadyScheduled`; o app
  oferece **"Substituir"** (cancela a pendente e envia a nova).
- Contagem regressiva para todos os clientes (`PowerCountdown`), cancelamento por qualquer aparelho com permissão
  (`PowerCancelled`), notificação no PC com o aparelho de origem e opção **Cancelar**: **janela própria de contagem**
  (sempre visível, no canto da bandeja, com contagem ao vivo) e item no menu da bandeja.
- **Trabalho ativo:** se houver sessões do Claude trabalhando ou builds em execução e o pedido não trouxer
  `acknowledgeActiveWork`, retorna `power.activeWork` com a lista; o app mostra o aviso e pede confirmação extra.
  (As fontes de "trabalho ativo" passam a existir nas specs 008 e 014; até lá a lista é vazia.)
- **App:** card de energia no painel com botões grandes; `SlideToConfirm` + biometria para Desligar, Reiniciar e
  Hibernar (Bloquear e Suspender sem biometria); confirmação de **Desligar** avisa que o PC só poderá ser ligado presencialmente e sugere Reiniciar
  ou Bloquear; contagem regressiva com **Cancelar** e haptics.
- Card "Energia" publicado no `DashboardAggregator` (contagem ativa).

### Fora
- Agendamentos de energia (spec 023).
- Ligar o PC (ADR-0011).

## Modelo de domínio

Estado em memória `PendingPowerAction { Action, ExecutesAt, Force, RequestedByDeviceId }` (a contagem é sempre do
agente).
Toda ação e cancelamento gera `AuditEntry`.

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| Hub | `PowerAction({ action, delaySeconds, force, acknowledgeActiveWork, localConfirmation })` → `PowerCountdownDto` | `power.basic` (Lock, Sleep) / `power.shutdown` (Shutdown, Restart, Hibernate) | `power.alreadyScheduled`, `power.activeWork`, `power.hibernateUnavailable`, `power.sleepUnavailable`, `validation.failed` |
| Hub | `CancelPowerAction()` | `power.basic` | `power.nothingToCancel` |
| Evento | `PowerCountdown(PowerCountdownDto)`, `PowerCancelled({ cancelledByDeviceId?, source })` | todos | — |

## Autorização

Matriz da seção 9. `localConfirmation` registrado na auditoria.

## Casos de borda e falhas

- Desligar é irreversível pelo app: confirmação explícita (AC-5).
- Ação já pendente → `power.alreadyScheduled`; o app oferece "Substituir".
- Hibernação desativada no sistema → `power.hibernateUnavailable` (detectada por `GetPwrCapabilities`).
- Sem estado de suspensão (S1–S3) → `power.sleepUnavailable`; o app desabilita o botão. PCs só com Modern Standby
  (S0 ocioso) também ficam sem Suspender por enquanto (ver Decisões adiadas).
- Windows Update pendente no reinício/desligamento → o app avisa que pode demorar (verificar indicador de reinício pendente).
- Cancelamento depois do prazo → `power.nothingToCancel`.
- Conexão cai durante a contagem → a ação segue; ao reconectar, o app recebe a contagem atual no snapshot do painel.
- Agente encerrado durante a contagem → ação descartada e registrada (vale para todas as ações).
- Lock com a tela já bloqueada → sucesso sem efeito.

## Requisitos não funcionais

- `PowerCountdown` chega a todos os clientes em < 300 ms na LAN.
- Todo uso de tempo via `TimeProvider` (testes com `FakeTimeProvider`).

## Integrações

`shutdown.exe` via `ArgumentList`; Win32 `SetSuspendState`, `LockWorkStation`, `GetPwrCapabilities`.

## Critérios de aceite

- **AC-1:** Dado `PowerAction(Shutdown, 60)`, então todos os clientes recebem `PowerCountdown` e o PC desliga em 60 s.
- **AC-2:** Dado uma contagem ativa, quando chamo `CancelPowerAction`, então o desligamento é abortado e todos recebem `PowerCancelled`.
- **AC-3:** Dado sessões do Claude no estado Working, quando peço desligar sem `acknowledgeActiveWork`, então recebo `power.activeWork` e o app exige confirmação extra.
- **AC-4:** Dado hibernação desabilitada, quando peço hibernar, então recebo `power.hibernateUnavailable`; dado um PC
  sem suspensão, quando peço suspender, então recebo `power.sleepUnavailable`.
- **AC-5:** Dado que confirmo Desligar, então a tela de confirmação avisa que o PC só poderá ser ligado presencialmente.
- **AC-6:** Dado um aparelho com papel Leitura, quando chama `PowerAction(Lock)`, então recebe `auth.forbidden`.
- **AC-7:** Dado uma contagem ativa, quando cancelo pelo PC (bandeja ou toast), então os aparelhos recebem `PowerCancelled`.
- **AC-8:** Dado uma ação pendente, quando peço outra ação, então recebo `power.alreadyScheduled`; ao escolher "Substituir", a pendente é cancelada e a nova é agendada.

## Decisões adiadas

- ~~Biblioteca de toast com botões para app não empacotado~~ → decidido no `/plan`: janela própria (ver revisão).
- **Suspender em PCs só com Modern Standby (S0 ocioso):** fica indisponível (`power.sleepUnavailable`). Revisitar
  quando houver um aparelho assim para testar como entrar em standby pelo agente.

## Respostas do /spec (2026-10-18)

1. **Sem "Sair da conta"** (deixaria o PC inacessível até um logon presencial).
2. Ação já pendente → **recusar** com `power.alreadyScheduled`; o app oferece "Substituir".
3. **Biometria** só em Desligar, Reiniciar e Hibernar.

## Revisão do /plan (2026-10-18)

Decisões do desenvolvedor depois de verificar a documentação da Microsoft e este PC:

1. **Contagem sempre no agente** e `shutdown /t 0` no prazo: `shutdown /t N` (N > 0) implica `/f`, e o
   `InitiateShutdown` com prazo exige `SHUTDOWN_FORCE_SELF`; os dois forçariam o fechamento dos apps.
2. **Notificação no PC por janela própria** (WinForms, sempre visível, contagem ao vivo, aparelho de origem, botão
   Cancelar) + menu da bandeja. Sem dependência nova, aparece com "Não perturbe" e funciona no single-file
   (ADR-0019). O Windows App SDK exigiria o runtime ou self-contained, que conflita com single-file.
3. **`power.sleepUnavailable` (409)**: este PC não tem nenhum estado de suspensão (`powercfg /a`: S1–S3 e S0 ocioso
   indisponíveis; só Hibernar).

## Perguntas em aberto

Nenhuma.
