# NanoUint.Editor

Visual scene editor for the [NanoUint](../NanoUint) engine, together with its xunit test suite.

| Path | Contents |
|---|---|
| `NanoUintEditor/` | WPF editor (net10.0-windows) |
| `NanoUintEditor.Tests/` | xunit tests for the editor model, code generator and undo stack |

## Build

Both projects reference `..\NanoUint\NanoUint.csproj`, so clone the engine next to this repository:

```bash
git clone https://github.com/NanoUint/NanoUint.git
git clone https://github.com/NanoUint/NanoUint.Editor.git
```

Then, from inside `NanoUint.Editor`:

```bash
dotnet build NanoUintEditor/NanoUintEditor.csproj
dotnet test  NanoUintEditor.Tests/NanoUintEditor.Tests.csproj
```

Features, shortcuts and workflow are documented in `NanoUintEditor/README.md`.
