import 'package:desktop/core/formatting.dart';
import 'package:desktop/models/enums/ticketing_mode.dart';
import 'package:desktop/screens/widgets/ticket_sheet_preview.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show FontLoader, rootBundle;
import 'package:flutter_test/flutter_test.dart';

/// The export screen's sheet preview is a scale model of `PrintSheetDocument`:
/// every measurement in it is a design pixel multiplied by however much the
/// sheet had to shrink to fit the window. That makes overflow the failure mode
/// to guard — a slot that fits at 700px can burst at 320px — so these tests
/// pump the sheet across the range the desktop window can produce and assert
/// nothing overflows and the right ticket details land on the paper.
void main() {
  // Without this the sheet is measured in the test framework's fallback font,
  // whose metrics are nothing like Manrope's — every wrap point would move and
  // the overflow assertions below would be testing a layout no user ever sees.
  setUpAll(() async {
    TestWidgetsFlutterBinding.ensureInitialized();

    const faces = {
      'Manrope': [
        'assets/fonts/Manrope-Regular.ttf',
        'assets/fonts/Manrope-SemiBold.ttf',
        'assets/fonts/Manrope-Bold.ttf',
        'assets/fonts/Manrope-ExtraBold.ttf',
      ],
      'IBMPlexMono': [
        'assets/fonts/IBMPlexMono-Regular.ttf',
        'assets/fonts/IBMPlexMono-SemiBold.ttf',
      ],
    };

    for (final face in faces.entries) {
      final loader = FontLoader(face.key);
      for (final asset in face.value) {
        loader.addFont(rootBundle.load(asset));
      }
      await loader.load();
    }
  });

  final singleOccurrence = PrintSheetContext(
    productName: 'FK Sarajevo — FK Željezničar',
    city: 'Sarajevo',
    productDate: null,
    ticketingMode: TicketingMode.singleOccurrence,
    validDate: null,
    issuedAt: _issued,
  );

  // The sheet is sized by its A4 aspect ratio, so the test surface has to be
  // that shape too — the default 800x600 would silently squash the slots and
  // every overflow assertion below would be measuring the wrong sheet.
  Future<void> pumpSheet(
    WidgetTester tester,
    Widget sheet,
    double width,
  ) async {
    final size = Size(width, width * 297 / 210);
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SizedBox.fromSize(size: size, child: sheet),
        ),
      ),
    );
  }

  List<PrintPreviewTicket> tickets(int count, {int from = 37}) => [
    for (var i = 0; i < count; i++)
      PrintPreviewTicket(
        sector: 'Sjever',
        type: 'Redovna',
        price: 25,
        serial: from + i,
      ),
  ];

  group('TicketSheetPreview', () {
    testWidgets('paints the eKarta mark in every ticket\'s brand roundel', (tester) async {
      // The mark is an asset, and an asset that is missing from the bundle or
      // fails to decode leaves the roundel silently blank rather than throwing
      // anything the other tests here would notice — hence loading it for real
      // (runAsync, so the image pipeline's real I/O actually runs) instead of
      // just asserting an Image widget exists in the tree.
      await pumpSheet(
        tester,
        TicketSheetPreview(
          sheet: singleOccurrence,
          tickets: tickets(3),
          ticketsPerSheet: 3,
        ),
        620,
      );

      final images = tester.widgetList<Image>(find.byType(Image)).toList();
      expect(images, hasLength(3), reason: 'one mark per ticket on the sheet');

      final provider = images.first.image as AssetImage;
      expect(provider.assetName, 'assets/ticket-mark.png');

      Object? loadError;
      await tester.runAsync(() async {
        try {
          await precacheImage(provider, tester.element(find.byType(Image).first));
        } catch (e) {
          loadError = e;
        }
      });

      expect(loadError, isNull, reason: 'the mark must actually decode from the bundle');
    });

    // A full sheet is the densest case; the narrow widths are what the stacked
    // layout below the 1080px breakpoint actually hands it.
    for (final width in [760.0, 620.0, 460.0, 320.0]) {
      testWidgets(
        'renders a full sheet without overflow at ${width.toInt()}px',
        (tester) async {
          await pumpSheet(
            tester,
            TicketSheetPreview(
              sheet: singleOccurrence,
              tickets: tickets(3),
              ticketsPerSheet: 3,
            ),
            width,
          );

          expect(tester.takeException(), isNull);
        },
      );
    }

    testWidgets('renders the longest realistic product name without overflow', (
      tester,
    ) async {
      // Products validate up to 200 characters, and the document clamps the
      // title to two lines rather than letting it push the ticket off the page.
      final long = PrintSheetContext(
        productName: 'Međunarodni ' * 16,
        city: 'Banja Luka',
        productDate: DateTime(2026, 9, 5, 20, 30),
        ticketingMode: TicketingMode.singleOccurrence,
        validDate: null,
        issuedAt: _issued,
      );

      await pumpSheet(
        tester,
        TicketSheetPreview(
          sheet: long,
          tickets: tickets(3),
          ticketsPerSheet: 3,
        ),
        620,
      );

      expect(tester.takeException(), isNull);
    });

    testWidgets(
      'shows one ticket per slot, numbered as the renderer will number them',
      (tester) async {
        await pumpSheet(
          tester,
          TicketSheetPreview(
            sheet: singleOccurrence,
            tickets: tickets(3),
            ticketsPerSheet: 3,
          ),
          700,
        );

        expect(find.text('#000037'), findsOneWidget);
        expect(find.text('#000038'), findsOneWidget);
        expect(find.text('#000039'), findsOneWidget);
        expect(find.text('SERIJSKI BROJ'), findsNWidgets(3));
        expect(find.text('DOGAĐAJ'), findsNWidgets(3));
      },
    );

    testWidgets(
      'leaves unused slots as blank paper, inviting action exactly once',
      (tester) async {
        await pumpSheet(
          tester,
          TicketSheetPreview(
            sheet: singleOccurrence,
            tickets: const [],
            ticketsPerSheet: 3,
          ),
          700,
        );

        expect(find.text('Odaberite sektor i broj karata.'), findsOneWidget);
        expect(find.text('SERIJSKI BROJ'), findsNothing);
      },
    );

    testWidgets('a partly filled sheet prints only the tickets it has', (
      tester,
    ) async {
      await pumpSheet(
        tester,
        TicketSheetPreview(
          sheet: singleOccurrence,
          tickets: tickets(2),
          ticketsPerSheet: 3,
        ),
        700,
      );

      expect(find.text('SERIJSKI BROJ'), findsNWidgets(2));
      // The hint belongs to an empty sheet, not to a half-full one.
      expect(find.text('Odaberite sektor i broj karata.'), findsNothing);
      expect(tester.takeException(), isNull);
    });

    testWidgets(
      'a single-occurrence ticket is valid for the showing, one entry',
      (tester) async {
        final sheet = PrintSheetContext(
          productName: 'Koncert',
          city: 'Mostar',
          productDate: _showing,
          ticketingMode: TicketingMode.singleOccurrence,
          validDate: null,
          issuedAt: _issued,
        );

        await pumpSheet(
          tester,
          TicketSheetPreview(
            sheet: sheet,
            tickets: tickets(1),
            ticketsPerSheet: 3,
          ),
          700,
        );

        expect(find.text('05.09.2026. 20:30'), findsOneWidget);
        expect(find.text('JEDAN ULAZ'), findsOneWidget);
      },
    );

    testWidgets('a daily-entry ticket is valid for the chosen day', (
      tester,
    ) async {
      final sheet = PrintSheetContext(
        productName: 'Muzej — dnevna ulaznica',
        city: 'Sarajevo',
        productDate: null,
        ticketingMode: TicketingMode.dailyEntry,
        validDate: DateTime(2026, 9, 14),
        issuedAt: _issued,
      );

      await pumpSheet(
        tester,
        TicketSheetPreview(
          sheet: sheet,
          tickets: tickets(1),
          ticketsPerSheet: 3,
        ),
        700,
      );

      expect(find.text('14.09.2026.'), findsOneWidget);
      expect(find.text('DNEVNI ULAZ'), findsOneWidget);
    });

    testWidgets('a sector without ticket types prints its name alone', (
      tester,
    ) async {
      await pumpSheet(
        tester,
        TicketSheetPreview(
          sheet: singleOccurrence,
          tickets: const [
            PrintPreviewTicket(
              sector: 'Sjever',
              type: null,
              price: 25,
              serial: 1,
            ),
          ],
          ticketsPerSheet: 3,
        ),
        700,
      );

      expect(find.text('Sjever'), findsOneWidget);
      expect(find.textContaining('Sjever ·'), findsNothing);
    });

    testWidgets(
      'a sector with ticket types prints both, as the renderer joins them',
      (tester) async {
        await pumpSheet(
          tester,
          TicketSheetPreview(
            sheet: singleOccurrence,
            tickets: const [
              PrintPreviewTicket(
                sector: 'Sjever',
                type: 'Studentska',
                price: 15,
                serial: 1,
              ),
            ],
            ticketsPerSheet: 3,
          ),
          700,
        );

        expect(find.text('Sjever · Studentska'), findsOneWidget);
        expect(find.text('15,00 KM'), findsOneWidget);
      },
    );
  });

  group('formatting', () {
    test('formatCount groups thousands with a dot', () {
      expect(formatCount(0), '0');
      expect(formatCount(999), '999');
      expect(formatCount(1000), '1.000');
      expect(formatCount(93600), '93.600');
      expect(formatCount(1234567), '1.234.567');
    });

    test('formatMoney uses comma decimals and grouped thousands', () {
      expect(formatMoney(0), '0,00 KM');
      expect(formatMoney(25), '25,00 KM');
      expect(formatMoney(7.5), '7,50 KM');
      expect(formatMoney(93600), '93.600,00 KM');
    });

    test('formatStubNumber pads to the six digits printed on the stub', () {
      expect(formatStubNumber(1), '#000001');
      expect(formatStubNumber(482), '#000482');
      expect(formatStubNumber(1234567), '#1234567');
    });

    test('formatDate and formatTime follow Bosnian notation', () {
      expect(formatDate(DateTime(2026, 8, 5)), '05.08.2026.');
      expect(formatTime(DateTime(2026, 8, 5, 9, 4)), '09:04');
    });
  });
}

final _issued = DateTime(2026, 8, 26);
final _showing = DateTime(2026, 9, 5, 20, 30);
