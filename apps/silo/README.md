# Velucid Silo (apps/silo)

This Nx project is a wrapper around the existing .NET 10 Orleans Silo solution
at `backend/src/Velucid.Silo/`. It is **not** a re-implementation — the .NET
code remains the source of truth for grain-based business logic.

## Why a wrapper?

The wrapper lets the rest of the workspace invoke silo build/test/serve via
Nx targets while the actual implementation stays in `backend/src/`. This
keeps the .NET tooling chain (Orleans, EF Core, KurrentDB .NET client) in
its existing location and avoids a disruptive directory move. Cleanup of
the legacy `backend/src/` path is scheduled for Story 4.5.

## Targets

- `nx build silo` — `dotnet build backend/src/Velucid.Silo/Velucid.Silo.csproj -c Release`
- `nx test silo` — `dotnet test backend/tests --no-build -c Release`
- `nx serve silo` — `dotnet run --project backend/src/Velucid.Silo/Velucid.Silo.csproj`
- `nx lint silo` — `dotnet format ... --verify-no-changes`
- `nx typecheck silo` — `dotnet build ... --no-restore`

## Future: migration to `@nx/dotnet` plugin

If/when the team wants richer Nx graph integration (e.g. affected
computation across .csproj references), the project can be regenerated via
`bunx nx g @nx/dotnet:app apps/silo` which produces a richer project.json.
The current `nx:run-commands` shape is intentionally minimal for the
bootstrap story.
