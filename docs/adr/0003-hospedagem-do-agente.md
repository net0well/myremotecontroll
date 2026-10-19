# ADR-0003: Hospedagem do agente (bandeja na sessão do usuário)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O agente precisa capturar a tela, injetar entrada de mouse/teclado, mostrar toasts e a borda de privacidade, e iniciar
o `claude` com o mesmo usuário, perfil e credenciais do terminal. Windows Services rodam na sessão 0, isolada da área
de trabalho do usuário, e não conseguem fazer nada disso.

## Decisão

- Executável `net10.0-windows` com **WinForms `NotifyIcon`** (ícone de bandeja) + **Generic Host** com Kestrel,
  SignalR e os serviços em segundo plano. A UI de bandeja roda na thread STA; o host roda em paralelo.
- Inicia com o Windows por **tarefa do Agendador de Tarefas** "ao fazer logon" do usuário, `RunLevel` limitado
  (sem elevação), reinício automático em caso de falha.
- **Instância única** via mutex nomeado por usuário; a segunda instância sinaliza a primeira (mostrar menu) e sai.
- Encerramento gracioso: avisa os clientes (`AgentShuttingDown`), encerra sessões do Claude de forma retomável,
  descarrega a fila de auditoria.
- Apenas o serviço de sensores roda elevado (ADR-0014).

## Alternativas consideradas

- **Windows Service puro:** sessão 0; não captura tela nem interage com o ambiente do usuário.
- **Chave `Run` do registro (HKCU):** mais simples, mas sem reinício automático em falha nem atraso configurável.
- **MSIX com StartupTask:** empacotamento complica o serviço elevado e o driver PawnIO (ADR-0019).

## Consequências (positivas / negativas / riscos)

- (+) Acesso completo à sessão do usuário, sem elevação.
- (−) O agente só roda com o usuário logado. Com o PC na tela de logon, não há agente (documentar).
- (risco) Usuário encerra pelo Gerenciador de Tarefas: a tarefa agendada não reinicia nesse caso; aceitável.

## Resolução dos pontos a validar (aceite em 2026-10-18)

- **Autostart por tarefa agendada** "ao fazer logon" (não pela chave `Run`).

## Specs afetadas

001, 026, 027.
