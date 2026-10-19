import 'dart:io';
import 'dart:typed_data';

import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/features/ocorrencia/application/ocorrencias_controller.dart';
import 'package:agente/features/ocorrencia/data/fila_ocorrencias.dart';
import 'package:agente/features/ocorrencia/domain/ocorrencia.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

/// Central de mentira no lugar da rede: some ([semRede]) ou responde com
/// [status] (201 = recebida; [proximos] na frente, um por pedido) e guarda o
/// idLocal de cada aviso que chegou até ela.
class _CentralFalsa implements HttpClientAdapter {
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
    recebidos.add((options.data as Map<String, dynamic>)['idLocal'] as String);
    final s = proximos.isEmpty ? status : proximos.removeAt(0);
    return ResponseBody.fromString(
      s == 201 ? '{"recebidaEm":"2026-10-15T12:00:00Z"}' : '{}',
      s,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

/// Repositório de verdade falando com a [_CentralFalsa].
class _RepoFalso extends DispositivoRepository {
  _RepoFalso(this.central) : super(_dio(central));

  final _CentralFalsa central;

  static Dio _dio(_CentralFalsa central) =>
      Dio(BaseOptions(baseUrl: 'http://central.test'))
        ..httpClientAdapter = central;

  @override
  Future<VeiculoVinculado> eu(String token) async =>
      const VeiculoVinculado(veiculoId: 'v1', placa: 'ABC1D23');
}

/// GPS sem plugin (teste): não liga, fica sem posição.
class _GpsParado extends GpsAoVivo {
  @override
  Future<void> iniciar() async {}
}

ProviderContainer _container(_RepoFalso repo) {
  final c = ProviderContainer(
    overrides: [
      dispositivoRepositoryProvider.overrideWithValue(repo),
      gpsAoVivoProvider.overrideWith((_) => _GpsParado()),
    ],
  );
  addTearDown(c.dispose);
  return c;
}

/// Deixa o DispositivoController ler o token salvo (assíncrono).
Future<void> _esperarCarregar() =>
    Future<void>.delayed(const Duration(milliseconds: 50));

void main() {
  group('modelo', () {
    test('tipos usam os nomes do enum do servidor', () {
      expect(TipoOcorrencia.values.map((t) => t.apiValor), [
        'Acidente',
        'PaneVeiculo',
        'PacienteMalEstar',
        'Outro',
      ]);
      expect(TipoOcorrencia.outro.exigeDescricao, isTrue);
      expect(TipoOcorrencia.acidente.exigeDescricao, isFalse);
    });

    test('idLocal é UUID v4', () {
      final id = novoIdLocal();
      const hex = '[0-9a-f]';
      final v4 = RegExp('^$hex{8}-$hex{4}-4$hex{3}-[89ab]$hex{3}-$hex{12}\$');
      expect(v4.hasMatch(id), isTrue);
      expect(novoIdLocal(), isNot(id));
    });

    test('pendente vai e volta do JSON da fila', () {
      final o = OcorrenciaPendente(
        idLocal: 'x',
        tipo: TipoOcorrencia.pacienteMalEstar,
        descricao: 'desmaiou',
        latitude: -22.9,
        longitude: -42.8,
        ocorridaEm: DateTime.utc(2026, 10, 15, 9),
      );
      final volta = OcorrenciaPendente.fromJson(o.toJson());

      expect(volta.tipo, TipoOcorrencia.pacienteMalEstar);
      expect(volta.descricao, 'desmaiou');
      expect(volta.ocorridaEm, DateTime.utc(2026, 10, 15, 9));
    });
  });

  group('envio do REPORT', () {
    setUp(() => FlutterSecureStorage.setMockInitialValues({}));

    test('sem vínculo: guarda no tablet e avisa', () async {
      final c = _container(_RepoFalso(_CentralFalsa()));
      await _esperarCarregar();

      final r = await c
          .read(ocorrenciasProvider.notifier)
          .reportar(TipoOcorrencia.acidente);

      expect(r.enviado, isFalse);
      expect(r.semVinculo, isTrue);
      expect(await c.read(filaOcorrenciasProvider).pendentes(), hasLength(1));
      expect(c.read(ocorrenciasProvider).pendentes, 1);
    });

    test('com vínculo e rede: central recebe e a fila esvazia', () async {
      FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'});
      final c = _container(_RepoFalso(_CentralFalsa()));
      await _esperarCarregar();

      final r = await c
          .read(ocorrenciasProvider.notifier)
          .reportar(TipoOcorrencia.paneVeiculo, descricao: '  pneu  ');

      expect(r.enviado, isTrue);
      expect(r.recebidaEm, DateTime.utc(2026, 10, 15, 12));
      expect(await c.read(filaOcorrenciasProvider).pendentes(), isEmpty);
    });

    test('sem rede: fica na fila e o reenvio usa o mesmo idLocal', () async {
      FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'});
      final central = _CentralFalsa()..semRede = true;
      final c = _container(_RepoFalso(central));
      await _esperarCarregar();
      final controller = c.read(ocorrenciasProvider.notifier);

      final r = await controller.reportar(TipoOcorrencia.acidente);
      expect(r.enviado, isFalse);
      expect(r.motivo, MotivoPendente.semRede);
      expect(c.read(ocorrenciasProvider).pendentes, 1);

      central.semRede = false;
      await controller.enviarPendentes();

      expect(await c.read(filaOcorrenciasProvider).pendentes(), isEmpty);
      expect(c.read(ocorrenciasProvider).pendentes, 0);
      expect(central.recebidos.toSet(), hasLength(1));
    });

    test('central sem REPORT (404): recusou, não "sem conexão"', () async {
      FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'});
      final c = _container(_RepoFalso(_CentralFalsa()..status = 404));
      await _esperarCarregar();

      final r = await c
          .read(ocorrenciasProvider.notifier)
          .reportar(TipoOcorrencia.acidente);

      expect(r.motivo, MotivoPendente.recusado);
      expect(r.status, 404);
      expect(await c.read(filaOcorrenciasProvider).pendentes(), hasLength(1));
    });

    test('central com falha (503) e token revogado (401)', () async {
      FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'});
      final central = _CentralFalsa()..proximos.addAll([503, 401]);
      final c = _container(_RepoFalso(central));
      await _esperarCarregar();
      final controller = c.read(ocorrenciasProvider.notifier);

      final falha = await controller.reportar(TipoOcorrencia.acidente);
      final revogado = await controller.reportar(TipoOcorrencia.acidente);

      expect(falha.motivo, MotivoPendente.centralComFalha);
      expect(falha.status, 503);
      expect(revogado.semVinculo, isTrue);
    });

    test('aviso recusado não trava os outros da fila', () async {
      FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'});
      final central = _CentralFalsa()..semRede = true;
      final c = _container(_RepoFalso(central));
      await _esperarCarregar();
      final controller = c.read(ocorrenciasProvider.notifier);
      await controller.reportar(TipoOcorrencia.acidente);
      await controller.reportar(TipoOcorrencia.paneVeiculo);

      central
        ..semRede = false
        ..proximos.addAll([404, 201]);
      await controller.enviarPendentes();

      final fila = await c.read(filaOcorrenciasProvider).pendentes();
      expect(fila.map((o) => o.tipo), [TipoOcorrencia.acidente]);
      expect(c.read(ocorrenciasProvider).pendentes, 1);
    });
  });
}
