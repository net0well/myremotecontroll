# MyRemoteControll

Sistema para controlar um PC Windows pelo celular: agente .NET 10 no PC (`agent/`) + app Flutter (`app/`).

## Regras de ferramentas
- Para TODO código C#/.NET: use SEMPRE o plugin dotnet-claude-kit (skills, agentes, comandos e o MCP Roslyn).
  Fluxo: /spec → /plan → /tdd ou /scaffold → /verify → /code-review. Use /build-fix, /security-scan, /arch-check, /outdated, /checkpoint e /wrap-up.
- Para TODO código Flutter/Dart: use SEMPRE as skills de Flutter instaladas (plugin dart-flutter e dart-lang/skills) e o MCP do Dart/Flutter.
  Antes de cada tarefa no app, liste as skills disponíveis e carregue as que se aplicam.
- Nunca use versões de pacotes de memória; verifique a última versão estável e a licença.
- Para o Claude Code CLI (stream-json, hooks, MCP, permissões), consulte https://code.claude.com/docs antes de implementar.

## Documentos
- Base: docs/base/MyRemoteControll-base-specs.md (fonte de verdade inicial; o prompt original está em docs/base/MyRemoteControll-prompt.md)
- ADRs: docs/adr/ (decisões aceitas são obrigatórias)
- Specs: docs/specs/ (só implemente specs com status Approved)
- Arquitetura: docs/architecture/ (estrutura, modelo de dados, contrato do Hub)

## Ordem obrigatória
1. ADRs aceitos antes das specs que dependem deles.
2. Specs com status Approved antes do código. Se o código precisar divergir, a spec volta para In Review.
3. Nunca assumir: o documento base traz respostas prévias, que devem ser validadas com o usuário no /spec.

## Painel
- O painel em tempo real é a tela inicial e deve ser bonito (design system em docs/base, seção 4.3).
- Toda feature nova publica seus dados no DashboardAggregator e ganha um card no painel.

## Idioma
- Specs, ADRs, comentários e UI em pt-BR; identificadores de código em inglês.
