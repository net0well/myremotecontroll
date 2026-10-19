# ADR-0021: Painel em tempo real (agregador + snapshot/delta)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O painel é a tela inicial do app e mostra tudo do PC ao vivo (seção 4.3 do documento base). Se o app assinasse cada
feature separadamente, haveria muitas assinaturas, tráfego redundante e rebuilds da tela inteira.

## Decisão

- No agente, um **`DashboardAggregator`** (singleton) mantém o `DashboardState` em memória, alimentado pelos eventos
  internos das features. Um **único amostrador** de métricas é compartilhado por todos os clientes e para quando
  ninguém está inscrito.
- `SubscribeDashboard(options)` devolve um **`DashboardSnapshot`** com `version`; depois o cliente recebe
  **`DashboardDelta(version, patches[])`**.
- Cada patch **substitui o conteúdo de um card inteiro** (`{cardId, data}` ou `{cardId, removed: true}`), em vez de
  JSON Patch por campo: cards são pequenos e o app troca o estado do provider do card de uma vez.
- `version` é monotônica **por conexão**; versão fora de sequência → o app chama `RequestDashboardSnapshot()`.
- **Throttle por card** (métricas: intervalo configurável 0,5–10 s, padrão 1 s; demais: orientados a evento, com
  intervalo mínimo por card).
- **Filtro por permissão:** cards sem permissão de leitura nem são enviados; ações rápidas sem permissão vêm marcadas
  como indisponíveis.
- No app, **um provider por card** (seleção fina com Riverpod); app em segundo plano cancela a assinatura e pede novo
  snapshot ao voltar; em dados móveis ou economia de bateria, reduz a frequência.

## Alternativas consideradas

- **App assina cada feature:** muitas assinaturas, mais tráfego, difícil garantir consistência.
- **JSON Patch (RFC 6902) por campo:** menos bytes, mas mais complexidade nos dois lados para cards já pequenos.

## Consequências (positivas / negativas / riscos)

- (+) Um ponto de entrada; consumo baixo em repouso; rebuild só do card alterado.
- (−) Toda feature precisa publicar seus dados no agregador (Definição de pronto, seção 13 do documento base).
- (risco) Agregador virar gargalo: estado imutável por card e envio fora da thread do Hub.

## Specs afetadas

007, 028 e todas as specs que criam cards.
