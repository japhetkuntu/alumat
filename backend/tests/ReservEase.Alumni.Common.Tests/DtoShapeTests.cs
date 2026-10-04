using System.Reflection;
using System.Text.Json;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Common.Tests;

/// <summary>Broad guard for the response DTOs the frontends depend on: every DTO is constructible, null-safe by default and survives JSON.</summary>
public class DtoShapeTests
{
    public static IEnumerable<object[]> Dtos() => typeof(ApiResponse<>).Assembly.GetTypes()
        .Where(t => t.Namespace == typeof(ApiResponse<>).Namespace && t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition
                    && t.GetConstructor(Type.EmptyTypes) is not null && t.Name.EndsWith("Dto"))
        .OrderBy(t => t.Name).Select(t => new object[] { t });

    [Theory, MemberData(nameof(Dtos))]
    public void Non_nullable_strings_default_to_empty_not_null(Type type)
    {
        var instance = Activator.CreateInstance(type)!;
        var nullability = new NullabilityInfoContext();
        foreach (var p in type.GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanRead && p.GetIndexParameters().Length == 0))
        {
            if (nullability.Create(p).ReadState == NullabilityState.NotNull)
                Assert.True(p.GetValue(instance) is not null, $"{type.Name}.{p.Name} is declared non-nullable but defaults to null");
        }
    }

    [Theory, MemberData(nameof(Dtos))]
    public void Collections_default_to_empty_not_null(Type type)
    {
        var instance = Activator.CreateInstance(type)!;
        var nullability = new NullabilityInfoContext();
        foreach (var p in type.GetProperties().Where(p => p.CanRead && p.PropertyType != typeof(string)
                     && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)))
        {
            if (nullability.Create(p).ReadState == NullabilityState.NotNull)
                Assert.True(p.GetValue(instance) is not null, $"{type.Name}.{p.Name} is declared non-nullable but defaults to null");
        }
    }

    [Theory, MemberData(nameof(Dtos))]
    public void A_default_instance_survives_a_json_round_trip(Type type)
    {
        var instance = Activator.CreateInstance(type)!;
        var json = JsonSerializer.Serialize(instance, type);
        var copy = JsonSerializer.Deserialize(json, type);
        Assert.NotNull(copy);
        Assert.Equal(json, JsonSerializer.Serialize(copy, type));
    }

    [Fact]
    public void There_are_dtos_to_check()
        => Assert.True(Dtos().Count() > 30);
}
