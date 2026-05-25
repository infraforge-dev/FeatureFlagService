namespace Banderas.Application.DTOs;

/// <summary>
/// Wire-form of a single variation as returned by the API.
/// Distinct type from <see cref="VariationRequest"/> by design — same fields today,
/// but the request carries operator intent (pre-validation) while the response
/// carries a validated, canonicalized projection of the domain VO.
/// </summary>
/// <param name="Key">Human-facing label as stored.</param>
/// <param name="Kind">
/// Canonical enum name: <c>"Boolean"</c>, <c>"String"</c>, <c>"Number"</c>, or <c>"Json"</c>.
/// </param>
/// <param name="Value">JSON-encoded value string matching the declared kind.</param>
public sealed record VariationResponse(string Key, string Kind, string Value);
