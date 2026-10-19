# ADR-0010: Conectividade externa (Tailscale)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O controle precisa funcionar fora de casa. O roteador é da operadora e expor a porta do agente na internet aumentaria
muito a superfície de ataque.

## Decisão

- **Tailscale (recomendado):** PC e celular na mesma tailnet; o app usa o IP `100.x` ou o nome MagicDNS do PC
  (incluído no QR de pareamento). Nenhuma porta aberta no roteador.
- **Cloudflare Tunnel (alternativa documentada):** só se o Tailscale não for possível. O TLS termina na Cloudflare, o
  que impede o pinning ponta a ponta do certificado autoassinado; exigiria Cloudflare Access na frente.
- **Nunca** redirecionamento de porta no roteador.
- Regra de firewall do Windows criada pelo instalador: porta 47800 liberada só no perfil **Privado** e na interface do
  Tailscale; bloqueada no perfil Público.

## Alternativas consideradas

- **Port forwarding:** proibido.
- **Relay próprio na nuvem:** custo e manutenção de um servidor; contradiz "sem conta em nuvem".

## Consequências (positivas / negativas / riscos)

- (+) Acesso externo cifrado (WireGuard) sem expor portas.
- (−) Requer o app Tailscale no celular e no PC (documentar no README).
- (risco) Celular fora da tailnet: o app mostra "PC inacessível" com dica de ativar o Tailscale.

## Specs afetadas

002, 004, 027.
