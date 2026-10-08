import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// URL da API. Padrão = produção de Maricá; sobrescreva no build com
/// `--dart-define=SMSMAIS_API_BASE_URL=...` (outra instância ou ambiente
/// local).
const String urlBaseApi = String.fromEnvironment(
  'SMSMAIS_API_BASE_URL',
  defaultValue: 'https://api.smsmarica.online',
);

/// Cliente HTTP base. Interceptor de auth do motorista entra com o BE-4.
Dio criarDio() {
  final dio = Dio(
    BaseOptions(
      baseUrl: urlBaseApi,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 15),
      contentType: 'application/json',
    ),
  );
  return dio;
}

final dioProvider = Provider<Dio>((_) => criarDio());
