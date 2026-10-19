# ADR-0019: Distribuição (single-file + instalador)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O agente precisa ser instalado num Windows limpo em poucos minutos: executáveis, tarefa de autostart, serviço de
sensores, driver PawnIO, regra de firewall e certificado. O app vai para Android e iOS.

## Decisão

- Agente e serviço de sensores publicados com `dotnet publish` **self-contained, single-file, win-x64**
  (sem trimming, que não é suportado no WinForms).
- **Instalador Inno Setup** (recomendado): copia arquivos, cria a tarefa agendada, instala o serviço e o PawnIO
  (com consentimento), cria a regra de firewall (perfil Privado + Tailscale) e gera o certificado; desinstalador limpo
  (remove tarefa, serviço, firewall e hooks do Claude, com backup). Script PowerShell como alternativa de desenvolvimento.
- Atualização: verificação manual de versão (sem auto-update na v1).
- App: APK/AAB assinado (Android); TestFlight (iOS, exige conta Apple Developer).

## Alternativas consideradas

- **MSIX:** sandbox e empacotamento complicam o serviço elevado, o driver e o autostart por tarefa.
- **Só script PowerShell:** sem desinstalador amigável nem registro em "Aplicativos instalados".

## Consequências (positivas / negativas / riscos)

- (+) Instalação em um passo e desinstalação limpa.
- (−) Executável sem assinatura de código dispara o SmartScreen; assinatura exige certificado pago (opcional).

## Specs afetadas

027, 001, 014, 024.
