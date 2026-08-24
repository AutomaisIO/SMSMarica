import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Cliente HTTP base. A URL definitiva virá do build-time (flavor) em A2.
/// Interceptor de auth e geração OpenAPI entram com S4.2 / A2.*.
Dio criarDio() {
  final dio = Dio(
    BaseOptions(
      baseUrl: const String.fromEnvironment(
        'SMSMAIS_API_BASE_URL',
        defaultValue: 'https://localhost:5001',
      ),
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 15),
      contentType: 'application/json',
    ),
  );
  return dio;
}

final dioProvider = Provider<Dio>((_) => criarDio());
