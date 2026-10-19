import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/resgate/domain/resgate.dart';
import 'package:agente/shared/api/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// A lista de resgate e a marcação, pelo token do veículo. Precisa de rede:
/// a lista é a mesma para todos os carros e quem marca primeiro fica com o
/// paciente — não dá para marcar sem a central.
class ResgateRepository {
  ResgateRepository(this._dio);

  final Dio _dio;

  Options _token(String token) =>
      Options(headers: <String, String>{'X-Dispositivo-Token': token});

  Future<ResgatesDaRota> listar(String token, String rotaId) =>
      _chamar(() async {
        final r = await _dio.get<Map<String, dynamic>>(
          '/rastreamento/dispositivos/resgates',
          queryParameters: {'rotaId': rotaId},
          options: _token(token),
        );
        return ResgatesDaRota.fromJson(r.data ?? const {});
      });

  /// 409 (não cabe agora, outro carro marcou antes) → [RecusaDaCentral] com
  /// a mensagem da central.
  Future<void> marcar(
    String token,
    String rotaId,
    List<String> sessaoIds, {
    String? motoristaId,
  }) => _chamar(
    () => _dio.post<void>(
      '/rastreamento/dispositivos/resgates',
      data: {
        'rotaId': rotaId,
        'sessaoIds': sessaoIds,
        'motoristaId': motoristaId,
      },
      options: _token(token),
    ),
  );

  Future<void> desmarcar(String token, String rotaId, String sessaoId) =>
      _chamar(
        () => _dio.delete<void>(
          '/rastreamento/dispositivos/resgates/$sessaoId',
          queryParameters: {'rotaId': rotaId},
          options: _token(token),
        ),
      );

  /// O motorista viu o paciente esperando no local: vale para todos os carros.
  Future<void> avisarNoLocal(String token, String sessaoId) => _chamar(
    () => _dio.post<void>(
      '/rastreamento/dispositivos/resgates/$sessaoId/no-local',
      options: _token(token),
    ),
  );

  Future<T> _chamar<T>(Future<T> Function() chamada) async {
    try {
      return await chamada();
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status == 401) throw const TokenDispositivoInvalido();
      if (status != null && status >= 400 && status < 500) {
        throw RecusaDaCentral(
          MotoristaRepository.mensagemDoServidor(e.response?.data),
        );
      }
      rethrow;
    }
  }
}

final resgateRepositoryProvider = Provider<ResgateRepository>(
  (ref) => ResgateRepository(ref.watch(dioProvider)),
);
