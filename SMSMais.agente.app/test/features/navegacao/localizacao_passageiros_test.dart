import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/application/localizacao_passageiros_controller.dart';
import 'package:agente/features/navegacao/data/localizacao_passageiros_repository.dart';
import 'package:agente/features/navegacao/domain/posicao_passageiro.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/navegacao/presentation/widgets/painel_passageiro.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Repositório de mentira: conta as consultas e devolve o que o teste mandar.
class _RepoFalso extends LocalizacaoPassageirosRepository {
  _RepoFalso() : super(Dio());

  int consultas = 0;
  List<PosicaoPassageiro> resposta = const [];
  Exception? erro;

  @override
  Future<List<PosicaoPassageiro>> listar(String token) async {
    consultas++;
    final e = erro;
    if (e != null) throw e;
    return resposta;
  }
}

final _agora = DateTime.utc(2026, 10, 23, 16, 30);

PosicaoPassageiro _posicao({
  String sessaoId = 's1',
  bool resgate = false,
  Duration idade = Duration.zero,
  double? precisaoM = 8,
  bool simulada = false,
}) => PosicaoPassageiro(
  sessaoId: sessaoId,
  resgate: resgate,
  latitude: -22.9194,
  longitude: -42.8186,
  precisaoM: precisaoM,
  capturadaEm: _agora.subtract(idade),
  simulada: simulada,
);

