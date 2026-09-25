using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class TextSearchTests
{
    private sealed record Person(string First, string Last, string? Company);

    private static readonly Person[] People =
    [
        new("Esi", "Owusu", "Acme Tech"),
        new("Kofi", "Boateng", null),
        new("Ama", "Mensah", "Northwind"),
    ];

    private static string[] Find(string? search) =>
        People.AsQueryable()
            .WhereMatches(p => TextSearch.Matches(search, p.First, p.Last, p.First + " " + p.Last, p.Last + " " + p.First, p.Company))
            .Select(p => p.First)
            .ToArray();

    [Theory]
    [InlineData("esi owusu")]
    [InlineData("owusu esi")]
    [InlineData("  ESI   Owusu ")]
    [InlineData("owusu")]
    [InlineData("esi acme")]
    public void FindsEsiByAnyWordsInAnyOrder(string search) => Assert.Equal(["Esi"], Find(search));

    [Fact]
    public void EmptySearchMatchesEverything() => Assert.Equal(3, Find("   ").Length);

    [Fact]
    public void EveryWordMustMatch() => Assert.Empty(Find("esi mensah"));

    [Fact]
    public void MissingFieldsNeverMatch() => Assert.Equal(["Kofi"], Find("kofi"));

    [Fact]
    public void MarkerAlsoWorksInMemory() => Assert.True(TextSearch.Matches("engineer acme", "Senior Engineer", "Acme"));
}
