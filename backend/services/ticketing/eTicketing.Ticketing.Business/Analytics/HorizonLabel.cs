namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// The Bosnian phrase for a forecast horizon, in one place.
///
/// <para>The horizons the tab offers are months — one, three, six and twelve — and every horizon
/// that reaches a reader does so inside a sentence: a finding's title, the LLM prompt, a PDF
/// heading. Written from the raw number those come out as "narednih 365 dana", which is the kind of
/// detail that makes generated text read as generated. Everything that names the horizon in prose
/// goes through here so the four wordings cannot drift apart between the card, the summary and the
/// export.</para>
///
/// <para>The clients have their own copy of this table (the desktop's <c>horizonPhrase</c>) for the
/// labels they compose themselves. That duplication is deliberate and small: a round trip to learn
/// the word for "six months" would be worse.</para>
/// </summary>
public static class HorizonLabel
{
    /// <summary>
    /// The bare period in the genitive, ready to follow "narednih" or "prethodnih":
    /// "mjesec dana", "3 mjeseca", "6 mjeseci", "godinu dana".
    /// </summary>
    public static string Phrase(int days) => days switch
    {
        30 => "mjesec dana",
        90 => "3 mjeseca",
        180 => "6 mjeseci",
        365 => "godinu dana",
        // The validator only lets those four past the API, but callers inside the service name
        // horizons of their own — the PDF export, a test — and they must still read as Bosnian.
        _ => $"{days} dana"
    };

    /// <summary>"narednih 6 mjeseci" — the stretch the forecast covers.</summary>
    public static string Next(int days) => $"narednih {Phrase(days)}";

    /// <summary>"prethodnih 6 mjeseci" — the equally long stretch of actuals it is compared
    /// against.</summary>
    public static string Previous(int days) => $"prethodnih {Phrase(days)}";
}
