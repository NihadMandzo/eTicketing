import 'package:desktop/screens/widgets/quantity_stepper_field.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';

/// The quantity box sits between two stepper icons, so "centred" is not a
/// matter of taste here — the digits either share an axis with the `−` and `+`
/// glyphs or they visibly do not. These tests measure it rather than eyeball
/// it: the caret rect gives the text line's exact position inside the field,
/// which is the only way to tell a centred line box from centred digits.
void main() {
  Future<void> pumpField(WidgetTester tester, TextEditingController controller) async {
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: Center(
          child: QuantityStepperField(controller: controller, onChanged: (_) {}),
        ),
      ),
    ));
  }

  /// Where the text line actually sits, in global coordinates. The caret spans
  /// exactly one line, so its rect is the line's vertical extent.
  Rect textLineRect(WidgetTester tester) {
    final editable = tester.renderObject<RenderEditable>(
      find.descendant(of: find.byType(EditableText), matching: find.byType(CustomPaint)).first,
    );
    final caret = editable.getLocalRectForCaret(const TextPosition(offset: 0));
    final origin = tester.getTopLeft(find.byType(EditableText));

    return caret.shift(origin);
  }

  group('QuantityStepperField', () {
    testWidgets('the digits share an optical centre line with the stepper icons', (tester) async {
      final controller = TextEditingController(text: '10');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      final line = textLineRect(tester);
      final box = tester.getRect(find.byType(QuantityStepperField));

      // The digits' visual mass runs from the top of the line down to the
      // baseline; the descender space below it is empty for 0-9. Centring that
      // span — not the whole line box — is what puts them level with the icons.
      final baseline = line.bottom - _descentOf(line.height);
      final digitsCentre = (line.top + baseline) / 2;

      expect(digitsCentre, closeTo(box.center.dy, 1.0));
    });

    testWidgets('the icons themselves are centred, so that axis is the right one', (tester) async {
      final controller = TextEditingController();
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      final box = tester.getRect(find.byType(QuantityStepperField));
      for (final icon in find.byType(Icon).evaluate()) {
        expect(tester.getRect(find.byWidget(icon.widget)).center.dy, closeTo(box.center.dy, 0.5));
      }
    });

    testWidgets('an empty field puts its hint on the same line', (tester) async {
      final controller = TextEditingController();
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      final hint = tester.getRect(find.text('0'));
      final line = textLineRect(tester);

      expect(hint.center.dy, closeTo(line.center.dy, 1.0));
    });

    testWidgets('a long number stays on one line and inside the box', (tester) async {
      final controller = TextEditingController(text: '12500');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      final line = textLineRect(tester);
      final box = tester.getRect(find.byType(QuantityStepperField));

      expect(line.height, lessThan(QuantityStepperField.boxHeight));
      expect(line.top, greaterThanOrEqualTo(box.top));
      expect(line.bottom, lessThanOrEqualTo(box.bottom));
      expect(tester.takeException(), isNull);
    });

    testWidgets('the box keeps its stated height whatever it holds', (tester) async {
      final controller = TextEditingController(text: '999999');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      expect(
        tester.getRect(find.byType(QuantityStepperField)).height,
        QuantityStepperField.boxHeight,
      );
    });
  });

  group('QuantityInputFormatter', () {
    TextEditingValue format(String before, String after) => const QuantityInputFormatter().formatEditUpdate(
          TextEditingValue(text: before, selection: TextSelection.collapsed(offset: before.length)),
          TextEditingValue(text: after, selection: TextSelection.collapsed(offset: after.length)),
        );

    test('keeps digits', () {
      expect(format('1', '12').text, '12');
      expect(format('', '1250').text, '1250');
    });

    test('drops anything that is not a digit', () {
      expect(format('', 'abc').text, '');
      expect(format('12', '12a3').text, '123');
      expect(format('', '-5').text, '5');
      expect(format('1', '1.5').text, '15');
      expect(format('', '1\n0').text, '10');
    });

    test('never leaves a leading zero standing', () {
      expect(format('0', '00').text, '0');
      expect(format('00', '007').text, '7');
      expect(format('', '0').text, '0');
    });

    test('keeps the caret inside the text it rewrote', () {
      final result = format('12', '12a3');
      expect(result.selection.baseOffset, lessThanOrEqualTo(result.text.length));
      expect(result.selection.baseOffset, greaterThanOrEqualTo(0));
    });
  });
}

/// Roughly the descender share of a line box. Only used to locate the baseline
/// for the assertion above, so it does not need to be exact — the tolerance on
/// the expectation is a whole pixel.
double _descentOf(double lineHeight) => lineHeight * 0.21;
