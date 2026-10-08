import 'dart:math' as math;

import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

/// Fase do deslocamento: nunca iniciado, contando ou congelado (após Parar).
enum FaseDeslocamento { parado, ativo, congelado }

class EstadoDeslocamento {
  const EstadoDeslocamento({
    this.fase = FaseDeslocamento.parado,
    this.velocidadeKmh = 0,
    this.velocidadeAoVivoKmh = 0,
    this.distanciaM = 0,
    this.inicio,
    this.fim,
    this.ultimoFix,
  });

  final FaseDeslocamento fase;

  /// Velocidade exibida: ao vivo durante o deslocamento, congelada depois
  /// de Parar.
  final double velocidadeKmh;

  /// Velocidade instantânea sempre atualizada (vai para o servidor).
  final double velocidadeAoVivoKmh;

  /// Odômetro do deslocamento atual, em metros.
  final double distanciaM;
  final DateTime? inicio;
  final DateTime? fim;
  final Position? ultimoFix;

  bool get ativo => fase == FaseDeslocamento.ativo;

  Duration get decorrido {
    final i = inicio;
    if (i == null) return Duration.zero;
    return (fim ?? DateTime.now()).difference(i);
  }

  EstadoDeslocamento copyWith({
    FaseDeslocamento? fase,
    double? velocidadeKmh,
    double? velocidadeAoVivoKmh,
    double? distanciaM,
    DateTime? inicio,
    DateTime? fim,
    Position? ultimoFix,
    bool limparFim = false,
  }) => EstadoDeslocamento(
    fase: fase ?? this.fase,
    velocidadeKmh: velocidadeKmh ?? this.velocidadeKmh,
    velocidadeAoVivoKmh: velocidadeAoVivoKmh ?? this.velocidadeAoVivoKmh,
    distanciaM: distanciaM ?? this.distanciaM,
    inicio: inicio ?? this.inicio,
    fim: limparFim ? null : (fim ?? this.fim),
    ultimoFix: ultimoFix ?? this.ultimoFix,
  );
}

/// Velocímetro + odômetro a partir do GPS (contrato em
/// `docs/modulos/tfd/deslocamento-tablet.md`).
///
/// Filtros do odômetro (o GPS "anda" sozinho com o carro parado):
/// - fix com precisão pior que [precisaoMaximaM] é descartado;
/// - salto que implicaria mais de [velocidadeMaximaKmh] é descartado;
/// - quase parado (< 3 km/h), passo menor que a precisão (mín. 5 m) não soma.
class DeslocamentoController extends StateNotifier<EstadoDeslocamento> {
  DeslocamentoController() : super(const EstadoDeslocamento());

  static const double precisaoMaximaM = 30;
  static const double velocidadeMaximaKmh = 180;

  /// Último fix aceito pelo odômetro (âncora do próximo passo).
  Position? _ancora;

  /// Fix anterior (qualquer), para derivar velocidade quando o sensor não dá.
  Position? _anterior;

  void iniciar() {
    _ancora = null;
    state = state.copyWith(
      fase: FaseDeslocamento.ativo,
      distanciaM: 0,
      velocidadeKmh: state.velocidadeAoVivoKmh,
      inicio: DateTime.now(),
      limparFim: true,
    );
  }

  void parar() {
    if (!state.ativo) return;
    state = state.copyWith(
      fase: FaseDeslocamento.congelado,
      fim: DateTime.now(),
    );
  }

  void aoReceberPosicao(Position p) {
    final kmh = _velocidade(p);
    _anterior = p;

    var distancia = state.distanciaM;
    if (state.ativo) distancia += _passo(p, kmh);

    state = state.copyWith(
      velocidadeAoVivoKmh: kmh,
      velocidadeKmh: state.ativo ? kmh : null,
      distanciaM: distancia,
      ultimoFix: p,
    );
  }

  double _velocidade(Position p) {
    double? kmh;
    // speedAccuracy = 0 quando o aparelho não informa (comum no Android 9).
    final sensorConfiavel =
        p.speed >= 0 && (p.speedAccuracy <= 0 || p.speedAccuracy < 3);
    if (sensorConfiavel) {
      kmh = p.speed * 3.6;
    } else {
      final ant = _anterior;
      if (ant != null) {
        final dt = p.timestamp.difference(ant.timestamp).inMilliseconds / 1000;
        if (dt >= 0.5) {
          final d = Geolocator.distanceBetween(
            ant.latitude,
            ant.longitude,
            p.latitude,
            p.longitude,
          );
          kmh = d / dt * 3.6;
        }
      }
    }
    if (kmh == null || kmh.isNaN || kmh < 1) return 0;
    return math.min(kmh, velocidadeMaximaKmh);
  }

  double _passo(Position p, double kmh) {
    if (p.accuracy > precisaoMaximaM) return 0;
    final ancora = _ancora;
    if (ancora == null) {
      _ancora = p;
      return 0;
    }
    final d = Geolocator.distanceBetween(
      ancora.latitude,
      ancora.longitude,
      p.latitude,
      p.longitude,
    );
    final dt = p.timestamp.difference(ancora.timestamp).inMilliseconds / 1000;
    if (dt > 0 && d / dt * 3.6 > velocidadeMaximaKmh) return 0;
    if (kmh < 3 && d < math.max(p.accuracy, 5)) return 0;
    _ancora = p;
    return d;
  }
}

final deslocamentoProvider =
    StateNotifierProvider<DeslocamentoController, EstadoDeslocamento>((ref) {
      final controller = DeslocamentoController();
      ref.listen<Position?>(gpsAoVivoProvider, (_, p) {
        if (p != null) controller.aoReceberPosicao(p);
      }, fireImmediately: true);
      return controller;
    });
