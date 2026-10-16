import 'dart:convert';

import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/viagem/domain/viagem.dart';
import 'package:agente/shared/api/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Manda as marcações da viagem à central, pelo token do veículo.
class ViagemRepository {
  ViagemRepository(this._dio);

  final Dio _dio;

  /// 401 → [TokenDispositivoInvalido]; outra recusa (4xx, fora 408/429) →
  /// [RecusaDaCentral] (não adianta reenviar); sem rede → [DioException].
  Future<void> registrar(String token, EventoViagemPendente e) async {
    try {
      await _dio.post<void>(
        '/rastreamento/dispositivos/viagem/eventos',
        data: e.toJson(),
        options: Options(
          headers: <String, String>{'X-Dispositivo-Token': token},
        ),
      );
    } on DioException catch (erro) {
      final status = erro.response?.statusCode;
      if (status == 401) throw const TokenDispositivoInvalido();
      final passageira = status == 408 || status == 429;
      if (status != null && status >= 400 && status < 500 && !passageira) {
        throw RecusaDaCentral(
          MotoristaRepository.mensagemDoServidor(erro.response?.data),
        );
      }
      rethrow;
    }
  }
}

/// Onde a viagem está, guardado no tablet (cifrado): sobrevive a fechar o
/// app no meio da rota. Só a rota atual.
class AndamentoLocal {
  AndamentoLocal(this._storage);

  static const _chave = 'andamento_viagem';

  final FlutterSecureStorage _storage;

  Future<AndamentoViagem?> ler(String rotaId) async {
    try {
      final s = await _storage.read(key: _chave);
      if (s == null) return null;
      final a = AndamentoViagem.fromJson(jsonDecode(s) as Map<String, dynamic>);
      return a.rotaId == rotaId ? a : null;
    } on Object catch (_) {
      return null;
    }
  }

  Future<void> salvar(AndamentoViagem a) async {
    try {
      await _storage.write(key: _chave, value: jsonEncode(a.toJson()));
    } on Object catch (_) {}
  }
}

final viagemRepositoryProvider = Provider<ViagemRepository>(
  (ref) => ViagemRepository(ref.watch(dioProvider)),
);

final andamentoLocalProvider = Provider<AndamentoLocal>(
  (_) => AndamentoLocal(
    const FlutterSecureStorage(
      aOptions: AndroidOptions(encryptedSharedPreferences: true),
    ),
  ),
);
