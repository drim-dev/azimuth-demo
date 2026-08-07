---
name: code-style
description: "Use when writing or reviewing code comments or naming things (variables, functions, types, files) — worked examples for the project's two hard rules: comments explain WHY not WHAT, and names describe purpose not implementation/history (project)"
---

# Code Style — Comments & Naming

Worked examples for two project rules stated in AGENTS.md. The rules themselves are mandatory and always apply; this skill is the reference with concrete before/after.

## Comments: explain WHY, never WHAT

**CRITICAL RULE: comments that describe WHAT the code does are FORBIDDEN.** If you can understand what the code does by reading it, do NOT add a comment. Method names, variable names, and structure should make the code self-documenting.

- NEVER describe operations: "Check if X exists", "Create Y", "Update Z", "Delete A"
- NEVER restate method calls: if the code says `.AnyAsync(...)`, don't write "Check if exists"
- NEVER annotate variable assignments: "Create tag", "Get user", "Build response"
- Good comments explain **WHY** or important business context: non-obvious constraints, business rules, security requirements, surprising behavior
- When in doubt, delete the comment

**Bad (self-evident — FORBIDDEN):**
```csharp
// Check if slug already exists
var slugExists = await _db.Tags.AsNoTracking().AnyAsync(t => t.Slug == request.Slug, ct);
if (slugExists)
{
    throw new ConflictException("A tag with this slug already exists");
}

// Create tag
var tag = new Tag
{
    Id = _idFactory.CreateId(),
    Name = request.Name,
    Slug = request.Slug,
    CreatedAt = DateTime.UtcNow
};

// Save to database
await _db.Tags.AddAsync(tag, ct);
await _db.SaveChangesAsync(ct);
```

**Good (explains WHY) — or better, NO comment when the code is clear:**
```csharp
// Slugs must be unique across all tags for URL routing
var slugExists = await _db.Tags.AsNoTracking().AnyAsync(t => t.Slug == request.Slug, ct);
if (slugExists)
{
    throw new ConflictException("A tag with this slug already exists");
}

var tag = new Tag
{
    Id = _idFactory.CreateId(),
    Name = request.Name,
    Slug = request.Slug,
    CreatedAt = DateTime.UtcNow
};

await _db.Tags.AddAsync(tag, ct);
await _db.SaveChangesAsync(ct);
```

**Forbidden comments** (code already shows this): "Check if X exists", "Create X", "Update X", "Delete X", "Get X by Y", "Save to database", "Return response".

**Acceptable comments** (explain WHY/business context):
- "Tag slugs must be unique for URL routing" — business rule
- "Soft delete preserves referential integrity" — WHY we soft delete
- "IdGen generates time-ordered IDs for pagination" — non-obvious benefit
- "Base32 encoding for URL-safe public IDs" — design decision

**Before writing a comment, ask:** Does it explain WHY (not WHAT)? Would a developer understand the code without it? Is there non-obvious business logic? If "no" to the first or "yes" to the second — delete it.

## Naming: describe purpose, not implementation or history

Names MUST tell what the code does, not how it's implemented or its history.

- When changing code, never document the old behavior or the behavior change in a name
- NEVER use implementation details: "ZodValidator", "MCPWrapper", "JSONParser"
- NEVER use temporal/historical context: "NewAPI", "LegacyHandler", "UnifiedTool", "ImprovedInterface", "EnhancedParser"
- NEVER use pattern names unless they add clarity (prefer "Tool" over "ToolFactory")

**Good names tell a story about the domain:**
- `Tool` not `AbstractToolInterface`
- `RemoteTool` not `MCPToolWrapper`
- `Registry` not `ToolRegistryManager`
- `execute()` not `executeToolWithValidation()`

If you catch yourself writing "new", "old", "legacy", "wrapper", "unified", or implementation details in a name or comment — STOP and find a name that describes the thing's actual purpose.
