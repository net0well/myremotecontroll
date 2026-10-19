# Plano: Spec 005 — Ações de energia

**Spec:** [005-power-actions](../specs/005-power-actions.md) (Approved, revisada no /plan) · **ADRs:** 0009, 0011, 0021
O card de energia no painel recebe a contagem ativa pelo snapshot da spec 007, feita logo em seguida.

## Decisões de implementação

### Agente
- **Contratos** (`Contracts/Power`): `PowerActionKind`, `PowerActionRequest`, `PowerCountdownDto` (com `Force`, nome do
  aparelho de origem e `UpdatePending`), `PowerCancelledDto` + `PowerCancelSource { Device, Tray, AgentShutdown, Failed }`,
  `ActiveWorkItemDto`. Eventos `PowerCountdown`/`PowerCancelled` em `IRemoteClient`, para todos.
  Novos códigos: `power.alreadyScheduled`, `power.activeWork`, `power.hibernateUnavailable`, `power.sleepUnavailable`,
  `power.nothingToCancel`.
- **`HubError.Details`** (opcional, omitido quando nulo): `power.activeWork` leva a lista de trabalho ativo, como o
  contrato do Hub já previa (`{ code, message, details }`). `OperationError` ganha `Details` também.
- **`IPowerService`** no projeto `Windows`, com tipos próprios (`PowerOperation`, `PowerCapabilities`), sem depender de
  `Contracts` (ADR-0002):
  - `Shutdown`/`Restart`: `shutdown.exe /s|/r /t 0 [/f]` pelo `IProcessRunner` (lista de argumentos).
  - `Sleep`/`Hibernate`: `SetSuspendState(hibernate, false, false)` depois de ligar `SeShutdownPrivilege`
    (`AdjustTokenPrivileges`), numa thread à parte (a chamada só volta quando o PC acorda).
  - `Lock`: `LockWorkStation`.
  - Capacidades por `GetPwrCapabilities`: suspender = S1, S2 ou S3; hibernar = S4 e arquivo de hibernação presente.
  - Reinício pendente por atualização: chaves `WindowsUpdate\Auto Update\RebootRequired` e
    `Component Based Servicing\RebootPending` (as que o Configuration Manager da Microsoft consulta).
  - P/Invoke gerado pelo **CsWin32** (`Microsoft.Windows.CsWin32` 0.3.363, MIT) a partir dos metadados oficiais,
    sem transcrever assinaturas e structs à mão.
- **`PowerService`** (singleton, `Features/Power`): uma ação pendente por vez, protegida por lock.
  - Ordem das checagens: validação → permissão da ação → disponibilidade → trabalho ativo → ação já pendente.
  - Toda execução passa pelo timer do agente (`TimeProvider.CreateTimer`). Atraso 0 vira 1 s, para a resposta e o
    `PowerCountdown` chegarem aos aparelhos antes de o PC desligar ou suspender.
  - Cancelar para o timer. Corrida entre timer e cancelamento: quem tirar a pendente primeiro vence.
  - Falha do Windows na execução → auditoria `failure` e `PowerCancelled(Failed)`.
  - Encerramento do agente com contagem ativa (`IHostedLifecycleService.StoppingAsync`) → descartada, auditada e
    `PowerCancelled(AgentShutdown)`.
  - Auditoria: `power.shutdown|restart|sleep|hibernate|lock` (pedido, com `localConfirmation`, atraso, forçar e prazo;
    recusas com o código), `power.cancel`, `power.execute`, `power.discard`.
- **Permissão por ação:** o método do Hub exige `power.basic` (filtro) e, para `Shutdown`, `Restart` e `Hibernate`,
  confere `power.shutdown` no próprio método. A negação é auditada como `auth.denied`, pelo mesmo helper do filtro.
- **Trabalho ativo:** `IActiveWorkSource` (nenhuma fonte até as specs 008 e 014). Vale para Desligar, Reiniciar,
  Suspender e Hibernar; Bloquear não interrompe trabalho.
