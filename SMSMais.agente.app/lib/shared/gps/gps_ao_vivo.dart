import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

/// GPS do tablet em ~1 s, compartilhado pela tela de Deslocamento e pelo
/// envio de posições ao servidor. Roda com serviço em foreground (notificação
/// fixa) para continuar com o app em segundo plano. O estado é o último fix.
///
/// A permissão de localização vem concedida pelo MDM (Device Owner); sem ela,
/// tenta de novo a cada 10 s em vez de abrir diálogo (bloqueado no quiosque).
///
/// Usa só o GPS nativo (`LocationManager`): o tablet não depende do Play
/// Services para ter posição.
class GpsAoVivo extends StateNotifier<Position?> {
  GpsAoVivo() : super(null);

  StreamSubscription<Position>? _assinatura;
  Timer? _retentativa;

  bool get ativo => _assinatura != null;

  Future<void> iniciar() async {
    if (_assinatura != null) return;
    _retentativa?.cancel();
    if (!await _temPermissao()) {
      _retentativa = Timer(const Duration(seconds: 10), iniciar);
      return;
    }
    _assinatura =
        Geolocator.getPositionStream(
          locationSettings: AndroidSettings(
            // GPS nativo do Android, nunca o do Play Services. Nos tablets
            // genéricos da frota o Play Services passa na checagem do plugin
            // mas recusa a conexão (SERVICE_INVALID), e a exceção derrubava
            // o app ao abrir — em loop, com o quiosque relançando.
            forceLocationManager: true,
            intervalDuration: const Duration(seconds: 1),
            foregroundNotificationConfig: const ForegroundNotificationConfig(
              notificationTitle: 'SMS Maricá — Transporte',
              notificationText: 'Compartilhando a localização do veículo.',
              enableWakeLock: true,
            ),
          ),
        ).listen(
          (p) => state = p,
          onError: (Object _) => _reiniciar(),
          cancelOnError: true,
        );
  }

  Future<void> _reiniciar() async {
    await _assinatura?.cancel();
    _assinatura = null;
    _retentativa?.cancel();
    _retentativa = Timer(const Duration(seconds: 10), iniciar);
  }

  /// Sem `Geolocator.isLocationServiceEnabled()`: ela ignora o
  /// `forceLocationManager`, pergunta sempre ao Play Services e, na recusa
  /// (SERVICE_INVALID), o geolocator_android 4.6.2 chama `getResult()` mesmo
  /// assim e derruba o app. Localização desligada já chega como erro do
  /// stream (→ [_reiniciar], nova tentativa em 10 s).
  Future<bool> _temPermissao() async {
    final p = await Geolocator.checkPermission();
    return p == LocationPermission.always || p == LocationPermission.whileInUse;
  }

  @override
  void dispose() {
    _retentativa?.cancel();
    unawaited(_assinatura?.cancel());
    super.dispose();
  }
}

final gpsAoVivoProvider = StateNotifierProvider<GpsAoVivo, Position?>((ref) {
  final gps = GpsAoVivo();
  unawaited(gps.iniciar());
  return gps;
});
