# Spec 013: Comandos rápidos, voz e imagens no chat do Claude

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 3 — Ferramentas de desenvolvimento · **ADRs:** 0004 · **Depende de:** 008

## Problema

Digitar prompts longos no celular é lento. Quero atalhos para pedidos frequentes, ditar por voz e mandar fotos ou
prints para o Claude.

## Escopo

### Dentro
- **Comandos rápidos** globais e por projeto, com variáveis `{projeto}`, `{arquivo}`, `{branch}`, ordenação e
  sugestões padrão: "Rode os testes", "Corrija os erros do build", "Faça o commit com uma mensagem descritiva",
  "Explique o que você alterou", "Revise o diff atual".
- **Voz** em pt-BR (`speech_to_text`), texto editável e enviado só ao tocar em Enviar.
- **Imagens** (câmera, galeria, print): redimensionadas no app (lado maior ≤ 1568 px, JPEG/PNG), enviadas como bloco
  `image` base64 no stream-json (formato confirmado no ADR-0004).
- Colar texto da área de transferência do celular.
- Barra de comandos rápidos no chat.

### Fora
- Transcrição no PC; anexar arquivos que não sejam imagem (usar o gerenciador de arquivos, spec 020).

## Modelo de domínio

`QuickCommand` (ver `data-model.md`).

## Contrato de API

| Hub | Permissão |
|---|---|
| `ListQuickCommands(projectId?)` | `claude.view` |
| `UpsertQuickCommand(dto)`, `DeleteQuickCommand(id)` | `claude.chat` |
| `SendClaudeMessage(sessionId, content[])` com blocos `image` | `claude.chat` |

## Autorização

Como na spec 008.

## Casos de borda e falhas

- Variável sem valor (ex.: `{arquivo}` sem arquivo selecionado) → o app pede o valor antes de enviar.
- Imagem muito grande mesmo após redução (> 5 MB) → recusada com mensagem.
- Permissão de microfone/câmera negada → orientação para habilitar.
- Reconhecimento de voz indisponível offline → aviso.

## Requisitos não funcionais

- Redimensionamento no app < 1 s para foto de 12 MP.
- Limite de mensagem do Hub ajustado para imagens (documentado na spec 026).

## Integrações

`speech_to_text`, `image_picker`.

## Critérios de aceite

- **AC-1:** Dado um comando rápido com `{projeto}`, quando o uso, então o texto enviado tem o nome do projeto substituído.
- **AC-2:** Dado uma foto de 12 MP, quando anexo, então ela é reduzida antes do envio e o Claude a recebe.
- **AC-3:** Dado o ditado por voz, então o texto aparece editável e só é enviado ao tocar em Enviar.

## Decisões adiadas

- Nenhuma.

## Perguntas em aberto

1. Comandos rápidos editáveis **pelo app** ou só na bandeja?
2. Limite de **imagens por mensagem** (ex.: 5)?
