using System.Diagnostics.CodeAnalysis;

namespace Banderas.Domain.Enums;

/// <summary>
/// The typed kind of value a <see cref="Banderas.Domain.ValueObjects.Variation"/> carries.
/// A flag's entire variation menu shares one kind — the SDK's typed accessors
/// (Phase 7) depend on this invariant.
/// </summary>
/// <remarks>
/// CA1720 (identifier contains type name) is suppressed: <c>Boolean</c>, <c>String</c>,
/// <c>Number</c>, and <c>Json</c> are the wire-contract names emitted as enum-as-string
/// across the API, AI prompts, and the eventual SDK. Renaming would break the contract.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Names are the wire contract for variation kinds."
)]
public enum VariationKind
{
    Boolean,
    String,
    Number,
    Json,
}
