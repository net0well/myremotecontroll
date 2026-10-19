# Spec 021: Gerenciador de processos

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 5 — Sistema · **ADRs:** 0009 · **Depende de:** 007

## Problema

Quando um programa trava ou pesa no PC, quero ver os processos e encerrar o culpado pelo celular, sem risco de
derrubar o sistema.

## Escopo

### Dentro
- Listar processos: nome, PID, CPU %, memória, usuário, caminho, título da janela; busca e ordenação.
- Detalhes do processo (linha de comando só para processos do próprio usuário; filhos).
- Encerrar e **encerrar árvore**, com confirmação + biometria.
- **Lista protegida:** `csrss`, `wininit`, `winlogon`, `lsass`, `services`, `smss`, `System`, o próprio agente e o
  serviço de sensores (por PID/caminho, não só por nome).
- Janelas abertas e **trazer para frente**.
- Card "Top processos" no painel (top 5 por CPU e memória).

### Fora
- Alterar prioridade/afinidade; serviços do Windows.

## Modelo de domínio

Nenhuma entidade persistida (auditoria das ações).

## Contrato de API

| Hub | Permissão | Erros |
|---|---|---|
| `ListProcesses(query)`, `GetProcessDetails(pid)`, `ListWindows()` | `processes.view` | `processes.notFound` |
| `KillProcess(pid, tree, localConfirmation)` | `processes.kill` | `processes.protected`, `processes.notFound` |
| `FocusWindow(hwnd)` | `processes.kill` | |

## Autorização

Ver: todos; encerrar e focar janela: Operador+.

## Casos de borda e falhas

- Processo elevado → o agente sem elevação não consegue encerrar → erro claro ("requer administrador").
- PID reutilizado entre listar e encerrar → o pedido leva também o horário de início do processo; divergência → `processes.notFound`.
- Processo protegido → `processes.protected`.

## Requisitos não funcionais

- Lista com CPU % calculada em janela de 1 s; atualização a cada 2 s na tela aberta.

## Integrações

Win32/WMI para processos e janelas.

## Critérios de aceite

- **AC-1:** Dado o processo `lsass`, quando tento encerrar, então recebo `processes.protected`.
- **AC-2:** Dado um processo travado, quando encerro a árvore, então ele e os filhos terminam e a ação é auditada.
- **AC-3:** Dado um PID reutilizado por outro processo, quando o pedido antigo chega, então recebo `processes.notFound`.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. "Trazer janela para frente" deve exigir `processes.kill` (Operador) ou `screen.control` (Administrador)?
