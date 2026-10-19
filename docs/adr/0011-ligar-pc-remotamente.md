# ADR-0011: Ligar o PC remotamente (fora do escopo)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Ligar o PC à distância exigiria Wake-on-LAN a partir de um dispositivo na mesma rede ou acesso ao roteador. O roteador é
da operadora (sem SSH nem WoL) e desligar/reiniciar já atende ao caso de uso.

## Decisão

- **Fora do escopo.** Não implementar Wake-on-LAN nem mecanismos equivalentes.
- A spec 006 permanece removida (numeração mantida).
- Como consequência, o app deixa explícito, na confirmação de **Desligar**, que o PC só poderá ser ligado
  presencialmente, e sugere **Reiniciar** ou **Bloquear** quando fizer sentido (R13).

## Alternativas consideradas (reavaliar se a necessidade surgir)

- Aparelho auxiliar sempre ligado na tailnet (Raspberry Pi, Android antigo) enviando o pacote WoL.
- ESP32 com firmware de WoL.
- Tomada inteligente + BIOS "ligar ao voltar energia".
- Relé no botão de power.

## Consequências (positivas / negativas / riscos)

- (+) Escopo menor e sem hardware extra.
- (−) Desligamento remoto por engano deixa o PC inacessível até alguém ligá-lo: mitigado por confirmação explícita,
  biometria e contagem regressiva com cancelamento (spec 005).

## Specs afetadas

005 (texto de confirmação), 006 (removida).
