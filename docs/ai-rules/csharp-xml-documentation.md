# C# XML Documentation Comments

Read before writing, reviewing, or maintaining XML documentation comments on C# code.

This file is the standard for writing, reviewing, and maintaining C# XML documentation comments. Apply it to all C# code in this repository.

When asked to write documentation, produce comments that satisfy these rules. When asked to review documentation, identify the rule each comment violates and show the corrected version. If the requested scope is ambiguous, ask once and then proceed.

The goal is quick understanding, not formal or exhaustive prose. Use plain, direct language and familiar words. A reader should normally understand a member after reading two or three short lines. Add more detail only when it explains behavior, context, or a constraint that matters.

## Tag taxonomy

| Tag | Purpose | Required on |
| --- | --- | --- |
| `<summary>` | One-sentence “what” | Every non-private member; private members according to the policy below |
| `<remarks>` | “Why” and “how”; architectural context, side effects, and constraints | Types, complex methods, and non-obvious properties |
| `<param name="...">` | What the parameter represents and its constraints | Every documented method or constructor parameter |
| `<typeparam name="...">` | What the type parameter represents | Every generic type or method |
| `<returns>` | What the return value represents | Every documented non-void method |
| `<value>` | What a property value represents when the summary is insufficient | Optional for properties with non-obvious values |
| `<exception cref="...">` | The condition under which an exception is thrown | Every explicitly thrown exception |
| `<inheritdoc />` | Documentation inherited from a base or interface member | Overrides and interface implementations whose contract is unchanged |
| `<see cref="...">` | A cross-reference to a type or member | Inline wherever precision requires it |
| `<see langword="..."/>` | A C# keyword such as `null`, `true`, `false`, `async`, or `await` | Always; never use backticks for keywords in XML comments |
| `<paramref name="..."/>` | An inline parameter reference | Inside parameter, remarks, or exception prose |
| `<para>` | A distinct idea inside remarks | Whenever remarks contain more than one thought |
| `<example>` and `<code>` | A usage demonstration | Entry-point APIs whose usage is not obvious |

Documentation must be well-formed XML. Keep every `cref` and `name` attribute accurate so compiler validation succeeds.

## Canonical phrasing

Use the established patterns below. Keep the wording natural and replace stock phrases when a simpler explanation is clearer.

### Constructors

```csharp
/// <summary>
///     Initializes a new instance of the <see cref="PermissionScanner" /> class.
/// </summary>
```

### Boolean properties

```csharp
/// <summary>
///     Gets or sets a value indicating whether discovered permissions are cached between scans.
/// </summary>
/// <value>
///     <see langword="true" /> if results are cached; otherwise, <see langword="false" />.
///     The default is <see langword="true" />.
/// </value>
```

### Other properties

Begin with “Gets” or “Gets or sets” and state the default when one exists.

```csharp
/// <summary>
///     Gets the namespace into which generated permission constants are emitted.
/// </summary>
```

### Methods

Use a third-person present-tense verb. Do not begin with “This method”.

```csharp
/// <summary>
///     Scans the given assembly for types decorated with permission attributes.
/// </summary>
```

### Async methods

Lead with “Asynchronously”. The return documentation always follows the task-result formula.

```csharp
/// <summary>
///     Asynchronously scans the given assembly for permission declarations.
/// </summary>
/// <returns>
///     A task that represents the asynchronous operation. The task result contains
///     the set of discovered <see cref="PermissionDescriptor" /> instances.
/// </returns>
```

For a plain `Task`, use: `A task that represents the asynchronous operation.`

Include:

```csharp
/// <param name="cancellationToken">
///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
/// </param>
```

Where applicable, include:

```csharp
/// <exception cref="OperationCanceledException">
///     If the <see cref="CancellationToken" /> is canceled.
/// </exception>
```

### Fluent builders

```csharp
/// <returns>The same builder instance so that multiple calls can be chained.</returns>
```

### Events

```csharp
/// <summary>
///     Occurs when a duplicate permission key is detected during generation.
/// </summary>
```

### Types

Use “Represents” for domain objects, “Defines” for contracts and enums, and “Provides” for static helper classes.

```csharp
/// <summary>
///     Represents a single discovered permission in the <c>{domain}.{resource}.{action}</c> convention.
/// </summary>
```

### Enums

Document both the enum type and every member. Member summaries are noun phrases and do not begin with “Indicates that”.

```csharp
/// <summary>
///     Defines the strategies for resolving duplicate permission keys.
/// </summary>
public enum DuplicateKeyStrategy
{
    /// <summary>
    ///     The first declaration wins; later duplicates are ignored.
    /// </summary>
    FirstWins,
}
```

### Overrides and interface implementations

Use `<inheritdoc />` when the contract is unchanged. If the implementation adds notable behavior, retain `<inheritdoc />` and append remarks.

```csharp
/// <inheritdoc />
/// <remarks>
///     This implementation caches results per assembly; see <see cref="ClearCache" />.
/// </remarks>
```

## Core XML documentation rules

### Summary

Write one complete sentence answering “what does this do?”. End it with a period. Use plain words, keep it short, and say what would help a reader understand the member. Do not explain implementation details or repeat the member name. Indent summary text four spaces inside the tag unless the surrounding codebase has an established different style.

