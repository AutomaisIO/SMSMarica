import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/features/perfil/domain/paciente.dart';
import 'package:sms_mais_cidadao/shared/api/dio_provider.dart';

/// Consulta dados do paciente na API `SMSMais.server`.
///
/// Endpoint: `GET /pacientes/{id}` (ver `PacientesApiModule`).
class PerfilRepository {
  PerfilRepository(this._dio);
  final Dio _dio;

  Future<Paciente> obterPorId(String pacienteId) async {
    try {
      final resposta = await _dio.get<Map<String, dynamic>>(
        '/pacientes/$pacienteId',
      );
      final dados = resposta.data;
      if (dados == null) {
        throw const FalhaAoCarregarPerfil('Resposta vazia do servidor.');
      }
      return Paciente.fromJson(dados);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) {
        throw const FalhaAoCarregarPerfil(
          'Paciente não encontrado. Verifique seu cadastro.',
        );
      }
      throw FalhaAoCarregarPerfil(
        'Falha ao comunicar com a Secretaria de Saúde: ${e.message ?? "erro desconhecido"}',
      );
    }
  }
}

class FalhaAoCarregarPerfil implements Exception {
  const FalhaAoCarregarPerfil(this.mensagem);
  final String mensagem;

  @override
  String toString() => mensagem;
}

final perfilRepositoryProvider = Provider<PerfilRepository>((ref) {
  return PerfilRepository(ref.watch(dioProvider));
});
