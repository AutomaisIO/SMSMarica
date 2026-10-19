import 'dart:async';

import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/viagem/data/fila_viagem.dart';
import 'package:agente/features/viagem/data/viagem_repository.dart';
import 'package:agente/features/viagem/domain/viagem.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Leva as marcações da viagem à central: guarda na fila (cifrada) e manda na
/// hora e a cada [intervalo], na ordem em que aconteceram. Recusa da central
/// (4xx) sai da fila para não travar as outras; sem rede, para e tenta no
/// próximo ciclo. O estado é quantas faltam chegar.
class ViagemController extends StateNotifier<int> {
  ViagemController(this._ref, this._fila, this._repo) : super(0) {
    _timer = Timer.periodic(intervalo, (_) => unawaited(enviarPendentes()));
    unawaited(enviarPendentes());
  }

  static const intervalo = Duration(seconds: 20);

  final Ref _ref;
  final FilaViagem _fila;
  final ViagemRepository _repo;
  Timer? _timer;
  bool _enviando = false;

  /// Guarda e tenta mandar já.
  Future<void> registrar(List<EventoViagemPendente> eventos) async {
    if (eventos.isEmpty) return;
    await _fila.adicionar(eventos);
    await enviarPendentes();
  }

  Future<void> enviarPendentes() async {
    if (_enviando) return;
    _enviando = true;
    try {
      final token = _ref.read(dispositivoProvider).token;
      if (token == null) return;
      for (final e in await _fila.pendentes()) {
        try {
          await _repo.registrar(token, e);
          await _fila.remover(e.idLocal);
        } on RecusaDaCentral catch (_) {
          await _fila.remover(e.idLocal);
        } on Object catch (_) {
          break;
        }
      }
    } on Object catch (_) {
    } finally {
      _enviando = false;
      try {
        final n = (await _fila.pendentes()).length;
        if (mounted) state = n;
      } on Object catch (_) {}
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

/// Criado na raiz do app: a fila anda mesmo fora da tela de navegação.
final viagemProvider = StateNotifierProvider<ViagemController, int>(
  (ref) => ViagemController(
    ref,
    ref.watch(filaViagemProvider),
    ref.watch(viagemRepositoryProvider),
  ),
);
