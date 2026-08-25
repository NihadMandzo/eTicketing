using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// The design tokens for the printed ticket, kept in one place so <see cref="TicketDocument"/>
/// reads as layout rather than as a wall of hex codes.
///
/// The source design is an HTML mock-up authored in CSS pixels on a 210x297mm sheet, so every
/// measurement here is expressed in those same pixels and converted once by <see cref="Px"/>.
/// Keeping the original numbers (rather than pre-converted points) means a value can be compared
/// against the mock-up directly when either side changes.
/// </summary>
public static class TicketTheme
{
    // Brand greens, dark to light. The three of them appear together in the panel-1 accent bar and
    // individually as the left rule of each "how to get in" card.
    public const string GreenDark = "#1D5B3A";
    public const string GreenMid = "#6FAE4A";
    public const string GreenLight = "#8DC63F";

    public const string TextPrimary = "#111827";
    public const string TextBody = "#374151";
    public const string TextMuted = "#6B7280";

    public const string Border = "#E5E7EB";
    public const string BorderStrong = "#D1D5DB";
    public const string Surface = "#F5F5F5";
    public const string StubSurface = "#F9FAFB";
    public const string White = "#FFFFFF";

    /// <summary>
    /// Each weight is addressed by its own family name rather than by asking for one family and a
    /// weight modifier.
    ///
    /// That is deliberate. Google Fonts' static Manrope instances declare their legacy family name
    /// as "Manrope ExtraLight" (an artefact of the variable font's default instance), and IBM Plex
    /// Mono's SemiBold declares "IBM Plex Mono SemiBold" — so <c>FontFamily("Manrope").Bold()</c>
    /// matches nothing and QuestPDF silently substitutes its bundled Lato. Registering each file
    /// under an explicit name and selecting it directly removes the weight-matching step entirely,
    /// and a missing face then fails loudly instead of quietly changing how the ticket looks.
    ///
    /// Consequence: never combine these with .Bold()/.SemiBold()/.ExtraBold(), which would ask the
    /// renderer to synthesise a heavier weight on top of an already-bold face.
    /// </summary>
    public const string SansRegular = "Manrope-Regular";
    public const string SansSemiBold = "Manrope-SemiBold";
    public const string SansBold = "Manrope-Bold";
    public const string SansExtraBold = "Manrope-ExtraBold";
    public const string MonoRegular = "IBMPlexMono-Regular";
    public const string MonoSemiBold = "IBMPlexMono-SemiBold";

    /// <summary>CSS pixels to PDF points: CSS is 96dpi, PDF is 72dpi.</summary>
    public static float Px(float pixels) => pixels * 0.75f;

    private static bool _registered;
    private static readonly Lock RegistrationLock = new();

    /// <summary>
    /// Loads Manrope and IBM Plex Mono into QuestPDF from this assembly's embedded resources, each
    /// under the file's own base name (see the family-name constants above for why).
    ///
    /// They have to be embedded rather than installed: the runtime image ships only the Liberation
    /// family, and the design depends on Manrope's weight range for its typographic hierarchy. Both
    /// faces are SIL Open Font License 1.1, which permits redistribution — the licences sit beside
    /// the .ttf files, and the files themselves are unmodified.
    ///
    /// The subsets pulled from Google Fonts are latin + latin-ext specifically, because the default
    /// latin subset omits every Bosnian diacritic (č ć ž š đ) and would render them as blanks.
    /// </summary>
    public static void EnsureFontsRegistered()
    {
        if (_registered) return;

        lock (RegistrationLock)
        {
            if (_registered) return;

            var assembly = Assembly.GetExecutingAssembly();
            foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                // "…Documents.Fonts.Manrope-SemiBold.ttf" -> "Manrope-SemiBold"
                var parts = resource.Split('.');
                var familyName = parts[^2];

                using var stream = assembly.GetManifestResourceStream(resource)!;
                FontManager.RegisterFontWithCustomName(familyName, stream);
            }

            _registered = true;
        }
    }

    private static byte[]? _logo;

    /// <summary>The eKarta mark shown in the header roundel, trimmed of its transparent margin so
    /// it centres optically inside the circle.</summary>
    public static byte[] Logo => _logo ??= ReadResource("logo.png");

    private static byte[] ReadResource(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Small-caps label sitting above almost every value on the sheet.</summary>
    public static TextStyle Label(float sizePx, string color = TextMuted, float letterSpacing = 0.14f, bool strong = false) =>
        TextStyle.Default.FontFamily(strong ? SansBold : SansSemiBold).FontSize(Px(sizePx))
            .LetterSpacing(letterSpacing).FontColor(color);

    public static TextStyle Value(float sizePx, string color = TextPrimary) =>
        TextStyle.Default.FontFamily(SansBold).FontSize(Px(sizePx)).FontColor(color);

    /// <summary>The heaviest weight — the event name and the two section headings.</summary>
    public static TextStyle Title(float sizePx, string color = TextPrimary) =>
        TextStyle.Default.FontFamily(SansExtraBold).FontSize(Px(sizePx)).FontColor(color);

    public static TextStyle Body(float sizePx, float lineHeight, string color = TextBody) =>
        TextStyle.Default.FontFamily(SansRegular).FontSize(Px(sizePx)).LineHeight(lineHeight).FontColor(color);

    public static TextStyle Emphasis(float sizePx, string color) =>
        TextStyle.Default.FontFamily(SansSemiBold).FontSize(Px(sizePx)).FontColor(color);

    public static TextStyle MonoStyle(float sizePx, string color = TextPrimary, bool strong = true) =>
        TextStyle.Default.FontFamily(strong ? MonoSemiBold : MonoRegular).FontSize(Px(sizePx)).FontColor(color);
}
