import 'package:agente/app/app.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('App sobe na tela de login', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: AgenteApp()));
    await tester.pumpAndSettle();

    expect(find.text('SMS Maricá'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });
}