void main() {
  group('PosicaoPassageiro', () {
    test(
      'lê o que o servidor manda (ida e resgate são esperas diferentes)',
      () {
        final ida = PosicaoPassageiro.fromJson({
          'sessaoId': 's1',
          'trecho': 'Ida',
          'latitude': -22.9193983,
          'longitude': -42.8186,
          'precisaoM': 5,
          'capturadaEm': '2026-10-23T16:28:53.457Z',
          'simulada': false,
        });
        expect(ida.resgate, isFalse);
        expect(ida.chave, 's1:ida');
        expect(ida.precisaoM, 5.0);
        expect(ida.capturadaEm.isUtc, isTrue);

        final resgate = PosicaoPassageiro.fromJson({
          'sessaoId': 's1',
          'trecho': 'Resgate',
          'latitude': -22.9,
          'longitude': -43.1,
          'precisaoM': null,
          'capturadaEm': '2026-10-23T16:28:53Z',
          'simulada': true,
        });
        expect(resgate.chave, 's1:resgate');
        expect(resgate.precisaoM, isNull);
        expect(resgate.simulada, isTrue);
        expect(chavePosicao('s1', resgate: true), resgate.chave);
      },
    );

    test(
      'o texto do painel: idade, precisão e o aviso de posição simulada',
      () {
        expect(
          descreverPosicao(_posicao(), agora: _agora),
          'compartilhando · agora · ±8 m',
        );
        expect(
          descreverPosicao(
            _posicao(idade: const Duration(minutes: 3)),
            agora: _agora,
          ),
          'compartilhando · há 3 min · ±8 m',
        );
        expect(
          descreverPosicao(
            _posicao(precisaoM: null, simulada: true),
            agora: _agora,
          ),
          'compartilhando · agora · posição simulada',
        );
      },
    );
  });

  group('LocalizacaoPassageirosController', () {
    // Intervalo curto, com relógio de verdade (o app usa 15 s).
    const intervalo = Duration(milliseconds: 40);
    Future<void> esperar([int ciclos = 1]) => Future<void>.delayed(
      intervalo * ciclos + const Duration(milliseconds: 15),
    );

    test(
      'consulta ao abrir e a cada intervalo, guardando por parada de busca',
      () async {
        final repo = _RepoFalso()
          ..resposta = [_posicao(), _posicao(sessaoId: 's2', resgate: true)];
        final c = LocalizacaoPassageirosController(
          repo,
          () => 'token',
          intervalo: intervalo,
        );
        addTearDown(c.dispose);
        await Future<void>.delayed(Duration.zero);
        expect(repo.consultas, 1);
        expect(c.state.keys, containsAll(['s1:ida', 's2:resgate']));

        repo.resposta = const [];
        await esperar();
        expect(repo.consultas, greaterThanOrEqualTo(2));
        expect(c.state, isEmpty, reason: 'embarcou ou parou: o pino some');
      },
    );

    test('servidor sem a função (404): para de perguntar', () async {
      final repo = _RepoFalso()..erro = const LocalizacaoIndisponivel();
      final c = LocalizacaoPassageirosController(
        repo,
        () => 'token',
        intervalo: intervalo,
      );
      addTearDown(c.dispose);
      await esperar(4);
      expect(repo.consultas, 1);
      expect(c.state, isEmpty);
    });

    test('sem rede: fica como estava e tenta de novo', () async {
      final repo = _RepoFalso()..resposta = [_posicao()];
      final c = LocalizacaoPassageirosController(
        repo,
        () => 'token',
        intervalo: intervalo,
      );
      addTearDown(c.dispose);
      await Future<void>.delayed(Duration.zero);
      repo.erro = DioException(requestOptions: RequestOptions());
      await esperar();
      expect(repo.consultas, greaterThanOrEqualTo(2));
      expect(c.state.keys, ['s1:ida']);
    });

    test('sem tablet vinculado: não consulta', () async {
      final repo = _RepoFalso();
      final c = LocalizacaoPassageirosController(
        repo,
        () => null,
        intervalo: intervalo,
      );
      addTearDown(c.dispose);
      await esperar(2);
      expect(repo.consultas, 0);
    });

    test('fechou a tela: para de consultar', () async {
      final repo = _RepoFalso();
      LocalizacaoPassageirosController(
        repo,
        () => 'token',
        intervalo: intervalo,
      ).dispose();
      await esperar(3);
      expect(repo.consultas, lessThanOrEqualTo(1));
    });
  });

  group('PainelPassageiro', () {
    final parada = ParadaNavegacao(
      id: 'p1',
      ordem: 1,
      tipo: TipoParada.coleta,
      rotulo: 'Rita',
      endereco: 'Rua Simulada Três, 88 — Centro',
      latitude: -22.9194,
      longitude: -42.8186,
      horarioPrevisto: DateTime(2026, 10, 23, 14, 7),
      passageiros: const [
        PassageiroTranslado(
          id: 's1',
          nome: 'Rita',
          confirmacao: StatusConfirmacao.confirmado,
        ),
      ],
    );

    Future<void> abrir(WidgetTester tester, PosicaoPassageiro? posicao) async {
      tester.view.physicalSize = const Size(853, 509);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        MaterialApp(
          theme: MaricaTheme.light(),
          home: Scaffold(
            body: PainelPassageiro(
              parada: parada,
              onCheguei: () {},
              posicao: posicao,
            ),
          ),
        ),
      );
    }

    testWidgets('mostra que o paciente compartilha a localização', (
      tester,
    ) async {
      await abrir(
        tester,
        _posicao(idade: const Duration(minutes: 2)).copiaAgora(),
      );
      expect(find.textContaining('compartilhando'), findsOneWidget);
      expect(find.textContaining('±8 m'), findsOneWidget);
      expect(find.byIcon(Icons.my_location), findsOneWidget);
    });

    testWidgets('avisa quando o celular diz que a posição é simulada', (
      tester,
    ) async {
      await abrir(tester, _posicao(simulada: true).copiaAgora());
      expect(find.textContaining('posição simulada'), findsOneWidget);
    });

    testWidgets('sem compartilhar: o painel fica como sempre', (tester) async {
      await abrir(tester, null);
      expect(find.textContaining('compartilhando'), findsNothing);
      expect(find.byIcon(Icons.my_location), findsNothing);
      expect(find.text('Cheguei'), findsOneWidget);
    });
  });
}

extension on PosicaoPassageiro {
  /// A mesma posição, com a idade contada a partir de agora (o painel usa o
  /// relógio de verdade).
  PosicaoPassageiro copiaAgora() => PosicaoPassageiro(
    sessaoId: sessaoId,
    resgate: resgate,
    latitude: latitude,
    longitude: longitude,
    precisaoM: precisaoM,
    capturadaEm: DateTime.now().subtract(_agora.difference(capturadaEm)),
    simulada: simulada,
  );
}
