# ADR-0020: Observabilidade (Serilog + correlation ID)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O agente roda sem console, na bandeja. Problemas precisam ser diagnosticados por logs, sem que eles vazem tokens,
conteúdo de arquivos ou conversas com o Claude.

## Decisão

- **Serilog** estruturado com arquivo rotativo em `%LOCALAPPDATA%\MyRemoteControll\logs` (bootstrap em dois estágios,
  configuração no `appsettings.json`). OpenTelemetry opcional, desligado por padrão.
- **Correlation ID** por requisição REST e por invocação do Hub, gravado também no `AuditEntry`.
- **Redação obrigatória:** header `Authorization`, query `access_token` e `ticket`, códigos de pareamento, conteúdo de
  arquivos, prompts e respostas do Claude nunca vão para o log. Testes garantem a redação.
- "Abrir logs" no menu da bandeja.
- Nenhuma telemetria sai do PC.

## Alternativas consideradas

- **Logging padrão do ASP.NET Core em arquivo:** sem sinks e enriquecedores prontos.

## Consequências (positivas / negativas / riscos)

- (+) Diagnóstico local sem expor dados sensíveis.
- (risco) Um log novo vazando dado sensível: mitigado por destructuring policies e pelo `/security-scan`.

## Specs afetadas

Todas.
