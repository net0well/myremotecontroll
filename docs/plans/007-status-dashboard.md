# Plano: Spec 007 — Status em tempo real e painel v1

**Spec:** [007-system-status-dashboard-v1](../specs/007-system-status-dashboard-v1.md) (Approved) · **ADRs:** 0005, 0021
Inclui o card "Energia" da spec 005 (contagem ativa, ações disponíveis, capacidades do PC).

## Decisões de implementação

### Fontes no Windows (`MyRemoteControll.Windows/Monitoring`)
- **`IMetricsSource.Sample()`**: uma amostra por chamada; quem decide o ritmo é o agente. Implementação com **PDH via
  CsWin32**, usando `PdhAddEnglishCounter` (os nomes dos contadores mudam com o idioma do Windows, e este PC está em
  pt-BR):
  - CPU: `Processor Information(*)\% Processor Utility` (total e por núcleo, limitado a 100, como o Gerenciador de
    Tarefas); clock = `Processor Frequency` × `% Processor Performance` / 100.
  - Discos: `LogicalDisk(*)\Disk Read/Write Bytes/sec` + `DriveInfo` para uso e espaço (só discos fixos prontos).
  - GPU: `GPU Engine(*)\Utilization Percentage` (soma por motor, maior motor por placa) e
    `GPU Adapter Memory(*)\Dedicated Usage`; nome pelo `EnumDisplayDevices`. Total de VRAM fica ausente (não há
    contador; a spec pede "uso, VRAM").
  - Memória: `GlobalMemoryStatusEx`. Rede: diferença de `NetworkInterface.GetIPStatistics()` entre amostras.
  - Contador indisponível → campo ausente (`null`), e o card mostra "—".
  - Contadores de taxa precisam de duas coletas: a primeira amostra vem sem eles.
- **`ISystemStatusSource.GetStatus()`**: nome do PC, Windows (nome pela build: 22000+ = Windows 11), ligado desde
  (`now - TickCount64`, em vez de uptime, para não mudar a cada segundo), usuário, endereços por interface, bateria
  (`GetSystemPowerStatus`; sem bateria → ausente), na tomada e plano de energia (`PowerGetActiveScheme`).
- **Rede:** classificação por interface: Ethernet, Wi-Fi, Tailscale (descrição/nome), VPN (túnel/PPP), virtual
  (Hyper-V `vEthernet`, VMware, VirtualBox, WSL), outras. Virtuais e loopback ficam fora dos totais e da lista.

### Agente (`Features/Dashboard`, `Features/Status`)
- **Contratos:** `SystemStatus`, `MetricsSnapshot` (+ CPU, memória, discos, rede, GPU), `DashboardOptions`,
  `DashboardSnapshot`, `CardState`, `CardPatch`. O `CardPatch` vira `{ cardId, card }` (card nulo = removido), porque
  um patch troca o card inteiro, inclusive as ações disponíveis, que mudam com permissões e capacidades.
- **`DashboardAggregator`** (singleton):
  - Guarda o último estado de cada card com uma revisão global.
  - Inscrição por conexão com versão própria, intervalo de métricas e modo de dados reduzidos.
  - Um laço em segundo plano monta e envia os deltas por conexão (`Clients.Client(id)`), fora da thread do Hub.
    Ele acorda a cada publicação e a cada 250 ms enquanto houver inscritos.
  - Cards de métricas respeitam o intervalo do inscrito; os demais vão no máximo a cada 250 ms.
  - Permissões resolvidas a cada envio. Um card que perde a permissão de leitura é removido, e as ações sem
    permissão (ou indisponíveis no PC) saem de `availableActions`.
  - Dados reduzidos: os cards de métricas mandam uma versão sem núcleos e sem interfaces.
- **Cards:** `pc` (status.view), `power` (status.view), `battery` (status.view, só com bateria), `cpu`, `memory`,
  `network`, `disks` e `gpu` (monitor.view). O app define a ordem.
- **`MetricsSampler`**: um único amostrador. Liga com o primeiro inscrito (painel ou `SubscribeMetrics`) e para com o
  último (AC-2). O intervalo é o menor entre os inscritos (0,5–10 s). Cada amostra publica os cards de métricas e
  manda `MetricsTick` para quem assinou métricas.
- **`StatusPublisher`**: a cada 5 s, enquanto houver inscritos, relê o status. Se mudou, publica os cards `pc` e
  `battery` e manda `StatusUpdated` para `perm:status.view`. O card de energia é republicado quando a contagem muda
  e quando o status é relido (capacidades e atualização pendente).
- **Hub:** `GetStatus` (status.view), `SubscribeMetrics`/`UnsubscribeMetrics` (monitor.view),
  `SubscribeDashboard`/`UnsubscribeDashboard`/`RequestDashboardSnapshot`/`SetDashboardRate` (qualquer aparelho;
  cards filtrados). As inscrições somem no `OnDisconnectedAsync`.
