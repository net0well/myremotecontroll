# ADR-0017: Preview do localhost (YARP)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

Quero abrir no celular o servidor de desenvolvimento que roda no PC (ex.: Vite em `localhost:5173`), com hot reload,
sem mudar o bind do servidor de dev e sem expor a porta na rede.

## Decisão

- **Proxy reverso YARP autenticado** no listener público: `/preview/{porta}/{**caminho}`, com encaminhamento de
  WebSocket (hot reload) e reescrita de cabeçalhos (`Host`, `Origin`, `Location`).
- Só portas em escuta em `127.0.0.1`, `::1` ou `0.0.0.0` do próprio PC; portas do agente bloqueadas.
- **Autenticação na WebView:** a WebView não envia o header `Authorization` em sub-recursos. O app obtém um
  **ticket de uso único** pelo Hub e abre `/preview/{porta}/?ticket=...`; o agente troca o ticket por um cookie
  `HttpOnly; Secure; SameSite=Strict` restrito ao caminho `/preview/{porta}/` e de curta duração.
- Permissão `dev.preview`; sem ela, 403.

## Alternativas consideradas

- **Expor a porta de dev na rede:** sem autenticação nem TLS.
- **Subdomínio por porta:** resolve caminhos absolutos, mas exige DNS curinga, impossível com IP/autoassinado.
- **Um listener por porta de preview (ex.: 47810+):** também resolve caminhos absolutos (`/assets`); avaliar na spec 015
  se o modo por caminho falhar com os frameworks usados.

## Consequências (positivas / negativas / riscos)

- (+) Nada muda no servidor de dev; acesso autenticado e cifrado.
- (−) Apps com caminhos absolutos podem quebrar no modo por caminho (documentar; alternativa acima).
- (risco) **WebView e certificado autoassinado:** a WebView usa a pilha TLS da plataforma e recusa o certificado do
  agente. Verificar se o `webview_flutter` permite tratar o erro de TLS validando o pin; senão, o preview exige o
  certificado do Tailscale (ADR-0006, opção B).

## Specs afetadas

015, 028.
