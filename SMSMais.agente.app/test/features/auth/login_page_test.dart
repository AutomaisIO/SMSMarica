import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/presentation/login_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Abre a tela de login numa área útil de [largura]×[altura] (pontos).
Future<void> _abrirEm(
  WidgetTester tester, {
  required double largura,
  required double altura,
}) async {
  tester.view.physicalSize = Size(largura, altura);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      child: MaterialApp(theme: MaricaTheme.light(), home: const LoginPage()),
    ),
  );
  await tester.pumpAndSettle();
}

void _esperaEntrarVisivel(WidgetTester tester, double altura) {
  final botao = find.widgetWithText(ElevatedButton, 'Entrar');
  expect(botao, findsOneWidget);
  expect(tester.getRect(botao).bottom, lessThanOrEqualTo(altura));
}

void main() {
  // Tablet da frota: 1280×800 px, densidade 240 (×1,5) → 853×533 pontos,
  // menos 24 da barra de status.
  testWidgets('cabe no tablet da frota em paisagem', (tester) async {
    await _abrirEm(tester, largura: 853, altura: 509);

    expect(tester.takeException(), isNull);
    _esperaEntrarVisivel(tester, 509);
  });

  testWidgets('com o teclado aberto rola em vez de estourar', (tester) async {
    await _abrirEm(tester, largura: 853, altura: 260);

    expect(tester.takeException(), isNull);
  });

  testWidgets('em retrato também cabe', (tester) async {
    await _abrirEm(tester, largura: 509, altura: 829);

    expect(tester.takeException(), isNull);
    _esperaEntrarVisivel(tester, 829);
  });
}
