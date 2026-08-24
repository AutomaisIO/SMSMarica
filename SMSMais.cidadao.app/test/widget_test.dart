import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sms_mais_cidadao/app/app.dart';

void main() {
  testWidgets('app inicia e mostra a tela de login', (tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: AppCidadaoMarica(),
      ),
    );

    // Aguarda a localizaçao e o router resolverem.
    await tester.pumpAndSettle();

    expect(find.text('Acessar minha conta'), findsOneWidget);
    expect(find.text('MARICÁ'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });
}
