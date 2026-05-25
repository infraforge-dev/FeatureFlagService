namespace Banderas.Application.DTOs;

/// <summary>
/// Wire-form of a single variation in a flag's menu.
/// Distinct from the domain <c>Variation</c> value object: this DTO carries operator
/// intent (raw, possibly mis-cased <see cref="Kind"/> string) before validation.
/// </summary>
/// <param name="Key">Human-facing label. Matches <c>^[a-z0-9\-_]+$</c>, ≤50 chars.</param>
/// <param name="Kind">
/// One of <c>"Boolean"</c>, <c>"String"</c>, <c>"Number"</c>, <c>"Json"</c>.
/// Case-insensitive on input; normalized at the mapping layer.
/// </param>
/// <param name="Value">
/// JSON-encoded string matching the declared kind. For <c>String</c> kind, the value
/// includes the JSON quotes — e.g. <c>"\"red-button\""</c>, not raw <c>red-button</c>.
/// </param>
public sealed record VariationRequest(string Key, string Kind, string Value);
