# Spec 015: Preview do localhost

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 3 — Ferramentas de desenvolvimento · **ADRs:** 0017, 0006 · **Depende de:** 004

## Problema

Quero ver no celular o app web que estou desenvolvendo no PC (ex.: Vite na porta 5173), com hot reload, sem mudar o
bind do servidor de dev e sem expô-lo na rede.

## Escopo

### Dentro
- Listar portas TCP em escuta com processo dono (`GetExtendedTcpTable`), só locais (`127.0.0.1`, `::1`, `0.0.0.0`),
  excluindo as portas do agente.
- Proxy **YARP** `/preview/{porta}/{**caminho}` com WebSocket (hot reload) e reescrita de `Host`, `Origin`, `Location`.
- Autenticação por **ticket de uso único** → cookie `HttpOnly; Secure; SameSite=Strict` restrito a `/preview/{porta}/`.
- **App:** lista de portas, WebView (`webview_flutter`) com recarregar, voltar e alternar user-agent mobile/desktop.
- **Primeiro passo da spec:** prova de conceito de TLS na WebView com o certificado autoassinado (validação do pin);
  se não for possível, o preview exige o certificado do Tailscale (ADR-0006, opção B).

### Fora
- Modo "um listener por porta" (alternativa do ADR-0017), salvo se o modo por caminho falhar com os frameworks usados.

## Modelo de domínio

Tickets em memória (uso único, 60 s).

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| Hub | `ListListeningPorts()` | `dev.preview` | |
| Hub | `CreatePreviewTicket(port)` → URL | `dev.preview` | `preview.portNotAllowed` |
| HTTP | `ANY /preview/{port}/{**path}` | cookie de preview | 403 |

## Autorização

Operador+.

## Casos de borda e falhas

- Apps com caminhos absolutos (`/assets`) → documentar; avaliar modo por listener.
- Cookies do app de dev → isolados por caminho.
- Porta do agente ou não local → `preview.portNotAllowed`.
- Servidor de dev parou → página de erro amigável.

## Requisitos não funcionais

- Hot reload funcionando pelo proxy; latência adicional < 50 ms na LAN.

## Integrações

YARP, `webview_flutter`.

## Critérios de aceite

- **AC-1:** Dado um `npm run dev` na porta 5173, quando escolho a porta, então a página abre no celular e o hot reload funciona.
- **AC-2:** Dado um aparelho sem `dev.preview`, então o proxy responde 403.
- **AC-3:** Dado um ticket já usado, então o proxy recusa a abertura.

## Decisões adiadas

- Modo por listener dedicado: só se a prova de conceito mostrar necessidade.

## Perguntas em aberto

1. Quais frameworks de front você usa mais (Vite, Next, Angular, Flutter web)? Eles definem os testes do proxy.
