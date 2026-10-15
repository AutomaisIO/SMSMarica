import 'package:agente/app/app.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('App sobe na tela de login', (tester) async {
    FlutterSecureStorage.setMockInitialValues({});
    await tester.pumpWidget(const ProviderScope(child: AgenteApp()));
    await tester.pumpAndSettle();

    expect(find.text('SMS Maricá'), findsOneWidget);
    // Sem tablet vinculado nos testes: a entrada pede o vínculo primeiro.
    expect(find.text('Vincular veículo'), findsOneWidget);
  });
}
