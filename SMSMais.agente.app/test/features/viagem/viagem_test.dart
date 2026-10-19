import 'dart:io';
import 'dart:typed_data';

import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/rota/domain/rota_do_tablet.dart';
import 'package:agente/features/viagem/application/viagem_controller.dart';
import 'package:agente/features/viagem/data/fila_viagem.dart';
import 'package:agente/features/viagem/data/viagem_repository.dart';
import 'package:agente/features/viagem/domain/viagem.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

/// Rita (pacientes fictícios) com dois acompanhantes vindos da central.
const _rita = PassageiroTranslado(
  id: 's1',
  nome: 'Rita Simulada',
  confirmacao: StatusConfirmacao.confirmado,
  acompanhantes: [
    Acompanhante(
      nome: 'Filha',
      confirmacao: StatusConfirmacao.confirmado,
      id: 'a1',
      ordem: 1,
    ),
    Acompanhante(
      nome: 'Neto',
      confirmacao: StatusConfirmacao.confirmado,
      id: 'a2',
      ordem: 2,
    ),
  ],
);

ParadaNavegacao _parada(TipoParada tipo, List<PassageiroTranslado> quem) =>
    ParadaNavegacao(
      id: tipo == TipoParada.coleta ? 'coleta:s1' : 'destino:u1',
      ordem: 1,
      tipo: tipo,
      rotulo: 'x',
      endereco: 'x',
      latitude: -22.9,
      longitude: -42.8,
      horarioPrevisto: null,
      passageiros: quem,
    );

List<EventoViagemPendente> _eventos(
  ParadaNavegacao parada,
  Map<String, bool> marcas,
) {
  var n = 0;
  return eventosDaParada(
    rotaId: 'r1',
    parada: parada,
    marcas: marcas,
    novoId: () => 'e${n++}',
    agora: DateTime.utc(2026, 10, 16, 9),
    motoristaId: 'm1',
  );
}

