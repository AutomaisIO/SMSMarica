import 'dart:io';
import 'dart:typed_data';

import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/rota/data/rota_repository.dart';
import 'package:agente/features/rota/domain/rota_do_tablet.dart';
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

/// Resposta como o servidor manda (pacientes fictícios).
Map<String, dynamic> _rota({
  String id = 'r1',
  String status = 'Planejada',
  String motoristaId = 'm1',
}) => {
  'rotaId': id,
  'data': '2026-10-16',
  'status': status,
  'motoristaId': motoristaId,
  'motoristaNome': 'Motorista Simulado',
  'paradas': [
    {
      'id': 'coleta:s1',
      'ordem': 1,
      'tipo': 'Coleta',
      'rotulo': 'Ana Simulada',
      'endereco': 'Rua das Acácias, 7 — Itaipuaçu, Maricá',
      'latitude': -22.95,
      'longitude': -42.95,
      'horarioPrevisto': '07:30:00',
      'observacoes': 'Portão azul',
      'passageiros': [
        {
          'sessaoId': 's1',
          'nome': 'Ana Simulada',
          'telefone': '21911111111',
          'confirmacao': 'Confirmado',
          'assento': {'fileira': 1, 'numero': 2, 'tipo': 'Passageiro'},
          'condicao': {
            'mobilidade': 'CadeiranteVeiculoAdaptado',
            'dificuldadeVeiculoAlto': false,
            'isolamento': false,
            'usaOxigenio': true,
            'necessitaAjuda': true,
            'ajudaDescricao': 'Precisa de ajuda para subir',
          },
          'tipoTratamento': 'Hemodiálise',
          'tempoMedioMinutos': 240,
          'unidadeAtendimento': 'CLÍNICA SIMULADA',
          'acompanhantes': [
            {
              'nome': 'Carlos Simulado',
              'parentesco': 'Filho',
              'confirmacao': 'Aguardando',
              'assento': {'fileira': 1, 'numero': 4, 'tipo': 'Acompanhante'},
            },
            {'nome': 'Bia Simulada', 'confirmacao': 'Aguardando'},
          ],
        },
      ],
    },
    {
      'id': 'destino:u1',
      'ordem': 2,
      'tipo': 'Destino',
      'rotulo': 'CLÍNICA SIMULADA',
      'endereco': null,
      'latitude': null,
      'longitude': null,
      'horarioPrevisto': null,
      'observacoes': 'Entrada pela rampa lateral',
      'passageiros': <dynamic>[],
    },
  ],
};

/// Servidor de mentira no lugar da rede: responde [rotas] ou some.
class _Servidor implements HttpClientAdapter {
  bool semRede = false;
  List<dynamic> rotas = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (semRede) throw const SocketException('off');
    return ResponseBody.fromString(
      '[${rotas.map(_json).join(',')}]',
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  static String _json(Object? o) => switch (o) {
    null => 'null',
    final String s => '"${s.replaceAll('"', r'\"')}"',
    final num n => '$n',
    final bool b => '$b',
    final List<dynamic> l => '[${l.map(_json).join(',')}]',
    final Map<String, dynamic> m =>
      '{${m.entries.map((e) => '"${e.key}":${_json(e.value)}').join(',')}}',
    _ => throw ArgumentError(o),
  };

  @override
  void close({bool force = false}) {}
}

void main() {
  group('conversão do que o servidor manda', () {
    final rota = RotaDoTablet.fromJson(_rota(), veiculo: 'TST2F34');
    final coleta = rota.translado.paradas.first;
    final destino = rota.translado.paradas.last;
    final ana = coleta.passageiros.single;

    test('coleta com ponto, horário e observação para o motorista', () {
      expect(rota.translado.veiculo, 'TST2F34');
      expect(coleta.tipo, TipoParada.coleta);
      expect(coleta.temPonto, isTrue);
      expect(coleta.horarioPrevisto, DateTime(2026, 10, 16, 7, 30));
      expect(coleta.observacoes, 'Portão azul');
      expect(ana.confirmacao, StatusConfirmacao.confirmado);
      expect(ana.assento, 'Fileira 1, banco 2');
      expect(
        ana.observacao,
        'Cadeirante — só em veículo adaptado · Usa oxigênio · '
        'Ajuda: Precisa de ajuda para subir · Hemodiálise (cerca de 4 h)',
      );
    });

    test('dois acompanhantes, com parentesco e assento', () {
      expect(ana.acompanhantes, hasLength(2));
      expect(ana.acompanhantes[0].nome, 'Carlos Simulado (filho(a))');
      expect(ana.acompanhantes[0].assento, 'Fileira 1, banco 4');
      expect(ana.acompanhantes[1].nome, 'Bia Simulada');
      expect(ana.acompanhantes[1].assento, isNull);
      expect(ana.idAcompanhante(1), 's1:acompanhante:2');
    });

    test('destino sem ponto no mapa e sem horário', () {
      expect(destino.tipo, TipoParada.destino);
      expect(destino.temPonto, isFalse);
      expect(destino.horarioPrevisto, isNull);
      expect(destino.endereco, 'Endereço não informado');
      expect(destino.observacoes, 'Entrada pela rampa lateral');
    });
  });

  group('qual rota o motorista faz agora', () {
    RotaDoTablet r(String id, String status, String motorista) =>
        RotaDoTablet.fromJson(
          _rota(id: id, status: status, motoristaId: motorista),
        );

    test('a em andamento primeiro, a dele antes da do colega', () {
      final rotas = [
        r('a', 'Planejada', 'm1'),
        r('b', 'EmAndamento', 'm1'),
        r('c', 'EmAndamento', 'm2'),
      ];
      expect(escolherRota(rotas, 'm1')!.rotaId, 'b');
      expect(escolherRota(rotas, 'm2')!.rotaId, 'c');
    });

    test('sem rota dele, a do veículo; concluída só se não sobrar outra', () {
      expect(escolherRota([r('a', 'Planejada', 'm2')], 'm1')!.rotaId, 'a');
      expect(
        escolherRota([
          r('a', 'Concluida', 'm1'),
          r('b', 'Planejada', 'm2'),
        ], 'm1')!.rotaId,
        'b',
      );
      expect(escolherRota([], 'm1'), isNull);
    });
  });

  group('sem rede', () {
    setUp(() => FlutterSecureStorage.setMockInitialValues({}));

    test('usa a rota guardada do mesmo dia; a de ontem não vale', () async {
      final servidor = _Servidor()..rotas = [_rota()];
      var agora = DateTime.utc(2026, 10, 16, 12);
      final repo = RotaRepository(
        Dio(BaseOptions(baseUrl: 'http://central.test'))
          ..httpClientAdapter = servidor,
        const FlutterSecureStorage(),
        relogio: () => agora,
      );

      expect(await repo.rotasDoDia('t'), hasLength(1));
      await Future<void>.delayed(Duration.zero); // guarda em segundo plano

      servidor.semRede = true;
      expect((await repo.rotasDoDia('t')).single.rotaId, 'r1');

      agora = DateTime.utc(2026, 10, 17, 12);
      await expectLater(repo.rotasDoDia('t'), throwsA(isA<DioException>()));
    });
  });
}
