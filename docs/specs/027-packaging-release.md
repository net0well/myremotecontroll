# Spec 027: Empacotamento e entrega

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 7 — Segurança final e entrega · **ADRs:** 0019, 0003, 0010, 0014 · **Depende de:** todas as anteriores

## Problema

Quero instalar o MyRemoteControll num Windows limpo em poucos minutos, desinstalar sem deixar rastros e ter o app
instalado nos meus aparelhos.

## Escopo

### Dentro
- `dotnet publish` self-contained single-file win-x64 do agente e do serviço de sensores.
- **Instalador Inno Setup:** copia arquivos; cria a tarefa de autostart; instala o serviço de sensores e o PawnIO
  (com consentimento); cria a regra de firewall (porta 47800 só no perfil Privado e na interface do Tailscale); gera o
  certificado; oferece instalar os hooks do Claude.
- **Desinstalador limpo:** remove tarefa, serviço, regras de firewall e hooks do Claude (com backup); pergunta se
  apaga dados (`%LOCALAPPDATA%\MyRemoteControll`).
- Atualização: verificação manual de versão (sem auto-update).
- **App:** APK/AAB assinado (Android) e TestFlight (iOS, se houver conta Apple Developer).
- **README completo:** pré-requisitos, instalação, pareamento, Tailscale, hooks e MCP do Claude Code, Firebase/ntfy,
  solução de problemas, limitações (tela de bloqueio/UAC, sensores, preview).

### Fora
- Loja (Play Store/App Store); assinatura de código do executável (opcional, paga).

## Modelo de domínio

Nenhum.

## Contrato de API

Nenhum novo.

## Autorização

O instalador pede elevação (serviço, driver, firewall); o agente instalado roda sem elevação.

## Casos de borda e falhas

- Reinstalação sobre versão anterior → preserva dados e certificado.
- SmartScreen (executável sem assinatura) → documentado no README.
- PawnIO recusado → instala sem sensores.

## Requisitos não funcionais

- Instalação + pareamento < 2 min num Windows limpo.

## Integrações

Inno Setup, Agendador de Tarefas, Firewall do Windows, Service Control Manager.

## Critérios de aceite

- **AC-1:** Dado um Windows limpo, quando rodo o instalador, então em até 2 min consigo parear o app.
- **AC-2:** Dado a desinstalação, então não restam tarefas, serviços, regras de firewall nem hooks do MyRemoteControll.

## Decisões adiadas

- Auto-update: reavaliar depois da primeira versão.

## Perguntas em aberto

1. Distribuição do APK: arquivo direto ou Play Store (teste interno)?
