import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_marica_cidadao/features/auth/domain/usuario_autenticado.dart';

/// Contrato do repositório de autenticação.
///
/// Implementação real virá em C2 após S2.4 (Identidade) publicar o endpoint
/// de `POST /auth/login`. Até lá, usamos [AuthRepositoryMock].
abstract interface class AuthRepository {
  Future<UsuarioAutenticado> entrar({
    required String cpf,
    required String senha,
  });

  Future<void> sair();
}

/// Implementação mock usada enquanto o backend de Identidade não está pronto.
///
/// Aceita qualquer CPF com 11 dígitos + qualquer senha não-vazia.
class AuthRepositoryMock implements AuthRepository {
  @override
  Future<UsuarioAutenticado> entrar({
    required String cpf,
    required String senha,
  }) async {
    await Future<void>.delayed(const Duration(milliseconds: 700));

    final cpfDigitos = cpf.replaceAll(RegExp(r'\D'), '');
    if (cpfDigitos.length != 11) {
      throw const FalhaDeAutenticacao('CPF deve conter 11 dígitos.');
    }
    if (senha.isEmpty) {
      throw const FalhaDeAutenticacao('Informe sua senha.');
    }

    return UsuarioAutenticado(
      pacienteId: '00000000-0000-7000-0000-000000000001',
      nomeExibicao: 'Cidadão Maricaense',
      token: 'mock-token-${DateTime.now().millisecondsSinceEpoch}',
    );
  }

  @override
  Future<void> sair() async {
    await Future<void>.delayed(const Duration(milliseconds: 200));
  }
}

class FalhaDeAutenticacao implements Exception {
  const FalhaDeAutenticacao(this.mensagem);
  final String mensagem;

  @override
  String toString() => mensagem;
}

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  // Trocar por implementação real quando S2.4 (Identidade) estiver pronto.
  return AuthRepositoryMock();
});
