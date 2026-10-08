import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/shared/api/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Token do dispositivo (tablet ↔ veículo) no armazenamento cifrado do Android.
class TokenDispositivoStore {
  const TokenDispositivoStore(this._storage);

  final FlutterSecureStorage _storage;

  static const _chave = 'dispositivo_token';

  Future<String?> ler() => _storage.read(key: _chave);
  Future<void> salvar(String token) =>
      _storage.write(key: _chave, value: token);
  Future<void> apagar() => _storage.delete(key: _chave);
}

/// Erro de token revogado/inválido (HTTP 401) — o tablet foi desvinculado.
class TokenDispositivoInvalido implements Exception {
  const TokenDispositivoInvalido();
}

/// Código de ativação inválido, expirado ou já usado (HTTP 404).
class CodigoAtivacaoInvalido implements Exception {
  const CodigoAtivacaoInvalido();
}

/// Endpoints do tablet: `/rastreamento/dispositivos/*` com `X-Dispositivo-Token`.
class DispositivoRepository {
  DispositivoRepository(this._dio);

  final Dio _dio;

  static const _header = 'X-Dispositivo-Token';

  /// Troca o código de ativação (gerado no painel) por um token próprio.
  Future<({String token, VeiculoVinculado veiculo})> ativar({
    required String codigo,
    String? modelo,
    String? identificador,
  }) async {
    try {
      final resp = await _dio.post<Map<String, dynamic>>(
        '/rastreamento/dispositivos/ativar',
        data: <String, dynamic>{
          'codigo': codigo,
          'modelo': modelo,
          'identificador': identificador,
        },
      );
      final json = resp.data!;
      return (
        token: json['token'] as String,
        veiculo: VeiculoVinculado.fromJson(json),
      );
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status == 404 || status == 400) throw const CodigoAtivacaoInvalido();
      rethrow;
    }
  }

  Future<VeiculoVinculado> eu(String token) async {
    try {
      final resp = await _dio.get<Map<String, dynamic>>(
        '/rastreamento/dispositivos/eu',
        options: Options(headers: <String, String>{_header: token}),
      );
      return VeiculoVinculado.fromJson(resp.data!);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const TokenDispositivoInvalido();
      rethrow;
    }
  }

  Future<void> enviarPontos(String token, List<PontoDispositivo> pontos) async {
    try {
      await _dio.post<void>(
        '/rastreamento/dispositivos/pontos',
        data: <String, dynamic>{
          'pontos': pontos.map((p) => p.toJson()).toList(),
        },
        options: Options(headers: <String, String>{_header: token}),
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const TokenDispositivoInvalido();
      rethrow;
    }
  }
}

final tokenDispositivoStoreProvider = Provider<TokenDispositivoStore>(
  (_) => const TokenDispositivoStore(
    FlutterSecureStorage(
      aOptions: AndroidOptions(encryptedSharedPreferences: true),
    ),
  ),
);

final dispositivoRepositoryProvider = Provider<DispositivoRepository>(
  (ref) => DispositivoRepository(ref.watch(dioProvider)),
);
