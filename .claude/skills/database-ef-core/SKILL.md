---
name: database-ef-core
description: Use when working with the database — Entity Framework Core entities, DbContext, EF configurations, migrations, or writing/optimizing LINQ queries. Provides code-first conventions, file locations, and query best practices (navigation properties, AsNoTracking, Select projections) (project)
---

# Database & Entity Framework Core

The backend uses PostgreSQL with Entity Framework Core, code-first.

## When to Use This Skill

Use when you touch the data layer:

- Adding or changing domain entities
- Writing or editing EF configurations (Fluent API)
- Creating or reviewing migrations
- Writing or optimizing LINQ queries in handlers

For the overall backend feature structure (endpoint/request/validator/handler), use the `vertical-slice-architecture` skill — this skill is only the data layer.

## PostgreSQL Setup

- Connection string: configured via environment variables (managed by .NET Aspire locally)
- Migrations: Entity Framework Core migrations
- Migrations location: `backend/src/DrimDev.Api/Database/Migrations/`

## Code-First Conventions

- **Code-first approach** — schema is generated from entities + configurations.
- **Domain entities**: `backend/src/DrimDev.Api/Domain/{Domain}/` grouped by domain (e.g., `Domain/Guides/Guide.cs`).
- **DbContext**: `backend/src/DrimDev.Api/Database/AppDbContext.cs`, namespace `DrimDev.Api.Database`.
- **EF Configurations**: `backend/src/DrimDev.Api/Database/Configurations/` for Fluent API configurations.
- **Migrations**: `backend/src/DrimDev.Api/Database/Migrations/`.

**Always review generated migrations before applying them** — EF can produce surprising column drops, renames, or unintended cascades.

## EF Core Best Practices

1. **Navigation Properties** — always define relationships explicitly in domain entities to simplify LINQ.
   - Example: `public List<GuideTag> GuideTags { get; set; }`
   - Enables cleaner queries: `.Include(g => g.GuideTags)` instead of manual joins.

2. **`AsNoTracking()` for read-only queries** — always use it when retrieving data that will not be modified. Skips change-tracking overhead.
   - Example: `await _db.Guides.AsNoTracking().Where(g => g.Slug == slug).FirstOrDefaultAsync(ct);`

3. **`Select()` for projections** — project in the database, not in memory. Reduces data transferred.
   - Example: `.Select(s => new { s.Id, s.Name })` instead of loading full entities and mapping in C#.

## Referential Integrity & Delete Behavior

- Set delete behavior explicitly in the EF configuration (`OnDelete(...)`). `Restrict` blocks deletes that would orphan referencing rows — a delete against a referenced row then throws a DB-level FK violation (surfaces as 500 unless the handler checks first).
- When implementing a delete on an entity others reference, check for references in the handler and return a domain error (e.g. 409) rather than letting the FK violation surface raw. See `Features/Groups/DeleteGroup.cs` for the reference pattern.
