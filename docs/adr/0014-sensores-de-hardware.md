# ADR-0014: Sensores de hardware (serviço elevado separado)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Temperaturas, ventoinhas e consumo exigem LibreHardwareMonitorLib, que precisa de privilégio de administrador e do
driver **PawnIO** (que substituiu o WinRing0). O agente principal roda sem elevação (ADR-0003).

## Decisão

- Executável separado **`MyRemoteControll.Sensors`** (Worker Service) instalado como **serviço do Windows** elevado.
- Expõe **somente leitura** por **named pipe** local (`\\.\pipe\MyRemoteControll.Sensors`), com ACL que permite acesso
  apenas ao SID do usuário que instalou o agente. Protocolo simples (JSON por linha): `snapshot` e `list-sensors`.
- Nenhum comando de escrita, nenhuma entrada do app chega ao serviço: só o agente o consulta.
- O instalador instala o PawnIO e o serviço **com consentimento explícito**.
- Se o serviço ou o PawnIO estiverem ausentes, o agente mostra os sensores como "indisponível" com instruções, e o
  restante do monitoramento segue funcionando.

## Alternativas consideradas

- **Agente inteiro elevado:** amplia muito a superfície de ataque (tudo que é remoto rodaria como administrador) e
  atrapalha a interação com a sessão do usuário.
- **Sem temperaturas:** perde uma funcionalidade pedida.

## Consequências (positivas / negativas / riscos)

- (+) Privilégio elevado restrito a um componente pequeno e somente leitura.
- (−) Dois executáveis para instalar e atualizar.
- (risco) Licenças: LibreHardwareMonitorLib é MPL-2.0; conferir a licença e a forma de distribuição do PawnIO antes da spec 024.

## Specs afetadas

024, 027, 028.
