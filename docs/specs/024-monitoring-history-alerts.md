# Spec 024: Monitoramento, histórico e alertas

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 6 — Monitoramento · **ADRs:** 0014, 0007, 0013 · **Depende de:** 007, 011

## Problema

Quero acompanhar temperaturas e o histórico do PC e ser avisado quando algo sair do normal (superaquecimento, disco
cheio, CPU alta por muito tempo, bateria fraca, Claude esperando por mim).

## Escopo

### Dentro
- **Serviço `MyRemoteControll.Sensors`** (ADR-0014): Worker Service elevado, LibreHardwareMonitorLib + PawnIO, named
  pipe somente leitura com ACL do usuário; temperaturas (CPU, GPU, discos), ventoinhas (RPM), consumo.
- **Histórico** no SQLite: bruto 24 h, agregação por minuto 7 dias e por hora 90 dias (`MetricRollup`); retenção
  configurável; gráficos 1 h / 24 h / 7 dias no app.
- **Regras de alerta:** temperatura acima de X, disco livre abaixo de X%, CPU sustentada acima de X por N min, bateria
  fraca, sessão do Claude aguardando há N min, agente offline (detectado pelo app/push). Severidade, cooldown,
  resolução, silenciar; canais app/push/toast.
- Card "Temperaturas e ventoinhas" e card "Alertas" no painel.

### Fora
- Overclock/controle de ventoinhas (somente leitura).

## Modelo de domínio

`MetricSample`, `MetricRollup`, `AlertRule`, `AlertEvent` (ver `data-model.md`).

## Contrato de API

| Tipo | Nome | Permissão |
|---|---|---|
| REST | `GET /api/v1/metrics/history?kind=&key=&from=&to=&resolution=` | `monitor.view` |
| Hub | `ListAlertRules()`, `ListAlerts(activeOnly)` | `monitor.view` |
| Hub | `UpsertAlertRule(dto)`, `DeleteAlertRule(id)`, `SilenceAlert(id, minutes)` | `alerts.manage` |
| Evento | `AlertRaised`, `AlertResolved` | `perm:monitor.view` |
| Pipe | `snapshot`, `list-sensors` (agente ↔ serviço) | ACL do usuário |

## Autorização

Ver: todos; gerenciar alertas: Administrador.

## Casos de borda e falhas

- PawnIO ausente → sensores "indisponível" com instrução.
- Serviço de sensores parado → o restante do monitoramento continua.
- Valor oscilando no limiar → histerese e cooldown evitam alertas repetidos.
- Detecção de "agente offline" → o app (ou um último push "agente encerrando") identifica; sem servidor externo, um
  PC que cai de repente só é detectado quando o app abre.

## Requisitos não funcionais

- Gráfico de 7 dias carrega em < 1 s; amostragem sem impacto perceptível (< 1% CPU).

## Integrações

LibreHardwareMonitorLib (MPL-2.0), PawnIO (licença a conferir), push.

## Critérios de aceite

- **AC-1:** Dado CPU acima de 90 °C por 60 s, então recebo alerta uma vez e "resolvido" quando cair.
- **AC-2:** Dado o serviço de sensores parado, então o restante do monitoramento continua funcionando.
- **AC-3:** Dado o histórico de 7 dias, então o gráfico carrega em < 1 s.

## Decisões adiadas

- Detecção ativa de "PC offline" por servidor externo: fora (sem nuvem).

## Perguntas em aberto

1. Retenção: **bruto 24 h, minuto 7 dias, hora 90 dias** está ok?
2. Intervalo de amostragem para o histórico (ex.: 5 s)?
3. Regras de alerta padrão já ativas (ex.: CPU > 90 °C por 60 s, disco < 10%)?
