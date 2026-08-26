using eTicketing.Contracts.Persistence;
using FluentAssertions;

namespace eTicketing.Contracts.Tests.Persistence;

/// <summary>
/// The enum identifier is a C# symbol and cannot hold a space or a diacritic, so anything that
/// reaches a person — a printed ticket, a PDF, an e-mail — has to go through
/// <see cref="CityExtensions.ToDisplayName"/> rather than ToString().
/// </summary>
public class CityExtensionsTests
{
    [Theory]
    [InlineData(City.BanjaLuka, "Banja Luka")]
    [InlineData(City.Bihac, "Bihać")]
    [InlineData(City.Brcko, "Brčko")]
    public void ToDisplayName_ForCitiesTheIdentifierCannotSpell_ReturnsTheWrittenName(City city, string expected)
    {
        city.ToDisplayName().Should().Be(expected);
    }

    [Theory]
    [InlineData(City.Sarajevo, "Sarajevo")]
    [InlineData(City.Mostar, "Mostar")]
    [InlineData(City.Tuzla, "Tuzla")]
    [InlineData(City.Zenica, "Zenica")]
    [InlineData(City.Trebinje, "Trebinje")]
    public void ToDisplayName_ForCitiesTheIdentifierSpellsCorrectly_ReturnsTheIdentifier(City city, string expected)
    {
        city.ToDisplayName().Should().Be(expected);
    }

    [Fact]
    public void ToDisplayName_ForEveryDeclaredCity_ReturnsSomethingReadable()
    {
        // A city appended later without a mapping still has to come out as text, and must never
        // come out as a run-together identifier that a customer would notice on a ticket.
        foreach (var city in Enum.GetValues<City>())
        {
            var display = city.ToDisplayName();

            display.Should().NotBeNullOrWhiteSpace();
            display.Should().NotContain("_");
        }
    }
}
