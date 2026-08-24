import 'dart:async';

import 'package:agente/features/rastreamento/data/rastreamento_repository.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

/// Compartilha a posição do motorista com o backend a cada ~1 minuto enquanto
/// há sessão ativa. Usa o stream do geolocator com serviço em foreground no
/// Android, de modo a continuar reportando com o app em segundo plano.
class ServicoGps {
  ServicoGps(this._repo);

  final RastreamentoRepository _repo;

  static const Duration intervalo = Duration(minutes: 1);

  StreamSubscription<Position>? _assinatura;
  String? _motoristaId;

  bool get ativo => _assinatura != null;

  /// Inicia o envio periódico para o [motoristaId]. Idempotente.
  Future<void> iniciar(String motoristaId) async {
    if (ativo && _motoristaId == motoristaId) return;
    await parar();
    _motoristaId = motoristaId;

    if (!await _garantirPermissao()) return;

    // Posição imediata + atualizações a cada 1 min via stream.
    await _enviarAtual();
    final stream = Geolocator.getPositionStream(locationSettings: _settings());
    _assinatura = stream.listen(
      _aoReceber,
      onError: (_) {},
      cancelOnError: false,
    );
  }

  Future<void> parar() async {
    await _assinatura?.cancel();
    _assinatura = null;
    _motoristaId = null;
  }

  LocationSettings _settings() {
    // Serviço em foreground mantém o GPS reportando em segundo plano (Android).
    return AndroidSettings(
      accuracy: LocationAccuracy.high,
      intervalDuration: intervalo,
      foregroundNotificationConfig: const ForegroundNotificationConfig(
        notificationTitle: 'SMS Maricá — Transporte',
        notificationText: 'Compartilhando a localização da rota.',
        enableWakeLock: true,
      ),
    );
  }

  Future<bool> _garantirPermissao() async {
    if (!await Geolocator.isLocationServiceEnabled()) return false;
    var permissao = await Geolocator.checkPermission();
    if (permissao == LocationPermission.denied) {
      permissao = await Geolocator.requestPermission();
    }
    return permissao == LocationPermission.always ||
        permissao == LocationPermission.whileInUse;
  }

  Future<void> _enviarAtual() async {
    try {
      final pos = await Geolocator.getCurrentPosition(
        locationSettings:
            const LocationSettings(accuracy: LocationAccuracy.high),
      );
      await _aoReceber(pos);
    } on Object catch (_) {
      // Sem fix imediato: o stream continua tentando.
    }
  }

  Future<void> _aoReceber(Position pos) async {
    final id = _motoristaId;
    if (id == null) return;
    try {
      await _repo.enviarPonto(
        motoristaId: id,
        latitude: pos.latitude,
        longitude: pos.longitude,
        capturadoEm: DateTime.now().toUtc(),
      );
    } on Object catch (_) {
      // Offline/erro de rede: o próximo ciclo reenvia.
    }
  }
}

final servicoGpsProvider = Provider<ServicoGps>(
  (ref) => ServicoGps(ref.watch(rastreamentoRepositoryProvider)),
);
