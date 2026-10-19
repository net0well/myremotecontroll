# ADR-0002: Arquitetura do agente (Vertical Slice)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O agente é um app local, de um único desenvolvedor, com vida longa, domínio de complexidade baixa a média e muita
integração com o sistema operacional (Win32/WinRT, processos, arquivos, captura de tela, Claude Code CLI).
São cerca de 18 áreas funcionais que crescem de forma independente, fase a fase.

Respostas para o questionário do `architecture-advisor` / `/dotnet-init`:

| Pergunta | Resposta |
|---|---|
| Complexidade do domínio | Baixa a média (regras simples; a complexidade está nas integrações) |
| Tamanho do time | 1 desenvolvedor |
| Vida útil | Longa |
| Compliance | Nenhum requisito formal; segurança forte por ser acesso remoto ao PC |
| Integrações | Muitas, todas locais (SO, CLI do Claude, git, sensores) |
| Implantação | Um executável na máquina do usuário + um serviço elevado de sensores |

O prompt original (`docs/base/MyRemoteControll-prompt.md`) propunha projetos em camadas (Core, Infrastructure,
Screen). O documento base v2.2 substitui essa proposta pela organização abaixo.

## Decisão

**Vertical Slice Architecture** (feature folders) em poucos projetos:

| Projeto | Responsabilidade |
|---|---|
| `MyRemoteControll.Agent` | Host (Kestrel, SignalR, bandeja) e todas as features em `Features/<Feature>/` |
| `MyRemoteControll.Windows` | Interop Win32/WinRT atrás de interfaces (energia, entrada, captura, toasts, processos, janelas) |
| `MyRemoteControll.Claude` | `ClaudeProcess`, `StreamJsonParser`, servidor MCP de permissões, receptor de hooks |
| `MyRemoteControll.Sensors` | Serviço elevado (LibreHardwareMonitor + named pipe), ver ADR-0014 |
| `MyRemoteControll.Contracts` | DTOs, enums, códigos de erro, `IRemoteHub` e `IRemoteClient` compartilhados |

Regras:

- Cada feature contém seus endpoints, handlers, métodos do Hub, validação e mapeamentos.
- **Sem mediator** (handlers como classes registradas no DI). Se surgir necessidade de pipeline, usar `Mediator`
  (MIT, source-generated). MediatR 13+ tem licença comercial e está descartado.
- `DbContext` injetado diretamente, sem repositório genérico (regra `architecture` do kit).
- Dependências: `Agent` → `Windows`, `Claude`, `Contracts`; `Claude` → `Contracts`; `Windows` não depende de `Agent`.
  `Sensors` é um executável independente que só compartilha o contrato do pipe.
- Métodos do Hub são finos: validam, checam permissão (filtro do Hub) e delegam ao handler da feature.

## Alternativas consideradas

- **Clean Architecture (4 projetos):** camadas e abstrações demais para um domínio simples; cada feature tocaria 4 projetos.
- **Modular Monolith:** isolamento de módulos útil para times múltiplos; aqui só adiciona cerimônia.
- **Camadas do prompt original (Core/Infrastructure/Screen):** organiza por tipo técnico, espalhando cada feature.

## Consequências (positivas / negativas / riscos)

- (+) Uma feature nova mexe em uma pasta; fácil de especificar, testar e revisar por spec.
- (+) Interop isolado em `Windows` permite fakes nos testes (`IPowerService`, `IScreenCapture`, `IInputInjector`...).
- (−) Possível duplicação pequena entre features; aceitável e tratada com `/de-sloppify`.
- (risco) Hub único concentrando muitos métodos: mitigado com classe `partial` por feature ou delegação a handlers.

## Resolução dos pontos a validar (aceite em 2026-10-18)

- **Sem mediator:** handlers diretos registrados no DI.

## Specs afetadas

Todas.
