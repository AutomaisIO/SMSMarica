import 'dart:async';

import 'package:agente/features/deslocamento/application/deslocamento_controller.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Manda a posição do tablet para o Mapa da frota enquanto houver vínculo:
/// a cada 5 s com deslocamento ativo, a cada 60 s parado. Sem rede, guarda até
/// [maxFila] pontos em memória e reenvia em lote. 401 = desvinculado no painel.
class EnvioPosicoesService {
  EnvioPosicoesService(this._ref);

  final Ref _ref;

  static const int maxFila = 500;
  static const Duration tick = Duration(seconds: 5);
  static const Duration intervaloAtivo = Duration(seconds: 5);
  static const Duration intervaloParado = Duration(seconds: 60);

  final List<PontoDispositivo> _fila = <PontoDispositivo>[];
  Timer? _timer;
  DateTime? _ultimoEnfileirado;
  DateTime? _ultimoFixEnfileirado;
  bool _enviando = false;

  void iniciar() {
    _timer ??= Timer.periodic(tick, (_) => unawaited(_ciclo()));
  }

  void parar() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> _ciclo() async {
    final token = _ref.read(dispositivoProvider).token;
    if (token == null) return;

    _enfileirarSeDevido();
    if (_fila.isEmpty || _enviando) return;

    _enviando = true;
    final lote = List<PontoDispositivo>.of(_fila);
    try {
      await _ref.read(dispositivoRepositoryProvider).enviarPontos(token, lote);
      _fila.removeRange(0, lote.length);
    } on TokenDispositivoInvalido {
      _fila.clear();
      await _ref
          .read(dispositivoProvider.notifier)
          .desvincularLocal('Tablet desvinculado no painel. Vincule de novo.');
    } on Object catch (_) {
      // Sem rede/servidor fora: mantém na fila para o próximo ciclo.
    } finally {
      _enviando = false;
    }
  }

  void _enfileirarSeDevido() {
    final fix = _ref.read(gpsAoVivoProvider);
    if (fix == null) return;
    // Mesmo fix já enfileirado (GPS sem atualização): não duplica.
    if (_ultimoFixEnfileirado == fix.timestamp) return;

    final desloc = _ref.read(deslocamentoProvider);
    final intervalo = desloc.ativo ? intervaloAtivo : intervaloParado;
    final agora = DateTime.now();
    final ultimo = _ultimoEnfileirado;
    if (ultimo != null && agora.difference(ultimo) < intervalo) return;

    _fila.add(
      PontoDispositivo(
        latitude: fix.latitude,
        longitude: fix.longitude,
        capturadoEm: fix.timestamp.toUtc(),
        velocidadeKmh: desloc.velocidadeAoVivoKmh,
        rumo: fix.heading >= 0 && fix.heading < 360 ? fix.heading : null,
        precisaoM: fix.accuracy > 0 ? fix.accuracy : null,
      ),
    );
    if (_fila.length > maxFila) _fila.removeRange(0, _fila.length - maxFila);
    _ultimoEnfileirado = agora;
    _ultimoFixEnfileirado = fix.timestamp;
  }
}

/// Criado na raiz do app (AgenteApp) — começa a rodar assim que lido.
final envioPosicoesProvider = Provider<EnvioPosicoesService>((ref) {
  final service = EnvioPosicoesService(ref)..iniciar();
  ref.onDispose(service.parar);
  return service;
});
