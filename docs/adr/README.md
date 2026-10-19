# ADRs — MyRemoteControll

Registro das decisões de arquitetura (ADR-0001). Specs referenciam os ADRs dos quais dependem.
Nenhuma spec dependente começa antes de o ADR estar **Aceito**.

| ADR | Título | Status | Specs |
|---|---|---|---|
| [0001](0001-registro-de-decisoes.md) | Registro de decisões | Aceito | todas |
| [0002](0002-arquitetura-do-agente.md) | Arquitetura do agente (Vertical Slice) | Aceito | todas |
| [0003](0003-hospedagem-do-agente.md) | Hospedagem do agente (bandeja na sessão do usuário) | Aceito | 001 |
| [0004](0004-integracao-claude-code.md) | Integração com o Claude Code | Aceito | 008–016 |
| [0005](0005-transporte-tempo-real.md) | Transporte de tempo real (SignalR + REST) | Aceito | todas |
| [0006](0006-tls-e-confianca.md) | TLS e confiança (autoassinado + pinning) | Aceito | 002 |
| [0007](0007-persistencia.md) | Persistência (SQLite + EF Core 10) | Aceito | 001+ |
| [0008](0008-autenticacao.md) | Autenticação (pareamento + token de dispositivo) | Aceito | 002, 003 |
| [0009](0009-autorizacao.md) | Autorização (papéis + permissões granulares) | Aceito | 003 |
| [0010](0010-conectividade-externa.md) | Conectividade externa (Tailscale) | Aceito | 002 |
| [0011](0011-ligar-pc-remotamente.md) | Ligar o PC remotamente (fora do escopo) | Aceito | — |
| [0012](0012-captura-e-streaming-de-tela.md) | Captura e streaming de tela | Aceito | 017–019 |
| [0013](0013-notificacoes-push.md) | Notificações push (FCM ou ntfy) | Aceito | 011 |
| [0014](0014-sensores-de-hardware.md) | Sensores de hardware (serviço elevado separado) | Aceito | 024 |
| [0015](0015-execucao-de-git.md) | Execução de Git (executável `git`) | Aceito | 012 |
| [0016](0016-arquitetura-do-app.md) | Arquitetura do app Flutter | Aceito | 004+ |
| [0017](0017-preview-do-localhost.md) | Preview do localhost (YARP) | Aceito | 015 |
| [0018](0018-agendador.md) | Agendador (`BackgroundService` próprio) | Aceito | 023 |
| [0019](0019-distribuicao.md) | Distribuição (single-file + instalador) | Aceito | 027 |
| [0020](0020-observabilidade.md) | Observabilidade (Serilog + correlation ID) | Aceito | todas |
| [0021](0021-painel-em-tempo-real.md) | Painel em tempo real (agregador + snapshot/delta) | Aceito | 007, 028 |

## Formato

```markdown
# ADR-NNNN: <Título>
**Status:** Proposto | Aceito | Substituído por ADR-XXXX
**Data:** AAAA-MM-DD
## Contexto
## Decisão
## Alternativas consideradas
## Consequências (positivas / negativas / riscos)
## Specs afetadas
```

ADRs podem ter uma seção extra **Resolução dos pontos a validar**, que registra as escolhas feitas no aceite.
