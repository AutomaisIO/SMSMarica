import 'dart:async';

import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/ocorrencia/data/fila_ocorrencias.dart';
import 'package:agente/features/ocorrencia/domain/ocorrencia.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// O que aconteceu com um REPORT, para a tela dizer ao motorista.
class ResultadoReport {
  const ResultadoReport.enviado(DateTime this.recebidaEm) : semVinculo = false;
  const ResultadoReport.pendente({this.semVinculo = false}) : recebidaEm = null;

  /// Quando a central recebeu; null = ainda na fila do tablet.
  final DateTime? recebidaEm;

  /// Tablet sem veículo vinculado: não há para quem mandar ainda.
  final bool semVinculo;

  bool get enviado => recebidaEm != null;
}

class EstadoOcorrencias {
  const EstadoOcorrencias({this.pendentes = 0});

  /// REPORTs guardados no tablet esperando rede (ou vínculo).
  final int pendentes;
}

/// Botão REPORT: grava o aviso no tablet ANTES de enviar (se o app cair no
/// meio, não se perde) e manda à central com a posição do GPS. Sem rede, a
/// fila reenvia sozinha a cada [intervaloReenvio]; o `idLocal` impede a
/// central de receber duas vezes.
class OcorrenciasController extends StateNotifier<EstadoOcorrencias> {
  OcorrenciasController(this._ref, this._fila)
    : super(const EstadoOcorrencias()) {
    _timer = Timer.periodic(
      intervaloReenvio,
      (_) => unawaited(enviarPendentes()),
    );
    unawaited(enviarPendentes());
  }

  static const Duration intervaloReenvio = Duration(seconds: 15);

  final Ref _ref;
  final FilaOcorrencias _fila;
  Timer? _timer;
  bool _enviando = false;

  Future<ResultadoReport> reportar(
    TipoOcorrencia tipo, {
    String? descricao,
  }) async {
    final fix = _ref.read(gpsAoVivoProvider);
    final texto = descricao?.trim();
    final ocorrencia = OcorrenciaPendente(
      idLocal: novoIdLocal(),
      tipo: tipo,
      descricao: texto == null || texto.isEmpty ? null : texto,
      latitude: fix?.latitude,
      longitude: fix?.longitude,
      precisaoM: fix != null && fix.accuracy > 0 ? fix.accuracy : null,
      ocorridaEm: DateTime.now().toUtc(),
    );
    await _fila.adicionar(ocorrencia);

    final token = _ref.read(dispositivoProvider).token;
    if (token == null) {
      await _atualizarContagem();
      return const ResultadoReport.pendente(semVinculo: true);
    }
    try {
      final recebida = await _ref
          .read(dispositivoRepositoryProvider)
          .enviarOcorrencia(token, ocorrencia);
      await _fila.remover(ocorrencia.idLocal);
      await _atualizarContagem();
      return ResultadoReport.enviado(recebida);
    } on Object catch (_) {
      // Sem rede/servidor fora (ou token revogado): fica na fila.
      await _atualizarContagem();
      return const ResultadoReport.pendente();
    }
  }

  /// Reenvia o que ficou na fila, do mais antigo ao mais novo.
  Future<void> enviarPendentes() async {
    if (_enviando) return;
    _enviando = true;
    try {
      final token = _ref.read(dispositivoProvider).token;
      if (token == null) return;
      final repo = _ref.read(dispositivoRepositoryProvider);
      for (final o in await _fila.pendentes()) {
        try {
          await repo.enviarOcorrencia(token, o);
          await _fila.remover(o.idLocal);
        } on Object catch (_) {
          break; // Sem rede: o próximo ciclo tenta de novo.
        }
      }
    } on Object catch (_) {
      // Fila ilegível agora: o próximo ciclo tenta de novo.
    } finally {
      _enviando = false;
      await _atualizarContagem();
    }
  }

  Future<void> _atualizarContagem() async {
    try {
      final n = (await _fila.pendentes()).length;
      if (mounted) state = EstadoOcorrencias(pendentes: n);
    } on Object catch (_) {}
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

/// Criado na raiz do app (AgenteApp): a fila reenvia mesmo sem abrir o REPORT.
final ocorrenciasProvider =
    StateNotifierProvider<OcorrenciasController, EstadoOcorrencias>(
      (ref) => OcorrenciasController(ref, ref.watch(filaOcorrenciasProvider)),
    );
