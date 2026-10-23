import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

/// Uma leitura do GPS do aparelho.
typedef LeituraGps = ({
  double latitude,
  double longitude,
  double? precisaoM,
  DateTime capturadaEm,
  bool simulada,
});

/// O que o aparelho responde sobre usar a localização com o app aberto.
enum AcessoGps {
  /// Pode usar, com a localização precisa.
  liberado,

  /// Pode usar, mas só a aproximada (Android 12+, iOS 14+): o app envia assim mesmo (ADR-0079 §3).
  aproximado,

  /// Negou; o próximo "Permitir" pergunta de novo.
  negado,

  /// Negou de vez: só pelas configurações do aparelho.
  negadoDeVez,

  /// A localização do aparelho está desligada.
  desligado,

  /// O aparelho não informa a localização.
  indisponivel;

  bool get podeUsar => this == liberado || this == aproximado;
}

/// O GPS do aparelho atrás de uma interface (os testes trocam por um falso). Só "durante o uso",
/// nunca em segundo plano (ADR-0079 §3).
abstract class FonteGps {
  /// Confere a permissão sem perguntar nada (volta ao app, consulta de minuto em minuto).
  Future<AcessoGps> conferirAcesso();

  /// Pede a permissão, se o aparelho ainda puder perguntar. Só depois do "Compartilhar".
  Future<AcessoGps> pedirAcesso();

  /// Posições enquanto houver assinatura; cancelar a assinatura desliga o GPS.
  Stream<LeituraGps> posicoes();

  /// Abre as configurações: as da localização, se ela está desligada; senão, as do app.
  Future<void> abrirConfiguracoes(AcessoGps motivo);
}

class GpsDoAparelho implements FonteGps {
  const GpsDoAparelho();

  @override
  Future<AcessoGps> conferirAcesso() => _acesso(pedir: false);

  @override
  Future<AcessoGps> pedirAcesso() => _acesso(pedir: true);

  Future<AcessoGps> _acesso({required bool pedir}) async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) return AcessoGps.desligado;
      var permissao = await Geolocator.checkPermission();
      if (pedir && permissao == LocationPermission.denied) permissao = await Geolocator.requestPermission();
      return switch (permissao) {
        LocationPermission.whileInUse || LocationPermission.always =>
          await Geolocator.getLocationAccuracy() == LocationAccuracyStatus.reduced
              ? AcessoGps.aproximado
              : AcessoGps.liberado,
        LocationPermission.deniedForever => AcessoGps.negadoDeVez,
        LocationPermission.denied => AcessoGps.negado,
        LocationPermission.unableToDetermine => AcessoGps.indisponivel,
      };
    } on Object {
      return AcessoGps.indisponivel;
    }
  }

  /// Recusou a "Precisão do local" do Google: até o app fechar, a posição vem do Android direto.
  static bool _semGoogle = false;

  @visibleForTesting
  static void esquecerRecusaDoGoogle() => _semGoogle = false;

  /// No Android, primeiro o cliente do Google Play Services (o melhor dentro de prédio). Ele confere as
  /// configurações e, com a "Precisão do local" desligada, mostra o aviso do Google; recusado (com a
  /// localização do aparelho ligada), segue pelo LocationManager do Android, que não pergunta nada e no
  /// Android 12+ usa o provedor fundido da plataforma. Sem isso, o aviso voltaria a cada consulta.
  @override
  Stream<LeituraGps> posicoes() {
    late final StreamController<LeituraGps> saida;
    StreamSubscription<Position>? atual;

    void ouvir() {
      final semGoogle = _semGoogle;
      atual = Geolocator.getPositionStream(locationSettings: _ajustes(semGoogle: semGoogle)).listen(
        (p) => saida.add((
          latitude: p.latitude,
          longitude: p.longitude,
          precisaoM: p.accuracy.isFinite && p.accuracy > 0 ? p.accuracy : null,
          capturadaEm: p.timestamp,
          simulada: p.isMocked,
        )),
        onError: (Object e) async {
          final recusouOGoogle = !semGoogle &&
              defaultTargetPlatform == TargetPlatform.android &&
              e is LocationServiceDisabledException &&
              await Geolocator.isLocationServiceEnabled();
          if (saida.isClosed) return;
          if (!recusouOGoogle) {
            saida.addError(e);
            return;
          }
          _semGoogle = true;
          await atual?.cancel();
          if (!saida.isClosed) ouvir();
        },
      );
    }

    saida = StreamController<LeituraGps>(onListen: ouvir, onCancel: () => atual?.cancel());
    return saida.stream;
  }

  /// Uma leitura a cada 5 s no Android, para o envio sair a cada ~15 s (quem decide é o controle da
  /// localização). Sem serviço em primeiro plano e sem segundo plano no iOS: fechou o app, parou.
  static LocationSettings _ajustes({required bool semGoogle}) => switch (defaultTargetPlatform) {
        TargetPlatform.android =>
          AndroidSettings(intervalDuration: const Duration(seconds: 5), forceLocationManager: semGoogle),
        TargetPlatform.iOS => AppleSettings(allowBackgroundLocationUpdates: false),
        _ => const LocationSettings(),
      };

  @override
  Future<void> abrirConfiguracoes(AcessoGps motivo) async {
    try {
      if (motivo == AcessoGps.desligado) {
        await Geolocator.openLocationSettings();
      } else {
        await Geolocator.openAppSettings();
      }
    } on Object {
      /* sem as configurações, a faixa continua explicando */
    }
  }
}

final fonteGpsProvider = Provider<FonteGps>((_) => const GpsDoAparelho());
