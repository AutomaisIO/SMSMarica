import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/features/auth/presentation/auth_controller.dart';
import 'package:sms_mais_cidadao/features/perfil/data/perfil_repository.dart';
import 'package:sms_mais_cidadao/features/perfil/domain/paciente.dart';

/// Busca o perfil do usuário logado atualmente.
final perfilDoUsuarioProvider = FutureProvider<Paciente>((ref) async {
  final usuario = ref.watch(usuarioAtualProvider);
  if (usuario == null) {
    throw StateError('Sem usuário autenticado.');
  }
  final repo = ref.watch(perfilRepositoryProvider);
  return repo.obterPorId(usuario.pacienteId);
});
