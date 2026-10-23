import 'dart:async';

import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/navegacao/data/localizacao_passageiros_repository.dart';
import 'package:agente/features/navegacao/domain/posicao_passageiro.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Enquanto a tela de navegação está aberta, consulta a cada 15 s quem das
/// paradas deste carro compartilha a localização (ADR-0079 §5), por
/// [PosicaoPassageiro.chave]. Só em memória; fechou a tela, parou.
class LocalizacaoPassageirosController
    extends StateNotifier<Map<String, PosicaoPassageiro>> {
  LocalizacaoPassageirosController(
    this._repo,
    this._token, {
    Duration intervalo = const Duration(seconds: 15),
  }) : super(const {}) {
    unawaited(consultar());
    _timer = Timer.periodic(intervalo, (_) => unawaited(consultar()));
  }

  final LocalizacaoPassageirosRepository _repo;
  final String? Function() _token;
  Timer? _timer;
  bool _consultando = false;

  Future<void> consultar() async {
    final token = _token();
    if (token == null || _consultando || !mounted) return;
    _consultando = true;
    try {
      final lista = await _repo.listar(token);
      if (mounted) state = {for (final p in lista) p.chave: p};
    } on LocalizacaoIndisponivel {
      // Servidor sem a função: para de perguntar nesta tela.
      _timer?.cancel();
      if (mounted) state = const {};
    } on Object {
      // Sem rede ou token recusado: fica como estava (o pino mostra a idade
      // da posição) e a próxima consulta tenta de novo.
    } finally {
      _consultando = false;
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

final localizacaoPassageirosProvider =
    StateNotifierProvider.autoDispose<
      LocalizacaoPassageirosController,
      Map<String, PosicaoPassageiro>
    >(
      (ref) => LocalizacaoPassageirosController(
        ref.watch(localizacaoPassageirosRepositoryProvider),
        () => ref.read(dispositivoProvider).token,
      ),
    );
