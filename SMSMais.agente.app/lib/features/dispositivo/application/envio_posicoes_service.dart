import 'dart:async';

import 'package:agente/features/deslocamento/application/deslocamento_controller.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/data/fila_posicoes.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Manda a posição do tablet para o Mapa da frota enquanto houver vínculo:
/// a cada 5 s com deslocamento ativo, a cada 60 s parado. Todo ponto passa
/// pela [FilaPosicoes] em disco: sem rede, ou com o tablet reiniciado no
/// meio, ele espera lá e sai em lotes de até [maxLote], do mais antigo ao
/// mais novo, quando a rede volta. 401 = desvinculado no painel.
class EnvioPosicoesService {
  EnvioPosicoesService(this._ref, this._fila);

  final Ref _ref;
  final FilaPosicoes _fila;

  /// Teto do servidor por envio.
  static const int maxLote = 500;
  static const Duration tick = Duration(seconds: 5);
  static const Duration intervaloAtivo = Duration(seconds: 5);
  static const Duration intervaloParado = Duration(seconds: 60);
  static const _avisoDesvinculado =
      'Tablet desvinculado no painel. Vincule de novo.';

  Timer? _timer;
  DateTime? _ultimoEnfileirado;
  DateTime? _ultimoFixEnfileirado;
  String? _veiculoDaFila;
  bool _enviando = false;

  void iniciar() {
    _timer ??= Timer.periodic(tick, (_) => unawaited(_ciclo()));
  }

  void parar() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> _ciclo() async {
    final estado = _ref.read(dispositivoProvider);
    final token = estado.token;
    if (token == null) return;

    try {
      await _descartarSeTrocouDeVeiculo(estado.veiculo?.veiculoId);
      await _enfileirarSeDevido();
    } on Object catch (_) {
      // Fila indisponível neste ciclo: o próximo tenta de novo.
    }
    if (_enviando) return;

    _enviando = true;
    try {
      await _esvaziarFila(token);
    } on TokenDispositivoInvalido {
      // Só desvincula se o token recusado ainda é o atual: o tablet pode ter
      // sido vinculado de novo no meio do envio.
      if (_ref.read(dispositivoProvider).token == token) {
        await _descartarFila();
        await _ref
            .read(dispositivoProvider.notifier)
            .desvincularLocal(_avisoDesvinculado);
      }
    } on Object catch (_) {
      // Sem rede/servidor fora: os pontos seguem na fila para o próximo ciclo.
    } finally {
      _enviando = false;
    }
  }

  /// Ponto de um carro não pode sair com o token de outro: vinculado a outro
  /// veículo, a fila antiga é descartada.
  Future<void> _descartarSeTrocouDeVeiculo(String? veiculoId) async {
    if (veiculoId == null) return;
    final anterior = _veiculoDaFila;
    _veiculoDaFila = veiculoId;
    if (anterior != null && anterior != veiculoId) await _fila.limpar();
  }

  Future<void> _enfileirarSeDevido() async {
    final fix = _ref.read(gpsAoVivoProvider);
    if (fix == null) return;
    // Mesmo fix já enfileirado (GPS sem atualização): não duplica.
    if (_ultimoFixEnfileirado == fix.timestamp) return;

    final desloc = _ref.read(deslocamentoProvider);
    final intervalo = desloc.ativo ? intervaloAtivo : intervaloParado;
    final agora = DateTime.now();
    final ultimo = _ultimoEnfileirado;
    if (ultimo != null && agora.difference(ultimo) < intervalo) return;

    // Marca antes de gravar, para o ciclo seguinte não repetir o fix.
    _ultimoEnfileirado = agora;
    _ultimoFixEnfileirado = fix.timestamp;
    await _fila.adicionar(
      PontoDispositivo(
        latitude: fix.latitude,
        longitude: fix.longitude,
        capturadoEm: fix.timestamp.toUtc(),
        velocidadeKmh: desloc.velocidadeAoVivoKmh,
        rumo: fix.heading >= 0 && fix.heading < 360 ? fix.heading : null,
        precisaoM: fix.accuracy > 0 ? fix.accuracy : null,
      ),
    );
  }

  /// Manda a fila em lotes, do mais antigo ao mais novo, até esvaziar.
  Future<void> _esvaziarFila(String token) async {
    final repo = _ref.read(dispositivoRepositoryProvider);
    while (_ref.read(dispositivoProvider).token == token) {
      final lote = await _fila.lote(maxLote);
      if (lote.isEmpty) return;
      await repo.enviarPontos(token, [for (final item in lote) item.ponto]);
      await _fila.removerAte(lote.last.id);
      if (lote.length < maxLote) return;
    }
  }

  /// Esvazia a fila sem deixar erro de disco impedir o desvínculo.
  Future<void> _descartarFila() async {
    try {
      await _fila.limpar();
    } on Object catch (_) {
      // Banco indisponível agora: segue para o desvínculo mesmo assim.
    }
  }
}

/// Criado na raiz do app (AgenteApp) — começa a rodar assim que lido.
final envioPosicoesProvider = Provider<EnvioPosicoesService>((ref) {
  final service = EnvioPosicoesService(ref, ref.watch(filaPosicoesProvider))
    ..iniciar();
  ref.onDispose(service.parar);
  return service;
});
