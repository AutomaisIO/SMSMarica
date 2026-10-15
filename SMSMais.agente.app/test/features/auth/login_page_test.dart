import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/application/entrada_motorista_service.dart';
import 'package:agente/features/auth/data/fila_conferencias.dart';
import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/features/auth/facial/data/servico_facial.dart';
import 'package:agente/features/auth/presentation/login_page.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

class _DispositivoFalso extends DispositivoRepository {
  _DispositivoFalso() : super(Dio());

  @override
  Future<VeiculoVinculado> eu(String token) async =>
      const VeiculoVinculado(veiculoId: 'v1', placa: 'ABC1D23');
}

/// A tela não deve tocar no reconhecimento só para aparecer.
class _FacialNaoUsado implements ReconhecedorFacial {
  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

class _EntradaFalsa extends EntradaMotoristaService {
  _EntradaFalsa(this.escala)
    : super(
        repo: MotoristaRepository(Dio()),
        facial: _FacialNaoUsado(),
        escala: EscalaLocal(const FlutterSecureStorage()),
        fila: FilaConferenciasMemoria(),
      );

  final EscalaDoDia escala;

  @override
  Future<EscalaDoDia> carregarEscala(String token) async => escala;
}

const _escalaComRosto = EscalaDoDia(
  motoristas: [
    MotoristaEscalado(
      motoristaId: 'm1',
      nome: 'Maria Motorista',
      rotaId: 'r1',
      temReferencia: true,
      fotoHash: 'h1',
    ),
    MotoristaEscalado(
      motoristaId: 'm2',
      nome: 'João Novo',
      rotaId: 'r1',
      temReferencia: false,
    ),
  ],
  atualizada: true,
);

/// Abre a tela de entrada numa área útil de [largura]×[altura] (pontos).
Future<void> _abrirEm(
  WidgetTester tester, {
  required double largura,
  required double altura,
  bool vinculado = true,
}) async {
  FlutterSecureStorage.setMockInitialValues({
    if (vinculado) 'dispositivo_token': 't',
  });
  tester.view.physicalSize = Size(largura, altura);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        dispositivoRepositoryProvider.overrideWithValue(_DispositivoFalso()),
        entradaMotoristaServiceProvider.overrideWithValue(
          _EntradaFalsa(_escalaComRosto),
        ),
      ],
      child: MaterialApp(theme: MaricaTheme.light(), home: const LoginPage()),
    ),
  );
  await tester.pumpAndSettle();
}

void _esperaVisivel(WidgetTester tester, Finder alvo, double altura) {
  expect(alvo, findsOneWidget);
  expect(tester.getRect(alvo).bottom, lessThanOrEqualTo(altura));
}

final _entrarPeloRosto = find.text('Entrar pelo rosto');

void main() {
  // Tablet da frota: 1280×800 px, densidade 240 (×1,5) → 853×533 pontos,
  // menos 24 da barra de status.
  testWidgets('cabe no tablet da frota em paisagem', (tester) async {
    await _abrirEm(tester, largura: 853, altura: 509);

    expect(tester.takeException(), isNull);
    _esperaVisivel(tester, _entrarPeloRosto, 509);
    expect(find.textContaining('Primeiro acesso'), findsWidgets);
    expect(find.text('Tenho um código da central'), findsOneWidget);
    expect(find.text('Maria Motorista'), findsOneWidget);
    expect(find.text('João Novo (primeiro acesso)'), findsOneWidget);
  });

  testWidgets('com o teclado aberto rola em vez de estourar', (tester) async {
    await _abrirEm(tester, largura: 853, altura: 260);

    expect(tester.takeException(), isNull);
  });

  testWidgets('em retrato também cabe', (tester) async {
    await _abrirEm(tester, largura: 509, altura: 829);

    expect(tester.takeException(), isNull);
    _esperaVisivel(tester, _entrarPeloRosto, 829);
  });

  testWidgets('tablet sem vínculo manda vincular antes', (tester) async {
    await _abrirEm(tester, largura: 853, altura: 509, vinculado: false);

    expect(find.text('Vincular veículo'), findsOneWidget);
    expect(_entrarPeloRosto, findsNothing);
  });

  testWidgets('código da central: campo de 6 números', (tester) async {
    await _abrirEm(tester, largura: 853, altura: 509);

    await tester.tap(find.text('Tenho um código da central'));
    await tester.pumpAndSettle();

    expect(find.text('Código da central'), findsOneWidget);
    await tester.enterText(find.byType(TextField), '12a34567');
    expect(find.text('123456'), findsOneWidget);
  });
}
