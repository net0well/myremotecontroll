# Spec 020: Gerenciador de arquivos

**Status:** Draft
**Data:** 2026-10-18
**Fase:** 5 — Sistema · **ADRs:** 0005, 0009 · **Depende de:** 003, 004

## Problema

Quero navegar nas pastas do PC, baixar e enviar arquivos, pré-visualizar código, imagens e PDFs e mandar fotos do
celular para o PC, sem expor o disco inteiro.

## Escopo

### Dentro
- **Raízes permitidas** (`AllowedRoot` com `Kind` Files), cadastradas na bandeja; pasta **Inbox** padrão para uploads.
- **Validação de caminhos** (núcleo de segurança, com testes extensivos): normalização, bloqueio de path traversal,
  links simbólicos/junctions que saiam da raiz (resolução do destino final), ADS (`arquivo:stream`), nomes reservados
  (`CON`, `NUL`, `COM1`…), caminhos UNC/dispositivo (`\\?\`, `\\.\`), suporte a caminhos > 260 caracteres.
- Listar e ordenar; buscar por nome (e por conteúdo em pastas de código, com limite de tempo e tamanho).
- **Download** em streaming com `Range` (retomada) e progresso.
- **Upload** em streaming com progresso (limite configurável, padrão 2 GB), conflito de nome → renomeia
  ("foto (1).jpg"), escrita em arquivo temporário e troca atômica.
- Renomear, mover, copiar, criar pasta; **excluir para a Lixeira** (`SHFileOperation`/`IFileOperation` com undo).
- Pré-visualização: imagem, PDF, texto e código com destaque de sintaxe (limite de tamanho).
- **App:** navegador com breadcrumbs, busca, pré-visualização, download/upload com progresso, ações; **compartilhar do
  celular para o PC** (`receive_sharing_intent`) → Inbox.
- Card "Transferências" no painel (uploads/downloads em andamento).

### Fora
- Sincronização de pastas; edição de arquivos no app.

## Modelo de domínio

`AllowedRoot` (ver `data-model.md`); transferências em memória.

## Contrato de API

Ver `hub-contract.md` §2: `GET /files/roots`, `/files/list`, `/files/search`, `/files/download`, `/files/preview`,
`POST /files/upload`, `POST /files/ops`. Erros: `files.outsideRoot`, `files.notFound`, `files.inUse`,
`files.tooLarge`, `files.invalidName`, `files.rootUnavailable`.

## Autorização

`files.read` e `files.write`: Operador+; `files.delete`: Administrador (+ biometria). Raízes com `CanWrite = false`
recusam escrita para todos.

## Casos de borda e falhas

- `path=..\..\Windows` → `files.outsideRoot` + auditoria.
- Junction dentro da raiz apontando para fora → `files.outsideRoot`.
- Arquivo em uso → `files.inUse`.
- Nomes com caracteres inválidos → `files.invalidName`.
- Raiz removida ou unidade desconectada → `files.rootUnavailable`.
- Upload interrompido → temporário removido; nada parcial no destino.

## Requisitos não funcionais

- Download/upload limitados só pela rede (streaming, sem carregar em memória).
- Listagem de 5.000 itens < 1 s.

## Integrações

Shell do Windows (Lixeira), `receive_sharing_intent`, `file_picker`.

## Critérios de aceite

- **AC-1:** Dado `path=..\..\Windows`, então recebo `files.outsideRoot` e a tentativa é auditada.
- **AC-2:** Dado um download interrompido, quando retomo, então ele continua do byte em que parou.
- **AC-3:** Dado excluir um arquivo, então ele vai para a Lixeira, não é apagado definitivamente.
- **AC-4:** Dado compartilhar 3 fotos da galeria para o MyRemoteControll, então elas chegam na Inbox do PC.
- **AC-5:** Dado uma junction dentro da raiz que aponta para fora dela, quando acesso um arquivo por ela, então recebo `files.outsideRoot`.

## Decisões adiadas

- Pré-visualização de vídeo: fora desta versão.

## Perguntas em aberto

1. Quais pastas serão as **raízes iniciais** (ex.: Documentos, Downloads, pasta de projetos)? E onde fica a **Inbox**?
2. Limite de upload de **2 GB** está ok?
