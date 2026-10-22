import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/navegacao/presentation/widgets/confirmacao_parada_sheet.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Coleta com dois pacientes; só a Maria tem acompanhante → 3 pessoas.
final _parada = ParadaNavegacao(
  id: 'p1',
  ordem: 1,
  tipo: TipoParada.coleta,
  rotulo: 'Maria',
  endereco: 'Rua das Flores, 123 — Centro',
  latitude: -22.91,
  longitude: -42.82,
  horarioPrevisto: DateTime(2026, 10, 14, 7, 30),
  passageiros: const [
    PassageiroTranslado(
      id: 'pac-1',
      nome: 'Maria',
      confirmacao: StatusConfirmacao.confirmado,
      assento: 'Banco 1',
      acompanhantes: [
        Acompanhante(
          nome: 'José',
          confirmacao: StatusConfirmacao.pendente,
          assento: 'Banco 2',
        ),
      ],
    ),
    PassageiroTranslado(
      id: 'pac-2',
      nome: 'Ana',
      confirmacao: StatusConfirmacao.confirmado,
      assento: 'Banco 3',
    ),
  ],
);

Future<void> _abrir(WidgetTester tester, {ParadaNavegacao? parada}) async {
  // Tablet da frota em paisagem (853×509 pontos úteis).
  tester.view.physicalSize = const Size(853, 509);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: MaricaTheme.light(),
      home: Scaffold(body: ConfirmacaoParadaSheet(parada: parada ?? _parada)),
    ),
  );
}

bool _concluirHabilitado(WidgetTester tester) => tester
    .widget<ButtonStyleButton>(
      find.ancestor(
        of: find.text('Embarque concluído'),
        matching: find.byWidgetPredicate((w) => w is ButtonStyleButton),
      ),
    )
    .enabled;

Future<void> _marcar(WidgetTester tester, String rotulo, int indice) async {
  final botao = find.text(rotulo).at(indice);
  await tester.ensureVisible(botao);
  await tester.tap(botao);
  await tester.pump();
}

void main() {
  testWidgets('uma linha de conferência por pessoa', (tester) async {
    await _abrir(tester);

    expect(tester.takeException(), isNull);
    expect(find.text('Paciente'), findsNWidgets(2));
    expect(find.text('Acompanhante'), findsOneWidget);
    expect(find.text('Embarcou'), findsNWidgets(3));
    expect(find.text('0 de 3 conferidos'), findsOneWidget);
  });

  testWidgets('no desembarque só existe Desembarcou', (tester) async {
    final destino = ParadaNavegacao(
      id: 'destino:u1',
      ordem: 2,
      tipo: TipoParada.destino,
      rotulo: 'Clínica',
      endereco: 'Rua Simulada, 100',
      latitude: -22.89,
      longitude: -43.12,
      horarioPrevisto: null,
      passageiros: _parada.passageiros,
    );
    await _abrir(tester, parada: destino);

    expect(find.text('Desembarcou'), findsNWidgets(3));
    // Quem faltou no embarque não embarcou: não há "Ausente" para marcar.
    expect(find.text('Ausente'), findsNothing);
    for (var i = 0; i < 3; i++) {
      await _marcar(tester, 'Desembarcou', i);
    }
    expect(find.text('3 de 3 conferidos'), findsOneWidget);
  });

  testWidgets('só conclui com todas as pessoas conferidas', (tester) async {
    await _abrir(tester);
    expect(_concluirHabilitado(tester), isFalse);

    // Maria e Ana embarcam; o acompanhante da Maria ainda não foi conferido.
    await _marcar(tester, 'Embarcou', 0);
    await _marcar(tester, 'Embarcou', 2);
    expect(find.text('2 de 3 conferidos'), findsOneWidget);
    expect(_concluirHabilitado(tester), isFalse);

    // Acompanhante marcado à parte (ausente) libera a conclusão.
    await _marcar(tester, 'Ausente', 1);
    expect(find.text('3 de 3 conferidos'), findsOneWidget);
    expect(_concluirHabilitado(tester), isTrue);
  });

  testWidgets('devolve a marca de cada pessoa ao concluir', (tester) async {
    tester.view.physicalSize = const Size(853, 509);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    Map<String, bool>? marcas;
    await tester.pumpWidget(
      MaterialApp(
        theme: MaricaTheme.light(),
        home: Builder(
          builder: (context) => Scaffold(
            body: TextButton(
              onPressed: () async =>
                  marcas = await showModalBottomSheet<Map<String, bool>>(
                    context: context,
                    isScrollControlled: true,
                    builder: (_) => ConfirmacaoParadaSheet(parada: _parada),
                  ),
              child: const Text('abrir'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('abrir'));
    await tester.pumpAndSettle();

    await _marcar(tester, 'Embarcou', 0);
    await _marcar(tester, 'Ausente', 1);
    await _marcar(tester, 'Embarcou', 2);
    final concluir = find.text('Embarque concluído');
    await tester.ensureVisible(concluir);
    await tester.tap(concluir);
    await tester.pumpAndSettle();

    expect(marcas, {'pac-1': true, 'pac-1:acompanhante': false, 'pac-2': true});
  });
}
