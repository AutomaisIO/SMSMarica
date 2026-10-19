import 'dart:io';
import 'dart:typed_data';

import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/resgate/domain/resgate.dart';
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

  group('resgate', () {
    const joao = PassageiroTranslado(
      id: 's2',
      nome: 'João Simulado',
      confirmacao: StatusConfirmacao.confirmado,
    );
    ParadaNavegacao resgate(
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
      resgate: true,
      lote: 5,
    );
    final translado = TransladoNavegacao(
      id: 'r1',
      veiculo: 'SIM1R23',
      paradas: [
        resgate('resgate:u1:5', TipoParada.coleta, const [_rita, joao]),
        resgate('casa:s1:5', TipoParada.destino, const [_rita]),
        resgate('casa:s2:5', TipoParada.destino, const [joao]),
      ],
    );

    test('marcações do resgate vão marcadas como resgate', () {
      final e = _eventos(translado.paradas.first, {'s1': true, 's2': false});
      expect(e.map((x) => x.tipo), [
        TipoEventoViagem.embarque,
        TipoEventoViagem.ausencia,
      ]);
      expect(e.every((x) => x.resgate), isTrue);
      expect(e.first.toJson()['resgate'], isTrue);
    });

    test('a fila gravada antes da troca (volta) segue valendo', () {
      final antigo = _eventos(translado.paradas.first, {'s1': true}).first
          .toJson()
        ..remove('resgate')
        ..['volta'] = true;
      expect(EventoViagemPendente.fromJson(antigo).resgate, isTrue);
    });

    test('quem não estava no local não é deixado em casa; o acompanhante '
        'que faltou não desce com o paciente', () {
      final semRita = translado.paradasComAndamento(
        concluidas: const {'resgate:u1:5'},
        ausentes: {idAusenteNoResgate('s1', 5)},
      );
      expect(semRita[1].concluida, isTrue); // casa da Rita dispensada
      expect(semRita[2].concluida, isFalse);

      final semNeto = translado.paradasComAndamento(
        concluidas: const {'resgate:u1:5'},
        ausentes: {idAusenteNoResgate('s1:acompanhante:2', 5)},
      );
      expect(
        semNeto[1].passageiros.single.acompanhantes.map((a) => a.nome),
        ['Filha'],
      );

      // A marca de outro lote (resgate desmarcado e marcado de novo) não vale.
      final deOutroLote = translado.paradasComAndamento(
        concluidas: const {},
        ausentes: {idAusenteNoResgate('s1', 2)},
      );
      expect(deOutroLote[1].concluida, isFalse);
    });

    test('a rota da central marca o resgate e o lote', () {
      final rota = RotaDoTablet.fromJson({
        'rotaId': 'r1',
        'data': '2026-10-19',
        'status': 'EmAndamento',
        'paradas': [
          {
            'id': 'resgate:u1:5',
            'ordem': 3,
            'tipo': 'Coleta',
            'trecho': 'Resgate',
            'lote': 5,
            'passageiros': [
              {'sessaoId': 's1', 'nome': 'Rita Simulada'},
            ],
          },
        ],
      });

      final p = rota.translado.paradas.single;
      expect(p.resgate, isTrue);
      expect(p.lote, 5);
      expect(rota.andamento.ausentes, isEmpty);
    });
  });

  group('lugares ao escolher', () {
    ResgateDisponivel paciente(
      String id, {
      bool cadeirante = false,
      bool maca = false,
      bool isolamento = false,
      int acompanhantes = 0,
    }) => ResgateDisponivel(
      sessaoId: id,
      pacienteNome: id,
      unidadeNome: 'Clínica',
      disponibilidade: DisponibilidadeResgate.noLocal,
      cadeirante: cadeirante,
      maca: maca,
      isolamento: isolamento,
      acompanhantes: acompanhantes,
    );
    // Van fictícia: 4 bancos de passageiro, 1 de acompanhante, 1 de cadeirante.
    const van = CarroAgora(passageiro: 4, acompanhante: 1, cadeirante: 1);

    test('cada paciente escolhido leva o lugar dele e o dos acompanhantes', () {
      final ana = paciente('ana', cadeirante: true, acompanhantes: 2);
      final sobra = van.levando([ana])!;
      expect(van.livres, 6);
      expect(sobra.livres, 3); // Ana na cadeira + 2 acompanhantes
      expect(sobra.cadeirante, 0);
    });

    test('cadeirante só com assento de cadeirante sobrando', () {
      final ana = paciente('ana', cadeirante: true);
      final beto = paciente('beto', cadeirante: true);
      expect(van.levando([ana]), isNotNull);
      expect(van.levando([ana, beto]), isNull);
      expect(
        van.motivoNaoCabe(beto, [ana]),
        'Não sobra assento de cadeirante neste carro.',
      );
    });

    test('isolamento não divide o carro e maca só em ambulância', () {
      final iso = paciente('iso', isolamento: true, acompanhantes: 1);
      final outro = paciente('outro');
      expect(van.levando([iso]), isNotNull);
      expect(van.levando([iso, outro]), isNull);
      expect(van.levando([paciente('maca', maca: true)]), isNull);
      const ambulancia = CarroAgora(passageiro: 2, ambulancia: true);
      expect(ambulancia.levando([paciente('maca', maca: true)]), isNotNull);
    });

    test('mais gente do que lugar não cabe', () {
      final grupo = [
        paciente('a', acompanhantes: 1),
        paciente('b', acompanhantes: 1),
        paciente('c'),
      ];
      expect(van.levando(grupo)!.livres, 1); // 5 pessoas em 6 lugares
      final d = paciente('d', acompanhantes: 1);
      expect(van.levando([...grupo, d]), isNull);
      expect(
        van.motivoNaoCabe(d, grupo),
        'Não cabe: 2 pessoas para 1 lugar livre.',
      );
    });
  });

  group('lista de resgate', () {
    test('lê a lista da central e diz a disponibilidade como o painel', () {
      final r = ResgatesDaRota.fromJson({
        'lugaresLivres': 2,
        'lugaresCadeiranteLivres': 1,
        'disponiveis': [
          {
            'sessaoId': 's1',
            'pacienteNome': 'Rita Simulada',
            'unidadeNome': 'CLÍNICA SIMULADA',
            'acompanhantes': 2,
            'mobilidade': 'CadeiranteVeiculoAdaptado',
            'disponibilidade': 'EmAtendimento',
            'disponivelAs': '10:40:00',
            'disponivelEmMinutos': 12,
            'cabe': false,
            'motivoNaoCabe': 'Não cabe agora: 3 pessoas para 2 lugares livres.',
          },
          {
            'sessaoId': 's2',
            'pacienteNome': 'João Simulado',
            'unidadeNome': 'CLÍNICA SIMULADA',
            'disponibilidade': 'NoLocal',
            'aguardandoMinutos': 65,
            'avisadoPor': 'Paciente',
          },
        ],
        'marcados': [
          {
            'sessaoId': 's3',
            'pacienteNome': 'Ana Simulada',
            'unidadeNome': 'x',
          },
        ],
      });

      expect(r.lugaresLivres, 2);
      expect(r.marcados.single.sessaoId, 's3');
      final rita = r.disponiveis.first;
      expect(rita.cadeirante, isTrue);
      expect(rita.cabe, isFalse);
      expect(rita.textoDisponibilidade, 'Disponível em 12 min (às 10:40)');
      expect(r.disponiveis.last.quemAvisou, 'avisado pelo paciente');
      expect(
        r.disponiveis.last.textoDisponibilidade,
        'Aguardando há 1 h 05',
      );
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
