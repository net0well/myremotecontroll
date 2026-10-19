# Spec 028: Painel completo em tempo real

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 6 — Monitoramento (numerada 028 para não renumerar o backlog) · **ADRs:** 0021, 0016 · **Depende de:** 007 e todas as specs que criam cards

## Problema

Quero abrir o app e ver **tudo** o que está acontecendo no PC, ao vivo, num painel bonito, sem navegar por várias telas.
A infraestrutura e o painel v1 vêm da spec 007; cada spec intermediária já adiciona o card da sua área.

## Escopo

### Dentro — cards

| Card | Conteúdo ao vivo | Ação rápida |
|---|---|---|
| PC e conexão | Online/offline, nome, uptime, usuário, latência, LAN ou Tailscale | Trocar de PC |
| Energia | Contagem regressiva ativa, próximo agendamento de energia | Cancelar, bloquear, desligar |
| CPU | Gauge total, barras por núcleo, sparkline 60 s, clock | Abrir processos |
| Memória | Gauge RAM, uso/total, sparkline | — |
| GPU | Uso, VRAM, temperatura | — |
| Discos | Uso por unidade, leitura/escrita | Abrir arquivos |
| Rede | Gráfico de área up/down, totais | — |
| Temperaturas e ventoinhas | CPU, GPU, discos, RPM, cores por limiar | Abrir monitoramento |
| Bateria | Nível, carregando, tempo restante | — |
| Claude Code | Sessões por estado, última atividade, custo do dia | Abrir sessão |
| Permissões pendentes | Pedidos do Claude **no topo do painel** quando existirem | Aprovar / Negar |
| Builds e testes | Execuções em andamento com última linha, último resultado | Cancelar, pedir correção |
| Git | Por projeto: branch, alterados, ahead/behind | Abrir diff, commit |
| Agendamentos | Próximos com contagem regressiva | Cancelar, executar agora |
| Alertas | Alertas ativos com severidade | Ver detalhe, silenciar |
| Top processos | Top 5 por CPU e memória | Encerrar |
| Tela | Miniatura ao vivo (≈1 quadro/s, opcional) e quem está assistindo | Espelhar, screenshot |
| Transferências | Uploads/downloads com progresso | Cancelar |
| Aparelhos | Aparelhos conectados agora | Revogar (admin) |
| Atividade recente | Últimas ações da auditoria | Abrir auditoria |

### Dentro — experiência
- Grade responsiva (1–2 colunas no celular, 3–4 no tablet/paisagem).
- Cards **reordenáveis, ocultáveis e com tamanhos** (pequeno, médio, grande); layout salvo por PC.
- Toque no card abre a tela detalhada.
- **Modo quiosque** (tablet como monitor do PC): tela sempre ligada, tela cheia, sem ações destrutivas.
- Seletor de frequência; indicador "ao vivo" e "desatualizado há X s".

### Fora
- Painel web no PC; painéis personalizados com fórmulas.

## Modelo de domínio

`DashboardState` (agente, memória); `DashboardLayout` (app, por PC: ordem, visibilidade, tamanho).

## Contrato de API

`SubscribeDashboard`, `DashboardSnapshot`, `DashboardDelta(version, patches[])`, `RequestDashboardSnapshot`,
`SetDashboardRate` (ver `hub-contract.md`).

## Autorização

Cada card exige a permissão de leitura da área; ações rápidas exigem a permissão da ação; aparelhos com papel Leitura
veem só cards permitidos e sem ações.

## Casos de borda e falhas

- Conexão instável → último valor com selo "desatualizado".
- Muitos projetos/sessões → card agrupa e mostra "+N".
- Sensor indisponível → "—" com dica.
- PC offline → painel reduzido com último estado conhecido.
- Vários aparelhos assistindo → um único agregador.

## Requisitos não funcionais

- 60 fps na rolagem e nas animações em aparelho intermediário.
- Atualização visível < 1 s; snapshot inicial < 1,5 s na LAN.
- Tráfego em repouso conforme a meta da spec 007; respeitar "reduzir movimento" e fontes grandes.

## Integrações

Todas as features publicam no agregador.

## Critérios de aceite

- **AC-1:** Dado o app aberto, quando o painel carrega, então todos os cards permitidos aparecem preenchidos em até 1,5 s na LAN.
- **AC-2:** Dado uma mudança de CPU, então apenas o card de CPU é reconstruído e o valor anima até o novo número.
- **AC-3:** Dado um pedido de permissão do Claude, então o card de permissões aparece no topo com Aprovar/Negar e some quando resolvido em qualquer aparelho.
- **AC-4:** Dado um tablet em paisagem, então o painel mostra pelo menos 3 colunas.
- **AC-5:** Dado a conexão perdida, então os cards mostram "desatualizado há X s" e voltam ao vivo sozinhos quando a conexão retorna.
- **AC-6:** Dado um aparelho com papel Leitura, então ele não recebe dados de cards de arquivos nem botões de ações destrutivas.
- **AC-7:** Dado que reordeno e oculto cards, então o layout persiste após fechar o app e é independente por PC.
- **AC-8:** Dado o modo quiosque, então a tela não apaga e ações destrutivas ficam indisponíveis.
- **AC-9:** Dado "reduzir movimento" ativo no sistema, então as animações são substituídas por mudanças instantâneas.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. **Miniatura da tela** ligada por padrão (consome dados)?
2. Meta de consumo de dados em **4G** com o painel aberto?
