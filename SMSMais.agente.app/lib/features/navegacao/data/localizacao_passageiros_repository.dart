import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/navegacao/domain/posicao_passageiro.dart';
import 'package:agente/shared/api/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// O servidor ainda não tem a localização dos passageiros (HTTP 404): o
/// tablet segue como antes, sem os pinos.
class LocalizacaoIndisponivel implements Exception {
  const LocalizacaoIndisponivel();
}

/// Quem das paradas deste carro compartilha a localização pelo app do cidadão
/// agora (ADR-0082), pelo token do veículo. Sem cache: a posição fica só em
/// memória e some quando o paciente embarca ou para.
class LocalizacaoPassageirosRepository {
  LocalizacaoPassageirosRepository(this._dio);

  final Dio _dio;

  Future<List<PosicaoPassageiro>> listar(String token) async {
    try {
      final r = await _dio.get<List<dynamic>>(
        '/rastreamento/dispositivos/localizacao-passageiros',
        options: Options(
          headers: <String, String>{'X-Dispositivo-Token': token},
        ),
      );
      return [
        for (final p in r.data ?? const <dynamic>[])
          PosicaoPassageiro.fromJson(p as Map<String, dynamic>),
      ];
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status == 404) throw const LocalizacaoIndisponivel();
      if (status == 401) throw const TokenDispositivoInvalido();
      rethrow;
    }
  }
}

final localizacaoPassageirosRepositoryProvider =
    Provider<LocalizacaoPassageirosRepository>(
      (ref) => LocalizacaoPassageirosRepository(ref.watch(dioProvider)),
    );
