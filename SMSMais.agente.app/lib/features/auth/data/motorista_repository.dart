import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/shared/api/dio_client.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// O servidor respondeu e recusou, com uma mensagem para o motorista
/// (código errado, vencido, termo mudou...).
class RecusaDaCentral implements Exception {
  const RecusaDaCentral(this.mensagem);

  final String mensagem;

  @override
  String toString() => mensagem;
}

/// Login do motorista no tablet (ADR-0073). Tudo pelo token do veículo
/// (`X-Dispositivo-Token`), como o REPORT e as posições.
class MotoristaRepository {
  MotoristaRepository(this._dio);

  final Dio _dio;

  static const _header = 'X-Dispositivo-Token';
  static const _base = '/rastreamento/dispositivos';

  Options _token(String token) =>
      Options(headers: <String, String>{_header: token});

  Future<List<MotoristaEscalado>> escaladosDoDia(String token) =>
      _chamar(() async {
        final r = await _dio.get<List<dynamic>>(
          '$_base/motoristas-do-dia',
          options: _token(token),
        );
        return [
          for (final e in r.data ?? const <dynamic>[])
            MotoristaEscalado.fromJson(e as Map<String, dynamic>),
        ];
      });

  /// Foto de referência (JPEG base64) e o hash dela.
  Future<({String fotoBase64, String fotoHash})> referencia(
    String token,
    String motoristaId,
  ) => _chamar(() async {
    final r = await _dio.get<Map<String, dynamic>>(
      '$_base/motoristas/$motoristaId/referencia-facial',
      options: _token(token),
    );
    return (
      fotoBase64: r.data!['fotoBase64'] as String,
      fotoHash: r.data!['fotoHash'] as String,
    );
  });

  Future<CodigoEmitido> solicitarCodigo(String token, String cpf) =>
      _chamar(() async {
        final r = await _dio.post<Map<String, dynamic>>(
          '$_base/primeiro-acesso/codigo',
          data: {'cpf': cpf},
          options: _token(token),
        );
        return CodigoEmitido.fromJson(r.data!);
      });

  Future<PrimeiroAcessoLiberado> confirmarCodigo(
    String token,
    String cpf,
    String codigo,
  ) => _chamar(() async {
    final r = await _dio.post<Map<String, dynamic>>(
      '$_base/primeiro-acesso/confirmar',
      data: {'cpf': cpf, 'codigo': codigo},
      options: _token(token),
    );
    return PrimeiroAcessoLiberado.fromJson(r.data!);
  });

  /// Devolve o hash da foto gravada.
  Future<String> cadastrarRosto(
    String token, {
    required String ticket,
    required String fotoBase64,
    required String termoVersao,
    double? latitude,
    double? longitude,
  }) => _chamar(() async {
    final r = await _dio.post<Map<String, dynamic>>(
      '$_base/primeiro-acesso/rosto',
      data: {
        'ticket': ticket,
        'fotoBase64': fotoBase64,
        'termoVersao': termoVersao,
        'latitude': latitude,
        'longitude': longitude,
      },
      options: _token(token),
    );
    return r.data!['fotoHash'] as String;
  });

  Future<void> registrarConferencia(String token, ConferenciaPendente c) =>
      _chamar(
        () => _dio.post<void>(
          '$_base/verificacoes-faciais',
          data: c.toJson(),
          options: _token(token),
        ),
      );

  /// Entrada pelo código da central: devolve quem ela liberou.
  Future<({String motoristaId, String nome})> usarLiberacao(
    String token, {
    required String idLocal,
    required String codigo,
    double? latitude,
    double? longitude,
  }) => _chamar(() async {
    final r = await _dio.post<Map<String, dynamic>>(
      '$_base/liberacao',
      data: {
        'idLocal': idLocal,
        'codigo': codigo,
        'latitude': latitude,
        'longitude': longitude,
      },
      options: _token(token),
    );
    return (
      motoristaId: r.data!['motoristaId'] as String,
      nome: r.data!['nome'] as String,
    );
  });

  /// 401 vira [TokenDispositivoInvalido]; 4xx com mensagem vira
  /// [RecusaDaCentral]; o resto (sem rede, 5xx) sobe como veio.
  Future<T> _chamar<T>(Future<T> Function() chamada) async {
    try {
      return await chamada();
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status == 401) throw const TokenDispositivoInvalido();
      final passageira = status == 408 || status == 429;
      if (status != null && status >= 400 && status < 500 && !passageira) {
        throw RecusaDaCentral(mensagemDoServidor(e.response?.data));
      }
      rethrow;
    }
  }

  /// Primeira mensagem de `errors` (validação) ou `detail` (ProblemDetails).
  static String mensagemDoServidor(Object? corpo) {
    if (corpo is Map) {
      final erros = corpo['errors'];
      if (erros is Map) {
        for (final v in erros.values) {
          if (v is List && v.isNotEmpty) return v.first.toString();
        }
      }
      final detalhe = corpo['detail'];
      if (detalhe is String && detalhe.isNotEmpty) return detalhe;
    }
    return 'A central não aceitou agora. Tente de novo ou peça à central.';
  }
}

final motoristaRepositoryProvider = Provider<MotoristaRepository>(
  (ref) => MotoristaRepository(ref.watch(dioProvider)),
);
