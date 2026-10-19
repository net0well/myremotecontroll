# MyRemoteControll Agent — .NET 10

Agente que roda na sessão do usuário do Windows (ícone de bandeja) e expõe a API/Hub que o app usa para controlar o
PC. Regras gerais do repositório em `../CLAUDE.md`; decisões em `../docs/adr/`; specs em `../docs/specs/`.

## Stack

- **.NET 10** / C# 14, `net10.0-windows` (WinForms para a bandeja + ASP.NET Core no mesmo processo)
- **ASP.NET Core Minimal APIs** — `IEndpointGroup` por feature, descoberto por `app.MapEndpoints(assembly)`
- **SignalR** — Hub único `/hubs/remote` (`Hub<IRemoteClient>`), métodos em arquivos `partial` por área em `Hubs/`
- **EF Core 10 + SQLite** — `%LOCALAPPDATA%\MyRemoteControll\data.db`, WAL, migrations aplicadas na inicialização
- **Sem mediator** — handlers como classes registradas no DI (ADR-0002)
- **Autenticação própria** — token de dispositivo opaco (ADR-0008), esquema `DeviceToken`; policies `perm:<chave>` (ADR-0009)
- **Serilog** — arquivo rotativo em `%LOCALAPPDATA%\MyRemoteControll\logs`, correlation ID por requisição
- **xUnit v3** (Microsoft Testing Platform) — integração sobe o Kestrel real em portas efêmeras; sem Testcontainers

## Arquitetura (Vertical Slice — ADR-0002)

```
src/
  MyRemoteControll.Agent/        # host: Program.cs, Hosting/, Tray/, Hubs/, Infrastructure/, Features/<Area>/
  MyRemoteControll.Windows/      # interop com o SO atrás de interfaces (processos, autostart, energia, captura...)
  MyRemoteControll.Claude/       # integração com o Claude Code CLI (spec 008+)
  MyRemoteControll.Contracts/    # DTOs, IRemoteClient, ErrorCodes compartilhados com o app
tests/
  MyRemoteControll.Agent.Tests/             # unidade
  MyRemoteControll.Agent.IntegrationTests/  # ponta a ponta (Support/TestAgent.cs)
```

- **Dois listeners:** público `:47800` (HTTPS, autenticado) e loopback `127.0.0.1:47801` (MCP e hooks do Claude).
  Endpoints do loopback usam `.LoopbackOnly()`; a decisão vem da conexão (`Listeners.GetListenerKind`), nunca do header Host.
- **Ciclo de vida:** `AgentRuntime` (estado, novas tentativas) → `AgentApplicationFactory` (monta e inicia o host).
- **Toda feature nova** publica seus dados no `DashboardAggregator` e ganha um card no painel (a partir da spec 007).

## Convenções

- C# 14, `sealed`, primary constructors, records, file-scoped namespaces, `CancellationToken` em tudo, `TimeProvider`
  (nunca `DateTime.Now`), `[LoggerMessage]` para logs, Result pattern + ProblemDetails com `code` estável
  (`MyRemoteControll.Contracts.Errors.ErrorCodes`).
- Processos externos sempre com `ProcessStartInfo.ArgumentList` via `IProcessRunner`.
- Comentários em pt-BR explicando o "porquê"; identificadores em inglês.
- Testes: `AC{n}_<Descricao>` para cada critério de aceite da spec; testes que mexem no sistema real ficam com
  `[Fact(Explicit = true)]` e limpam o que criam.

## Comandos

```bash
dotnet build                                   # 0 avisos (TreatWarningsAsErrors)
dotnet test                                    # unidade + integração
dotnet test --project tests/MyRemoteControll.Agent.Tests --explicit only   # testes de sistema (Agendador real)
dotnet format MyRemoteControll.slnx --verify-no-changes
dotnet ef migrations add <Nome> --project src/MyRemoteControll.Agent --output-dir Infrastructure/Persistence/Migrations
dotnet publish src/MyRemoteControll.Agent -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true
```

## Ferramentas

- Use as skills e comandos do dotnet-claude-kit (`/spec` → `/plan` → `/tdd`/`/scaffold` → `/verify` → `/code-review`).
- Prefira as ferramentas MCP do Roslyn (`find_symbol`, `get_file_outline`, `detect_antipatterns`, `get_diagnostics`)
  a ler arquivos inteiros.
- Nunca use versões de pacotes de memória: `dotnet add package <nome>` sem versão (Central Package Management).

## Antipadrões proibidos

- Endpoints em `Program.cs`; repositório genérico sobre o EF Core; `async void` (exceto handlers de evento da bandeja);
  `.Result`/`.Wait()` (exceto as fronteiras síncronas documentadas: `Main` STA e `SessionEnding`);
  `new HttpClient()` fora de testes; logs com tokens, conteúdo de arquivos ou mensagens do Claude.
