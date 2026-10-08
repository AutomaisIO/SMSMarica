import 'package:flutter/services.dart';

/// Canal nativo (MainActivity.kt) para identificação do tablet e tela ligada.
class CanalDispositivo {
  const CanalDispositivo();

  static const _canal = MethodChannel('io.automais.smsmais.agente/dispositivo');

  /// `{modelo, identificador}` do aparelho (ANDROID_ID como identificador).
  Future<({String? modelo, String? identificador})> info() async {
    try {
      final mapa = await _canal.invokeMapMethod<String, String?>('info');
      return (modelo: mapa?['modelo'], identificador: mapa?['identificador']);
    } on Object catch (_) {
      return (modelo: null, identificador: null);
    }
  }

  /// Liga/desliga o `FLAG_KEEP_SCREEN_ON` da janela.
  Future<void> manterTelaLigada({required bool ligada}) async {
    try {
      await _canal.invokeMethod<void>('manterTelaLigada', ligada);
    } on Object catch (_) {
      // Sem canal (teste/plataforma): ignora.
    }
  }
}
