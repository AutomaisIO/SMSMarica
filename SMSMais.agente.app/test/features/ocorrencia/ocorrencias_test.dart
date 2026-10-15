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

/// Servidor de mentira: aceita ou recusa por falta de rede, e guarda o que
/// recebeu.
class _RepoFalso extends DispositivoRepository {
  _RepoFalso() : super(Dio());

  bool semRede = false;
  final List<String> recebidos = [];

  @override
  Future<VeiculoVinculado> eu(String token) async =>
      const VeiculoVinculado(veiculoId: 'v1', placa: 'ABC1D23');

  @override
  Future<DateTime> enviarOcorrencia(
    String token,
    OcorrenciaPendente ocorrencia,
  ) async {
    recebidos.add(ocorrencia.idLocal);
    if (semRede) {
      throw DioException(requestOptions: RequestOptions(), message: 'off');
    }
    return DateTime.utc(2026, 10, 15, 12);
  }
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
      final c = _container(_RepoFalso());
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
      final repo = _RepoFalso();
      final c = _container(repo);
      await _esperarCarregar();

      final r = await c
          .read(ocorrenciasProvider.notifier)
          .reportar(TipoOcorrencia.paneVeiculo, descricao: '  pneu  ');

      expect(r.enviado, isTrue);
      expect(await c.read(filaOcorrenciasProvider).pendentes(), isEmpty);
    });

    test('sem rede: fica na fila e o reenvio usa o mesmo idLocal', () async {
      FlutterSecureStorage.setMockInitialValues({'dispositivo_token': 't'});
      final repo = _RepoFalso()..semRede = true;
      final c = _container(repo);
      await _esperarCarregar();
      final controller = c.read(ocorrenciasProvider.notifier);

      final r = await controller.reportar(TipoOcorrencia.acidente);
      expect(r.enviado, isFalse);
      expect(r.semVinculo, isFalse);
      expect(c.read(ocorrenciasProvider).pendentes, 1);

      repo.semRede = false;
      await controller.enviarPendentes();

      expect(await c.read(filaOcorrenciasProvider).pendentes(), isEmpty);
      expect(c.read(ocorrenciasProvider).pendentes, 0);
      expect(repo.recebidos.toSet(), hasLength(1));
    });
  });
}