- **Tráfego (AC-7):** números arredondados na fonte (1 casa; bytes inteiros), campos nulos omitidos e cards que não
  mudaram não são reenviados.

### App
- `DashboardController`:
  - Assina ao conectar e guarda os cards por id.
  - Aplica deltas em ordem. Uma versão fora de sequência pede snapshot (AC-3), e os deltas que chegam durante o
    pedido ficam guardados e são aplicados depois se forem mais novos.
  - Em segundo plano, cancela a assinatura e pede snapshot ao voltar.
  - Intervalo de 1 s, ou 3 s + dados reduzidos em dados móveis ou economia de bateria.
- Um provider por card (`dashboardCardProvider(id)` com `select`), para que só o card alterado reconstrua (AC-5). As
  sparklines de 60 s são acumuladas no app.
- Grade: 1–2 colunas no celular e 3–4 no tablet (AC-6). Offline mostra o último estado e há quanto tempo (AC-4).
- Card Energia (spec 005) dentro do painel, com as ações vindas de `availableActions`.

## Critérios de aceite → testes

| AC | Teste |
|---|---|
| AC-1 | agente: `AC1_Dashboard_SendsMetricDeltasAtTheChosenInterval` (1 s e 3 s, relógio falso); app: `ac1_métricas atualizam com transição animada` |
| AC-2 | `AC2_WithoutSubscribers_TheSamplerStops` |
| AC-3 | app: `ac3_delta fora de sequência pede novo snapshot` |
| AC-4 | app: `ac4_PC offline mostra o último estado e há quanto tempo` |
| AC-5 | app: `ac5_mudança só de CPU reconstrói só o card de CPU` (contagem de rebuilds) |
| AC-6 | app: `ac6_tablet em paisagem mostra pelo menos 3 colunas` |
| AC-7 | `AC7_IdleDashboard_AverageTrafficStaysUnder5KBps` (60 s, bytes do JSON do protocolo por amostra realista: 24 núcleos, 3 discos, 3 interfaces); medição real no aparelho na verificação manual |
| Extras | filtro por permissão e ações do card de energia; contagem ativa no snapshot depois de reconectar (spec 005); bateria omitida no desktop; `MetricsTick`; versões por conexão; fontes reais deste PC dão valores plausíveis |

## Resultado (2026-10-18)

- Agente: 12 testes de integração + 12 de unidade (mapeamento, classificação de interfaces, fontes reais deste PC).
  - Tráfego (AC-7): 1617 B/s com amostras sintéticas (24 núcleos, 3 discos, 3 interfaces, valores mudando a cada
    segundo) e 380 B/s nos dados reduzidos a 3 s.
  - Com as fontes reais deste PC, em 60 s (teste explícito `AC7_RealSources_IdleTrafficOnThisPc`): 1102 B/s e um
    delta de CPU por segundo.
  - Amostrador: 0,17% de CPU da máquina a 1 amostra/s; a montagem dos contadores (~0,8 s) fica em segundo plano,
    depois do início.
- Agente publicado (Release, self-contained, ReadyToRun), medido como no plano 001:
  - Inicialização até `/health`: 2,4 s com o cache quente (meta < 3 s) e 4,8 s com o cache frio.
  - CPU ocioso: 0,52% de um núcleo em 30 s (meta < 1%).
  - Working set privado: 35 MB (meta < 80 MB). Antes do painel eram 21 MB; a diferença são os contadores PDH
    aquecidos em segundo plano, para o painel abrir com dados na hora.
- Fontes reais neste PC: 24 núcleos, AMD Radeon RX 7600, Ethernet. Os filtros WFP/QoS e os WAN Miniport aparecem
  como interfaces "Ethernet" sem IP e foram excluídos, porque repetiriam o tráfego.
- App: 8 testes do controlador (inclui AC-3 e AC-4), 6 de tela (AC-1, AC-4, AC-5, AC-6, ordem e detalhe) e 18 de
  preview (todos os cards montam sem overflow nos estados exigidos).
- Bugs achados pelos testes e corrigidos: card do PC offline estourava 12 px; previews sem as traduções do
  `material_ui`; bloqueio por biometria nunca disparava após 5 min (ver plano 004).
- `battery_plus` não resolve junto com o `connectivity_plus` 7.3.2 (dependências Linux de `dbus` incompatíveis); a
  economia de bateria é lida por um canal próprio (Android `PowerManager`, iOS `isLowPowerModeEnabled`).
- **Pendente de verificação manual:** painel no aparelho real (animações, 60 fps, troca Wi-Fi ↔ dados para 3 s,
  tráfego medido no celular).

