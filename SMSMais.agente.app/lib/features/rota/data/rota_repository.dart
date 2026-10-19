import 'dart:async';
import 'dart:convert';

import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/rota/domain/parada.dart';
import 'package:agente/features/rota/domain/rota_do_tablet.dart';
import 'package:agente/shared/api/dio_client.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Rotas de hoje do veículo do tablet, montadas pela central no painel
/// (`GET /rastreamento/dispositivos/rotas-do-dia`, pelo token do veículo).
/// A última resposta fica guardada cifrada no tablet: sem rede, vale a do
/// mesmo dia (em Brasília) — tem nome, endereço e telefone de paciente.
class RotaRepository {
  RotaRepository(this._dio, this._storage, {DateTime Function()? relogio})
    : _relogio = relogio ?? DateTime.now;

  static const _chave = 'rotas_do_dia';

  final Dio _dio;
  final FlutterSecureStorage _storage;
  final DateTime Function() _relogio;

  Future<List<RotaDoTablet>> rotasDoDia(
    String token, {
    String veiculo = '',
  }) async {
    try {
      final r = await _dio.get<List<dynamic>>(
        '/rastreamento/dispositivos/rotas-do-dia',
        options: Options(
          headers: <String, String>{'X-Dispositivo-Token': token},
        ),
      );
      final lista = r.data ?? const <dynamic>[];
      unawaited(_guardar(lista));
      return converter(lista, veiculo: veiculo);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const TokenDispositivoInvalido();
      final guardada = await _guardadaDoDia();
      if (guardada != null) return converter(guardada, veiculo: veiculo);
      rethrow;
    }
  }

  static List<RotaDoTablet> converter(
    List<dynamic> json, {
    String veiculo = '',
  }) => [
    for (final r in json)
      RotaDoTablet.fromJson(r as Map<String, dynamic>, veiculo: veiculo),
  ];

  Future<void> _guardar(List<dynamic> lista) async {
    try {
      await _storage.write(
        key: _chave,
        value: jsonEncode({
          'dia': Sessao.diaEmBrasilia(_relogio()).toIso8601String(),
          'rotas': lista,
        }),
      );
    } on Object catch (_) {}
  }

  Future<List<dynamic>?> _guardadaDoDia() async {
    try {
      final s = await _storage.read(key: _chave);
      if (s == null) return null;
      final j = jsonDecode(s) as Map<String, dynamic>;
      final hoje = Sessao.diaEmBrasilia(_relogio()).toIso8601String();
      return j['dia'] == hoje ? j['rotas'] as List<dynamic> : null;
    } on Object catch (_) {
      return null;
    }
  }
}

/// A rota que este motorista vai fazer agora: a em andamento primeiro, depois
/// a planejada; a dele se houver, senão a do veículo (quem está ao volante é
/// quem faz). Concluídas só se não sobrar outra.
RotaDoTablet? escolherRota(List<RotaDoTablet> rotas, String? motoristaId) {
  if (rotas.isEmpty) return null;
  final abertas = rotas.where((r) => !r.concluida).toList();
  final candidatas = abertas.isEmpty ? rotas : abertas;
  final minhas = candidatas.where((r) => r.motoristaId == motoristaId).toList();
  final lista = [...(minhas.isNotEmpty ? minhas : candidatas)]
    ..sort((a, b) => (b.emAndamento ? 1 : 0) - (a.emAndamento ? 1 : 0));
  return lista.first;
}

final rotaRepositoryProvider = Provider<RotaRepository>(
  (ref) => RotaRepository(
    ref.watch(dioProvider),
    const FlutterSecureStorage(
      aOptions: AndroidOptions(encryptedSharedPreferences: true),
    ),
  ),
);

/// Rotas de hoje do veículo (vazia sem vínculo).
final rotasDoDiaProvider = FutureProvider<List<RotaDoTablet>>((ref) async {
  final dispositivo = ref.watch(dispositivoProvider);
  final token = dispositivo.token;
  if (token == null) return const [];
  return ref
      .watch(rotaRepositoryProvider)
      .rotasDoDia(token, veiculo: dispositivo.veiculo?.placa ?? '');
});

/// A rota do motorista da sessão (null = nenhum translado hoje).
final rotaAtualProvider = FutureProvider<RotaDoTablet?>((ref) async {
  final rotas = await ref.watch(rotasDoDiaProvider.future);
  return escolherRota(rotas, ref.watch(sessaoProvider)?.motoristaId);
});

/// Paradas da rota atual, para a tela Rota do dia.
final rotaDoDiaProvider = FutureProvider<List<Parada>>((ref) async {
  final rota = await ref.watch(rotaAtualProvider.future);
  return [
    for (final p in rota?.translado.paradas ?? const <ParadaNavegacao>[])
      Parada(
        id: p.id,
        ordem: p.ordem,
        pacienteNome: p.rotulo,
        endereco: p.endereco,
        horarioPrevisto: p.horarioPrevisto,
        status: p.concluida ? StatusParada.concluida : StatusParada.pendente,
        etiqueta: !p.volta
            ? null
            : p.tipo == TipoParada.destino
            ? 'Volta · deixar em casa'
            : 'Volta · buscar na unidade',
      ),
  ];
});