### Remarks

Use remarks to answer why a member exists and how it behaves. Keep the explanation direct and include only context that helps someone use or maintain the code.

- Use one `<para>` for each distinct idea.
- Use the first paragraph for the system role or behavioral contract.
- Use later paragraphs for side effects, ordering constraints, thread safety, and lifetime.
- For dependency-injection services, state the lifetime and thread-safety contract explicitly.
- Link conceptual documentation with `<see href="...">` when relevant.
- Omit remarks on simple, self-evident members.
- Do not narrate history: no "no longer", "used to", or "does not do X" where X was never part of the contract. Describe only what the member does now (see `core-principles.md`).

For example, adjust this lifetime contract to match the service:

```text
The implementation may depend on other services registered with any lifetime.
The implementation does not need to be thread-safe.
```

Use an XML link for conceptual documentation:

```csharp
/// See <see href="https://example.com/docs/permissions">Permission generation</see>
/// for more information and examples.
```

### Parameters

Explain what each value represents and state its constraints. Keep the description short and use the same words a developer would use in conversation. State null contracts explicitly:

```csharp
/// <param name="key">The permission key. Cannot be <see langword="null" />.</param>
```

For optional null values, describe what null means:

```csharp
/// <param name="convention">
///     The naming convention, or <see langword="null" /> to use the default convention.
/// </param>
```

### Return values

Explain what the resolved value represents, not merely its type. Prefer one short sentence unless an important condition needs another line.

```csharp
/// <returns>
///     The matching descriptor, or <see langword="null" /> if no permission with the given key exists.
/// </returns>
```

### Exceptions

Use one `<exception>` element per exception type. State the condition in plain language. Separate multiple conditions for the same exception with `-or-`.

```csharp
/// <exception cref="ArgumentException">
///     <paramref name="key" /> is empty.
///     -or-
///     <paramref name="key" /> does not follow the
///     <c>{domain}.{resource}.{action}</c> convention.
/// </exception>
```

Document exceptions thrown by the member or observably by its direct callees. Do not document the entire transitive exception closure.

### Cross-references

- Use `<see cref="..." />` for types and members.
- Use `<see langword="..." />` for C# keywords.
- Use `<c>...</c>` for inline literals and format conventions.
- Use `<code>` for multi-line samples.

### Infrastructure APIs

For public-for-technical-reasons members, use this warning as the entire summary:

```csharp
/// <summary>
///     This is an internal API that supports the library infrastructure and is not
///     subject to the same compatibility standards as public APIs. It may be changed
///     or removed without notice in any release.
/// </summary>
```

## Required and private members

Document every non-private type and member. This includes `public`, `protected`, `internal`, `protected internal`, and `private protected` declarations. It applies to types, constructors, methods, properties, fields, constants, events, operators, delegates, and every enum member.

Private members are the only exception. Document a private member when:

- It implements specific or important functionality worth explaining.
- Its purpose or behavior is not obvious from its name and signature.
- A reader needs context to understand why it exists or how it should be changed.
- It implements a core algorithm or key invariant.
- It has important side effects, preconditions, ordering rules, recursion, caching, reentrancy, or shared-state changes.
- It exists because of a bug fix, workaround, security concern, compatibility rule, or performance decision that a future maintainer might undo. A plain `//` comment explaining why may be more suitable.
- It is a field with a non-obvious purpose, ownership rule, lifetime, unit, format, or allowed value.

Skip documentation when a private member is an obvious storage field, a trivial delegate-through, a small guard helper, or a self-explanatory expression-bodied member. Do not add documentation that merely restates the name or signature.

For a documented private member, apply the same XML structure and quality rules. Keep obvious parameter and return descriptions brief, but do not omit context that the reader needs.

```csharp
/// <summary>
///     Resolves attribute inheritance for the given type, walking base types depth-first.
/// </summary>
/// <remarks>
///     <para>
///         Declarations on the most-derived type win. The walk stops at the first type
///         outside the scanned assembly to avoid pulling in framework attributes.
///     </para>
///     <para>
///         This method mutates <see cref="_seenKeys" /> and must only be called while
///         holding <see cref="_scanLock" />.
///     </para>
/// </remarks>
private void ResolveInheritedPermissions(Type type)
{
}
```

## XML documentation review checklist

When reviewing documentation, report each issue as: **member → rule violated → corrected comment**.

Check each documented member in this order:

1. Every non-private member is documented, and private members that need explanation are documented.
2. The summary is one sentence, uses plain language and the correct verb form, ends with a period, and does not restate the name.
3. Canonical phrasing is used for constructors, Boolean properties, async methods, builders, events, enums, and types without making the wording needlessly formal.
4. Every parameter and type parameter is documented with its null or empty contract; every non-void return explains what the value represents.
5. Explicit exceptions are complete, their conditions are stated, and multiple conditions use `-or-`.
6. Keywords use `<see langword="..." />`, types use `<see cref="..." />`, and literals use `<c>`.
7. `<inheritdoc />` replaces copied base or interface documentation.
8. Remarks use `<para>` for distinct ideas, and implementation details do not leak into summaries.
9. Comments are short and easy to understand while still explaining the behavior and context that matter.
