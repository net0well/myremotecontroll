# ADR-0015: Execução de Git (executável `git`)

**Status:** Aceito
**Data:** 2026-10-18

## Contexto

O app mostra status, diff e log dos projetos e executa commit, push, pull, branch e descarte. O Git precisa usar as
mesmas credenciais, hooks e configuração que o usuário usa no terminal.

## Decisão

- Executar o **`git` instalado** via `ProcessStartInfo.ArgumentList` (nunca string de shell), sempre com
  `-C <diretório do projeto da whitelist>`, atrás de `IProcessRunner`.
- Saídas estruturadas: `git status --porcelain=v2 --branch -z`, `git log` com `--format` próprio e separador nulo,
  `git diff` unificado com limite de tamanho (arquivos binários e enormes resumidos).
- Variáveis para nunca travar esperando interação: `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`; timeout por
  operação; credencial que exigiria interação falha com mensagem clara.
- Pull com `--ff-only` por padrão; conflito → nada alterado e erro `git.conflict` com a lista de arquivos.
- Descartar exige permissão `dev.git.discard` + biometria no app.

## Alternativas consideradas

- **LibGit2Sharp:** sem suporte completo ao Git Credential Manager, hooks do usuário e alguns recursos (ex.: sparse,
  filtros LFS); comportamento pode divergir do terminal.

## Consequências (positivas / negativas / riscos)

- (+) Comportamento idêntico ao do terminal (credenciais, hooks, config).
- (−) Depende do Git instalado e no PATH (verificado na inicialização, com aviso).
- (risco) Mudanças de formato de saída: mitigado pelo uso de formatos porcelain estáveis.

## Specs afetadas

012, 028.
