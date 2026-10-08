import 'dart:async';
import 'dart:convert';

import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/shared/plataforma/canal_dispositivo.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Situação do vínculo tablet ↔ veículo.
class EstadoDispositivo {
  const EstadoDispositivo({
    this.carregando = true,
    this.token,
    this.veiculo,
    this.aviso,
  });

  final bool carregando;
  final String? token;
  final VeiculoVinculado? veiculo;

  /// Mensagem para a tela (ex.: "Tablet desvinculado no painel").
  final String? aviso;

  bool get vinculado => token != null;

  EstadoDispositivo copyWith({
    bool? carregando,
    String? token,
    VeiculoVinculado? veiculo,
    String? aviso,
    bool limparToken = false,
    bool limparAviso = false,
  }) => EstadoDispositivo(
    carregando: carregando ?? this.carregando,
    token: limparToken ? null : (token ?? this.token),
    veiculo: limparToken ? null : (veiculo ?? this.veiculo),
    aviso: limparAviso ? null : (aviso ?? this.aviso),
  );
}

class DispositivoController extends StateNotifier<EstadoDispositivo> {
  DispositivoController(this._repo, this._tokens, this._storage)
    : super(const EstadoDispositivo()) {
    unawaited(carregar());
  }

  final DispositivoRepository _repo;
  final TokenDispositivoStore _tokens;
  final FlutterSecureStorage _storage;

  static const _chaveVeiculo = 'dispositivo_veiculo';

  /// Lê o token salvo e confirma o veículo no servidor (sem rede, usa o cache).
  Future<void> carregar() async {
    final token = await _tokens.ler();
    if (token == null) {
      state = const EstadoDispositivo(carregando: false);
      return;
    }
    final cache = await _lerVeiculoCache();
    state = EstadoDispositivo(carregando: false, token: token, veiculo: cache);
    try {
      final veiculo = await _repo.eu(token);
      await _salvarVeiculoCache(veiculo);
      if (mounted) state = state.copyWith(veiculo: veiculo);
    } on TokenDispositivoInvalido {
      await desvincularLocal('Tablet desvinculado no painel. Vincule de novo.');
    } on Object catch (_) {
      // Sem rede: mantém o vínculo e o veículo do cache.
    }
  }

  /// Ativa com o código gerado em Veículos › Tablet vinculado.
  Future<VeiculoVinculado> ativar(String codigo) async {
    final info = await const CanalDispositivo().info();
    final r = await _repo.ativar(
      codigo: codigo.trim().toUpperCase(),
      modelo: info.modelo,
      identificador: info.identificador,
    );
    await _tokens.salvar(r.token);
    await _salvarVeiculoCache(r.veiculo);
    state = EstadoDispositivo(
      carregando: false,
      token: r.token,
      veiculo: r.veiculo,
    );
    return r.veiculo;
  }

  /// Apaga o vínculo local (token revogado no servidor ou desvinculado aqui).
  Future<void> desvincularLocal([String? aviso]) async {
    await _tokens.apagar();
    await _storage.delete(key: _chaveVeiculo);
    if (mounted) state = EstadoDispositivo(carregando: false, aviso: aviso);
  }

  void limparAviso() => state = state.copyWith(limparAviso: true);

  Future<VeiculoVinculado?> _lerVeiculoCache() async {
    try {
      final s = await _storage.read(key: _chaveVeiculo);
      if (s == null) return null;
      return VeiculoVinculado.fromJson(jsonDecode(s) as Map<String, dynamic>);
    } on Object catch (_) {
      return null;
    }
  }

  Future<void> _salvarVeiculoCache(VeiculoVinculado v) => _storage.write(
    key: _chaveVeiculo,
    value: jsonEncode(<String, dynamic>{
      'veiculoId': v.veiculoId,
      'veiculoPlaca': v.placa,
      'veiculoModelo': v.modelo,
    }),
  );
}

final dispositivoProvider =
    StateNotifierProvider<DispositivoController, EstadoDispositivo>(
      (ref) => DispositivoController(
        ref.watch(dispositivoRepositoryProvider),
        ref.watch(tokenDispositivoStoreProvider),
        const FlutterSecureStorage(
          aOptions: AndroidOptions(encryptedSharedPreferences: true),
        ),
      ),
    );
