# Tools

| Area | Purpose |
| --- | --- |
| `Elyndor.ContentValidator/` | Validate composed content; audit icons, talents, balance/progression; export inspection JSON. |
| `assets/` | Offline art import/conversion/slicing and associated manifests. |
| `dev/` | Windows launcher and launcher smoke tests. |
| `repository/` | Dependency-free Node checks for repository hygiene/navigation. |

The [.NET tool manifest](../.config/dotnet-tools.json) is at the conventional
repository-local location. Run `dotnet tool restore` from the repository root.

```powershell
node --test tools/repository/check-layout.test.mjs
node tools/repository/check-layout.mjs
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
```

See [asset tooling](assets/README.md) and [launcher setup](../docs/development/getting-started.md).
Do not put one-off content mutation generators or personal absolute-path scripts
in the repository root. Generated outputs belong under ignored `output/` or
`.artifacts/`, except explicitly reviewed runtime assets and audit exports.
