# Spec 007: Status em tempo real e primeira versão do painel

**Status:** Approved (2026-10-18)
**Data:** 2026-10-18
**Fase:** 1 — Energia e status · **ADRs:** 0005, 0021 · **Depende de:** 004, 005

## Problema

Ao abrir o app, quero ver na hora se o PC está online e como ele está (CPU, memória, discos, rede, bateria), ao vivo e
num painel bonito, que vai crescer com cada área do sistema.

## Escopo

### Dentro
- `GetStatus`: hostname, versão do Windows, uptime, usuário logado, IPs, bateria (nível, carregando, tempo restante),
  plano/estado de energia.
- **Métricas** com um único amostrador compartilhado (intervalo 0,5–10 s, padrão 1 s), que para quando ninguém está
  inscrito: CPU total e por núcleo, clock; RAM (uso/total); discos (uso por unidade, espaço livre, leitura/escrita);
  rede (up/down por interface e total); GPU básica (uso, VRAM) via contadores do Windows. Fontes atrás de interfaces
  com fakes.
- **Infraestrutura do painel** (ADR-0021): `DashboardAggregator`, `SubscribeDashboard`, `DashboardSnapshot`,
  `DashboardDelta(version, patches[])` por conexão, `RequestDashboardSnapshot`, `SetDashboardRate`, throttle por card,
  filtro por permissão, modo de dados reduzidos.
- **Painel v1 no app**, já com o visual final (seção 4.3 do documento base), nesta ordem: **PC e conexão**,
  **Energia** (da spec 005), **CPU**, **Memória**, **Rede**, **Discos**, **GPU** (básico), **Bateria** (se houver); gauges e
  sparklines de 60 s animados; estados ao vivo / reconectando / desatualizado / offline; grade responsiva
  (1–2 colunas no celular, 3–4 no tablet); toque no card abre detalhe simples.
- Um provider Riverpod por card; app em segundo plano cancela a assinatura e pede snapshot ao voltar; intervalo padrão
  de **1 s**, reduzido automaticamente para **3 s** em dados móveis ou economia de bateria.

### Fora
- Temperaturas e ventoinhas (spec 024), histórico e alertas (spec 024).
- Cards das demais áreas (cada spec adiciona o seu); reordenar/ocultar/tamanhos e modo quiosque (spec 028).

## Modelo de domínio

- `DashboardState` em memória no agente; `CardState { cardId, kind, data, availableActions }`.
- `SystemStatus`, `MetricsSnapshot` em `Contracts`.

## Contrato de API

| Tipo | Nome | Permissão |
|---|---|---|
| Hub | `GetStatus()` → `SystemStatus` | `status.view` |
| Hub | `SubscribeMetrics(intervalMs)` / `UnsubscribeMetrics()` | `monitor.view` |
| Hub | `SubscribeDashboard(DashboardOptions)` → `DashboardSnapshot` | autenticado (cards filtrados) |
| Hub | `UnsubscribeDashboard()`, `RequestDashboardSnapshot()`, `SetDashboardRate(intervalMs)` | autenticado |
| Evento | `DashboardDelta(version, patches[])`, `StatusUpdated`, `MetricsTick` | conforme permissão |

## Autorização

Cards exigem a permissão de leitura da área (`status.view`, `monitor.view`); ações rápidas exigem a permissão da ação.

## Casos de borda e falhas

- Contador de desempenho indisponível → o campo vem como ausente e o card mostra "—" com dica.
- Muitos clientes → um só amostrador; envio por conexão fora da thread do Hub.
- Delta com versão fora de sequência → o app pede snapshot automaticamente.
- PC offline → o app mostra o último estado conhecido e há quanto tempo está offline.
- Sem bateria (desktop) → card de bateria omitido.
- Interfaces de rede virtuais (Hyper-V, VPN) → agrupadas; Tailscale identificado.

## Requisitos não funcionais

- Atualização visível < 1 s após a mudança no PC; snapshot inicial < 1,5 s na LAN.
- 60 fps nas animações; transições de 200–300 ms; "reduzir movimento" respeitado.
- Amostrador com < 1% de CPU no intervalo de 1 s.
- Tráfego com o painel aberto e o PC em repouso: **≤ 5 KB/s no Wi-Fi** e **≤ 2 KB/s em dados móveis**.

## Integrações

PDH/contadores do Windows (`Processor Information`, `GPU Engine`, `GPU Adapter Memory`, `PhysicalDisk`),
`GlobalMemoryStatusEx`, `GetSystemPowerStatus`, `NetworkInterface`.

## Critérios de aceite

- **AC-1:** Dado o painel aberto, então as métricas atualizam no intervalo escolhido com transição animada.
- **AC-2:** Dado nenhum cliente inscrito, então o amostrador de alta frequência para.
- **AC-3:** Dado um delta com versão fora de sequência, então o app pede novo snapshot automaticamente.
- **AC-4:** Dado o PC offline, então o painel mostra o estado offline, há quanto tempo e o último estado conhecido.
- **AC-5:** Dado uma mudança só de CPU, então apenas o card de CPU é reconstruído (teste de widget contando rebuilds).
- **AC-6:** Dado um tablet em paisagem, então o painel mostra pelo menos 3 colunas.
- **AC-7:** Dado o painel aberto com o PC em repouso por 60 s, então o tráfego médio fica em até 5 KB/s no Wi-Fi.

## Decisões adiadas

- Miniatura da tela no painel: spec 028.

## Respostas do /spec (2026-10-18)

1. Ordem: PC e conexão, Energia, CPU, Memória, Rede, Discos, GPU, Bateria.
2. Intervalo padrão de **1 s**; **3 s** em dados móveis ou economia de bateria.
3. Tráfego em repouso: **≤ 5 KB/s no Wi-Fi** e **≤ 2 KB/s em dados móveis**.

## Perguntas em aberto

Nenhuma.