/// Central de mentira: some ([semRede]) ou responde [status] (os de
/// [proximos] primeiro) e guarda o tipo de cada marcação que chegou.
class _Central implements HttpClientAdapter {
  bool semRede = false;
  int status = 201;
  final List<int> proximos = [];
  final List<String> recebidos = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (semRede) throw const SocketException('off');
    recebidos.add((options.data as Map<String, dynamic>)['tipo'] as String);
    final s = proximos.isEmpty ? status : proximos.removeAt(0);
    return ResponseBody.fromString(
      '{}',
      s,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _Dispositivo extends DispositivoRepository {
  _Dispositivo() : super(Dio());

  @override
  Future<VeiculoVinculado> eu(String token) async =>
      const VeiculoVinculado(veiculoId: 'v1', placa: 'SIM1R23');
}

ProviderContainer _container(_Central central, FilaViagem fila) {
  final c = ProviderContainer(
    overrides: [
      dispositivoRepositoryProvider.overrideWithValue(_Dispositivo()),
      filaViagemProvider.overrideWithValue(fila),
      viagemRepositoryProvider.overrideWithValue(
        ViagemRepository(
          Dio(BaseOptions(baseUrl: 'http://central.test'))
            ..httpClientAdapter = central,
        ),
      ),
    ],
  );
  addTearDown(c.dispose);
  // Como no app: o vínculo do tablet carrega na abertura, antes da viagem.
  c.read(dispositivoProvider);
  return c;
}

EventoViagemPendente _marca(String id, TipoEventoViagem tipo) =>
    EventoViagemPendente(
      idLocal: id,
      rotaId: 'r1',
      tipo: tipo,
      ocorridoEm: DateTime.utc(2026, 10, 16, 9),
    );

void main() {
  group('marcações da parada', () {
    test('coleta: embarque e ausência de cada pessoa, com a posição', () {
      final e = _eventos(_parada(TipoParada.coleta, const [_rita]), {
        's1': true,
        's1:acompanhante': false,
        's1:acompanhante:2': true,
      });

      expect(e.map((x) => x.tipo), [
        TipoEventoViagem.embarque,
        TipoEventoViagem.ausencia,
        TipoEventoViagem.embarque,
      ]);
      expect(e.map((x) => x.pessoa), [
        PessoaViagem.paciente,
        PessoaViagem.acompanhante,
        PessoaViagem.acompanhante,
      ]);
      expect(e.map((x) => x.acompanhanteOrdem), [null, 1, 2]);
      expect(e[2].acompanhanteId, 'a2');
      expect(
        e.every((x) => x.sessaoId == 's1' && x.motoristaId == 'm1'),
        isTrue,
      );
      expect(e.map((x) => x.idLocal).toSet(), hasLength(3));
    });

    test('destino: sem quem faltou, o outro acompanhante segue o 2º', () {
      final desce = _rita.semAusentes({'s1:acompanhante'});
      expect(desce.idAcompanhante(), 's1:acompanhante:2');

      final e = _eventos(_parada(TipoParada.destino, [desce]), {
        's1': true,
        's1:acompanhante:2': true,
      });

      expect(e.map((x) => x.tipo), everyElement(TipoEventoViagem.desembarque));
      expect(e.map((x) => x.acompanhanteOrdem), [null, 2]);
    });

    test('destino: quem não desceu não vira marcação', () {
      final e = _eventos(_parada(TipoParada.destino, const [_rita]), {
        's1': false,
      });
      expect(e, isEmpty);
    });
  });

  group('volta', () {
    const joao = PassageiroTranslado(
      id: 's2',
      nome: 'João Simulado',
      confirmacao: StatusConfirmacao.confirmado,
    );
    ParadaNavegacao volta(
      String id,
      TipoParada tipo,
      List<PassageiroTranslado> quem,
    ) => ParadaNavegacao(
      id: id,
      ordem: 1,
      tipo: tipo,
      rotulo: id,
      endereco: 'x',
      latitude: -22.9,
      longitude: -42.8,
      horarioPrevisto: null,
      passageiros: quem,
      volta: true,
    );
    final translado = TransladoNavegacao(
      id: 'r1',
      veiculo: 'SIM1R23',
      paradas: [
        volta('retorno:u1', TipoParada.coleta, const [_rita, joao]),
        volta('casa:s1', TipoParada.destino, const [_rita]),
        volta('casa:s2', TipoParada.destino, const [joao]),
      ],
    );

    test('marcações da volta vão marcadas como volta', () {
      final e = _eventos(translado.paradas.first, {'s1': true, 's2': false});
      expect(e.map((x) => x.tipo), [
        TipoEventoViagem.embarque,
        TipoEventoViagem.ausencia,
      ]);
      expect(e.every((x) => x.volta), isTrue);
      expect(e.first.toJson()['volta'], isTrue);
    });

    test('quem faltou na ida não é buscado; quem não estava na unidade '
        'não é deixado em casa', () {
      final semJoaoNaIda = translado.paradasComAndamento(
        concluidas: const {},
        ausentes: const {'s2'},
      );
      expect(semJoaoNaIda.first.passageiros.map((p) => p.id), ['s1']);
      expect(semJoaoNaIda.last.concluida, isTrue); // casa do João dispensada

      final semRitaNaVolta = translado.paradasComAndamento(
        concluidas: const {'retorno:u1'},
        ausentes: {idAusenteNaVolta('s1')},
      );
      expect(semRitaNaVolta[1].concluida, isTrue); // casa da Rita dispensada
      expect(semRitaNaVolta[2].concluida, isFalse);
    });

    test('a rota da central marca a volta e quem não estava na unidade', () {
      final rota = RotaDoTablet.fromJson({
        'rotaId': 'r1',
        'data': '2026-10-16',
        'status': 'EmAndamento',
        'paradas': [
          {
            'id': 'retorno:u1',
            'ordem': 3,
            'tipo': 'Coleta',
            'trecho': 'Volta',
            'passageiros': [
              {
                'sessaoId': 's1',
                'nome': 'Rita Simulada',
                'situacao': 'Ausente',
              },
            ],
          },
        ],
      });

      expect(rota.translado.paradas.single.volta, isTrue);
      expect(rota.andamento.ausentes, {idAusenteNaVolta('s1')});
    });
  });

  group('andamento', () {
    test('o do tablet e o da central se somam (só da mesma rota)', () {
      const tablet = AndamentoViagem(
        rotaId: 'r1',
        concluidas: {'coleta:s1'},
        iniciada: true,
      );
      const central = AndamentoViagem(rotaId: 'r1', ausentes: {'s2'});

      final junto = tablet.unir(central);
      expect(junto.concluidas, {'coleta:s1'});
      expect(junto.ausentes, {'s2'});
      expect(junto.iniciada, isTrue);
      expect(tablet.unir(const AndamentoViagem(rotaId: 'r2')), same(tablet));

      final volta = AndamentoViagem.fromJson(junto.toJson());
      expect(volta.ausentes, {'s2'});
      expect(volta.iniciada, isTrue);
    });

    test('a rota da central traz quem faltou e o que já foi resolvido', () {
      final rota = RotaDoTablet.fromJson({
        'rotaId': 'r1',
        'data': '2026-10-16',
        'status': 'EmAndamento',
        'paradas': [
          {
            'id': 'coleta:s1',
            'ordem': 1,
            'tipo': 'Coleta',
            'concluida': true,
            'passageiros': [
              {
                'sessaoId': 's1',
                'nome': 'Rita Simulada',
                'situacao': 'Embarcou',
                'acompanhantes': [
                  {'nome': 'Filha', 'id': 'a1', 'situacao': 'Ausente'},
                  {'nome': 'Neto', 'id': 'a2', 'situacao': 'Embarcou'},
                ],
              },
            ],
          },
        ],
      });

      expect(rota.andamento.iniciada, isTrue);
      expect(rota.andamento.concluidas, {'coleta:s1'});
      expect(rota.andamento.ausentes, {'s1:acompanhante'});
      final acompanhantes =
          rota.translado.paradas.single.passageiros.single.acompanhantes;
      expect(acompanhantes.map((a) => a.id), ['a1', 'a2']);
      expect(acompanhantes.map((a) => a.ordem), [1, 2]);
    });
  });

  group('fila até a central', () {
    setUp(
      () =>
          FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'}),
    );

    Future<void> carregar() =>
        Future<void>.delayed(const Duration(milliseconds: 50));

    test('na ordem em que aconteceu, e a fila esvazia', () async {
      final central = _Central();
      final fila = FilaViagemMemoria();
      final c = _container(central, fila);
      await carregar();

      await c.read(viagemProvider.notifier).registrar([
        _marca('1', TipoEventoViagem.inicio),
        _marca('2', TipoEventoViagem.embarque),
        _marca('3', TipoEventoViagem.conclusao),
      ]);

      expect(central.recebidos, ['Inicio', 'Embarque', 'Conclusao']);
      expect(await fila.pendentes(), isEmpty);
      expect(c.read(viagemProvider), 0);
    });

    test('sem rede fica; recusa sai sem travar as outras', () async {
      final central = _Central()..semRede = true;
      final fila = FilaViagemMemoria();
      final c = _container(central, fila);
      await carregar();
      final viagem = c.read(viagemProvider.notifier);

      await viagem.registrar([
        _marca('1', TipoEventoViagem.inicio),
        _marca('2', TipoEventoViagem.embarque),
      ]);
      expect(c.read(viagemProvider), 2);

      central
        ..semRede = false
        ..proximos.add(422);
      await viagem.enviarPendentes();

      expect(central.recebidos, ['Inicio', 'Embarque']);
      expect(await fila.pendentes(), isEmpty);
    });

    test('token revogado (401): para e guarda tudo', () async {
      final central = _Central()..status = 401;
      final fila = FilaViagemMemoria();
      final c = _container(central, fila);
      await carregar();

      await c.read(viagemProvider.notifier).registrar([
        _marca('1', TipoEventoViagem.inicio),
        _marca('2', TipoEventoViagem.embarque),
      ]);

      expect(central.recebidos, ['Inicio']);
      expect((await fila.pendentes()).map((e) => e.idLocal), ['1', '2']);
    });
  });
}
