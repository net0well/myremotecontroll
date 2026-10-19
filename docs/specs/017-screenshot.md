# Spec 017: Screenshot sob demanda

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 4 — Tela · **ADRs:** 0012 · **Depende de:** 004

## Problema

Quero ver rapidamente o que está na tela do PC (ou num monitor específico) e guardar ou compartilhar a imagem.

## Escopo

### Dentro
- `ListMonitors()` com nome, resolução nativa, escala (DPI), posição e principal.
- Screenshot de um monitor ou de todos (composição pela área virtual), PNG na resolução nativa, via `IScreenCapture`
  (WGC com fallback DXGI).
- **App:** visualização com zoom e arraste, salvar na galeria, compartilhar.
- Auditoria de cada captura (ver tela é leitura sensível — confirmar na spec 003).

### Fora
- Espelhamento ao vivo (018).

## Modelo de domínio

Nenhuma entidade persistida.

## Contrato de API

| Tipo | Nome | Permissão | Erros |
|---|---|---|---|
| Hub | `ListMonitors()` | `screen.view` | |
| REST | `GET /api/v1/screen/screenshot?monitor=` → `image/png` | `screen.view` | `screen.secureDesktop`, `screen.monitorNotFound` |

## Autorização

Todos os papéis têm `screen.view` (seção 9).

## Casos de borda e falhas

- Tela de bloqueio/UAC (área de trabalho segura) → `screen.secureDesktop`.
- Monitores com escalas diferentes → cada um na resolução nativa; composição sem distorção.
- Monitor desconectado entre listar e capturar → `screen.monitorNotFound`.
- Conteúdo protegido (DRM) → aparece preto (limitação do Windows, documentada).

## Requisitos não funcionais

- Screenshot de 1 monitor 1080p < 1 s na LAN.

## Integrações

Windows.Graphics.Capture, DXGI (Vortice.Windows).

## Critérios de aceite

- **AC-1:** Dado 2 monitores, quando peço screenshot do monitor 2, então recebo apenas esse monitor na resolução nativa.
- **AC-2:** Dado a tela de bloqueio ativa, então recebo `screen.secureDesktop`.
- **AC-3:** Dado uma captura, então ela fica registrada na auditoria.

## Decisões adiadas

- Formato JPEG opcional para economizar dados: avaliar com o uso.

## Perguntas em aberto

1. Mostrar a **borda de privacidade** no PC também durante screenshots (um "flash" rápido)?
