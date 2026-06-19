import 'package:agente/shared/api/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Envia pontos de GPS do motorista para o backend (`POST /rastreamento/pontos`).
class RastreamentoRepository {
  RastreamentoRepository(this._dio);

  final Dio _dio;

  Future<void> enviarPonto({
    required String motoristaId,
    required double latitude,
    required double longitude,
    required DateTime capturadoEm,
  }) async {
    await _dio.post<dynamic>(
      '/rastreamento/pontos',
      data: <String, dynamic>{
        'motoristaId': motoristaId,
        'latitude': latitude,
        'longitude': longitude,
        'capturadoEm': capturadoEm.toUtc().toIso8601String(),
      },
    );
  }
}

final rastreamentoRepositoryProvider = Provider<RastreamentoRepository>(
  (ref) => RastreamentoRepository(ref.watch(dioProvider)),
);
