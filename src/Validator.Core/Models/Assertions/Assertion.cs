using System.Text.Json.Serialization;

namespace Validator.Core.Models.Assertions;

/// <summary>
/// Base class for all assertions.
/// Uses polymorphic JSON serialization with a kind discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BuildAssertion), "build")]
[JsonDerivedType(typeof(HttpAssertion), "http")]
[JsonDerivedType(typeof(DatabaseAssertion), "database")]
public abstract class Assertion
{
}
