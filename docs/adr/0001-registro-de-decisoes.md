# ADR-0001: Registro de decisões

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O MyRemoteControll será desenvolvido em fases, ao longo de meses, com apoio de IA. Decisões tomadas em uma sessão
precisam valer nas seguintes, e as specs (`/spec` do dotnet-claude-kit) precisam saber em quais decisões se apoiam.

## Decisão

- Registrar toda decisão de arquitetura em `docs/adr/NNNN-titulo.md`, no formato descrito em `docs/adr/README.md`.
- Status possíveis: **Proposto**, **Aceito**, **Substituído por ADR-XXXX**. Um ADR aceito não é editado no mérito:
  mudanças geram um novo ADR que substitui o anterior.
- Specs citam os ADRs dos quais dependem; nenhuma spec dependente começa antes de o ADR estar Aceito.
- O índice `docs/adr/README.md` é atualizado a cada ADR novo ou mudança de status.

## Alternativas consideradas

- **Wiki externa:** fica fora do repositório e da IA; perde o vínculo com o código.
- **Sem registro:** decisões se perdem entre sessões; a IA tende a reabrir discussões já resolvidas.

## Consequências (positivas / negativas / riscos)

- (+) Histórico rastreável; a IA carrega as decisões a partir do repositório.
- (−) Custo pequeno de escrita a cada decisão.
- (risco) ADRs desatualizados: mitigado pela regra de substituição e pela revisão ao fim de cada fase.

## Specs afetadas

Todas.