- **PC:** `PowerCountdownBroker` (criado no `Program`, como o broker de pareamento) avisa a bandeja do início e do
  fim de cada contagem. A bandeja mostra a **janela de contagem** (sempre visível, sem roubar o foco, no canto da
  área de trabalho, com a ação, o aparelho de origem, a contagem ao vivo e "Cancelar") e um item "Cancelar …" no
  menu. O cancelamento pelo PC é auditado como origem `tray`.
- **Testes:** `AgentRuntimeOptions.ConfigureServices` permite trocar o `IPowerService` por um falso e registrar
  fontes de trabalho ativo; o relógio é o `FakeTimeProvider`.

### App
- Card "Energia" no painel: Bloquear, Suspender, Reiniciar, Hibernar e Desligar, escondidos sem permissão e
  desabilitados quando o PC não oferece a ação (snapshot da 007).
- Folha de confirmação: atraso (agora, 30 s, 1 min, 5 min, personalizado até 24 h) e "forçar fechamento dos apps"
  (desligado). `SlideToConfirm` + biometria em Desligar, Reiniciar e Hibernar. Desligar avisa que o PC só liga
  presencialmente e sugere Reiniciar ou Bloquear (AC-5).
- Respostas: `power.alreadyScheduled` → "Substituir" (cancela e reenvia); `power.activeWork` → lista + confirmação
  extra (reenvia com `acknowledgeActiveWork`); indisponível → mensagem clara.
- Contagem ativa no card (de `PowerCountdown`) com "Cancelar", haptics a cada etapa e aviso de atualização pendente.

## Critérios de aceite → testes

| AC | Teste |
|---|---|
| AC-1 | `AC1_ShutdownIn60s_BroadcastsTheCountdownAndShutsDownAt60s` (integração, relógio falso, dois aparelhos) |
| AC-2 | `AC2_Cancel_AbortsTheShutdownAndEveryoneReceivesPowerCancelled` |
| AC-3 | `AC3_ActiveWorkWithoutAcknowledge_ReturnsPowerActiveWorkWithTheList` (fonte falsa); app: `ac3_trabalho ativo pede confirmação extra` |
| AC-4 | `AC4_HibernateOrSleepUnavailable_ReturnsTheSpecificError` |
| AC-5 | app: `ac5_confirmar Desligar avisa que o PC só liga presencialmente` |
| AC-6 | `AC6_ReadOnlyDevice_LockIsForbiddenAndAudited` (também fecha o AC-1 da spec 003 com `PowerAction`) |
| AC-7 | `AC7_CancelFromThePc_DevicesReceivePowerCancelled` (pelo broker, como a bandeja) |
| AC-8 | `AC8_SecondRequest_IsRefusedAndReplaceCancelsThenSchedules`; app: `ac8_ação pendente oferece Substituir` |
| Extras | atraso 0 executa em 1 s; aparelho só com `power.basic` não desliga; falha do Windows → `PowerCancelled(Failed)`; encerramento do agente descarta e audita; argumentos do `shutdown.exe` com e sem `/f` |

## Resultado (2026-10-18)

- Agente: 13 testes de integração (relógio falso, Windows falso) + 15 de unidade (argumentos do `shutdown.exe` com e
  sem `/f`, falha vira `PowerException`, matriz de permissões, validação do atraso). Capacidades reais deste PC:
  suspender indisponível e hibernar disponível, igual ao `powercfg /a`.
- Janela de contagem conferida em imagem (título, contagem, aparelho de origem, avisos de "forçar" e de atualização,
  Cancelar).
- App: 8 testes de widget (AC-3, AC-5, AC-8, troca para Bloquear, atraso e "forçar" no pedido, ação indisponível
  desabilitada e sem permissão escondida, contagem com Cancelar, mensagem de hibernação indisponível).
- **Pendente de verificação manual:** desligar, reiniciar, hibernar e bloquear de verdade a partir do celular;
  cancelar pela janela e pelo menu da bandeja; o canal de economia de bateria no iOS (escrito sem compilar, porque não
  há Mac aqui).

