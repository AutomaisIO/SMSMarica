import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_marica_cidadao/shared/api/api_config.dart';

final dioProvider = Provider<Dio>((ref) {
  final baseUrl = _resolverBaseUrl();

  final dio = Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(milliseconds: ApiConfig.timeoutMs),
      receiveTimeout: const Duration(milliseconds: ApiConfig.timeoutMs),
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
      },
    ),
  );

  if (kDebugMode) {
    dio.interceptors.add(
      LogInterceptor(
        requestBody: true,
        responseBody: true,
        responseHeader: false,
      ),
    );
  }

  return dio;
});

String _resolverBaseUrl() {
  if (kIsWeb) {
    return ApiConfig.baseUrlLocalhost;
  }
  if (Platform.isAndroid) {
    return ApiConfig.baseUrlAndroidEmulator;
  }
  return ApiConfig.baseUrlLocalhost;
}
