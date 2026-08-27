import 'package:desktop/screens/widgets/quantity_stepper_field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// The quantity box sits between two stepper icons, so "centred" is not a
/// matter of taste — the field either shares a centre line with the `−` and
/// `+` glyphs beside it or it visibly does not.
///
/// This is deliberately not a test that re-measures font ascent/descent or
/// tries to model `InputDecorator`'s internal baseline math — an earlier
/// attempt at that fought the decorator instead of using it (see the widget's
/// doc comment) and still came out wrong. What actually matters, and what is
/// actually checked here, is geometric: does the field's own box end up
/// centred against the boxHeight-tall buttons next to it.
void main() {
  Future<void> pumpField(
    WidgetTester tester,
    TextEditingController controller,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: QuantityStepperField(
              controller: controller,
              onChanged: (_) {},
            ),
          ),
        ),
      ),
    );
  }

  group('QuantityStepperField', () {
    testWidgets('the field sits on the same centre line as the stepper icons', (
      tester,
    ) async {
      final controller = TextEditingController(text: '10');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      final fieldCentre = tester.getRect(find.byType(TextField)).center.dy;
      final iconCentres = find
          .byType(Icon)
          .evaluate()
          .map((e) => tester.getRect(find.byWidget(e.widget)).center.dy)
          .toList();

      expect(iconCentres, hasLength(2));
      for (final iconCentre in iconCentres) {
        expect(fieldCentre, closeTo(iconCentre, 1.0));
      }
    });

    testWidgets('the field does not force itself to the full boxHeight', (
      tester,
    ) async {
      // The whole point of the fix: the field is left to size itself
      // naturally and let the Row centre it, rather than being forced into a
      // box exactly boxHeight tall (which is what fighting the decorator's
      // own centring produced before).
      final controller = TextEditingController(text: '10');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      final fieldHeight = tester.getRect(find.byType(TextField)).height;
      expect(fieldHeight, lessThan(QuantityStepperField.boxHeight));
    });

    testWidgets('renders the stepper icons and a field, no overflow', (
      tester,
    ) async {
      final controller = TextEditingController(text: '10');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      expect(find.byType(TextField), findsOneWidget);
      expect(find.byType(Icon), findsNWidgets(2));
      expect(tester.takeException(), isNull);
    });

    testWidgets('the whole control is boxHeight tall, border included', (
      tester,
    ) async {
      final controller = TextEditingController(text: '10');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      // The bordered container is exactly the button height plus the
      // border's own stroke on top and bottom.
      final controlHeight = tester
          .getRect(find.byType(QuantityStepperField))
          .height;
      expect(
        controlHeight,
        greaterThanOrEqualTo(QuantityStepperField.boxHeight),
      );
      expect(controlHeight, lessThan(QuantityStepperField.boxHeight + 4));
    });

    testWidgets('the − and + callbacks fire, and null disables the button', (
      tester,
    ) async {
      final controller = TextEditingController(text: '5');
      addTearDown(controller.dispose);
      var decremented = false;
      var incremented = false;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: QuantityStepperField(
                controller: controller,
                onChanged: (_) {},
                onDecrement: () => decremented = true,
                onIncrement: () => incremented = true,
              ),
            ),
          ),
        ),
      );

      await tester.tap(find.byType(InkWell).first);
      await tester.tap(find.byType(InkWell).last);

      expect(decremented, isTrue);
      expect(incremented, isTrue);
    });

    testWidgets('typing calls onChanged with the raw text', (tester) async {
      final controller = TextEditingController();
      addTearDown(controller.dispose);
      String? seen;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: QuantityStepperField(
                controller: controller,
                onChanged: (raw) => seen = raw,
              ),
            ),
          ),
        ),
      );

      await tester.enterText(find.byType(TextField), '250');
      expect(seen, '250');
    });

    testWidgets('a long number does not overflow the field', (tester) async {
      final controller = TextEditingController(text: '999999');
      addTearDown(controller.dispose);
      await pumpField(tester, controller);

      expect(tester.takeException(), isNull);
    });
  });

  group('QuantityInputFormatter', () {
    TextEditingValue format(String before, String after) =>
        const QuantityInputFormatter().formatEditUpdate(
          TextEditingValue(
            text: before,
            selection: TextSelection.collapsed(offset: before.length),
          ),
          TextEditingValue(
            text: after,
            selection: TextSelection.collapsed(offset: after.length),
          ),
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
      expect(
        result.selection.baseOffset,
        lessThanOrEqualTo(result.text.length),
      );
      expect(result.selection.baseOffset, greaterThanOrEqualTo(0));
    });
  });
}
