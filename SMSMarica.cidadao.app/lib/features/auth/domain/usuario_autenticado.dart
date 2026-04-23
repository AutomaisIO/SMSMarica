import 'package:equatable/equatable.dart';

/// Identidade do cidadão/paciente autenticado no app.
///
/// Enquanto `Identidade` (S2.4) não estiver pronto, esta classe é populada
/// pelo fluxo de login mock. Depois de S2.4, a API devolverá o token JWT
/// e os dados do perfil junto ao login real.
class UsuarioAutenticado extends Equatable {
  const UsuarioAutenticado({
    required this.pacienteId,
    required this.nomeExibicao,
    required this.token,
  });

  /// Identificador do `Paciente` associado.
  final String pacienteId;

  /// Nome curto para exibição no cabeçalho do app.
  final String nomeExibicao;

  /// JWT recebido do `Identidade` (placeholder enquanto mock).
  final String token;

  @override
  List<Object?> get props => [pacienteId, nomeExibicao, token];
}
