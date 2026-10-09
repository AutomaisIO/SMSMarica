import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';

/// Perfil do cidadão logado (`store/perfil.ts` do PWA): carregado ao entrar, compartilhado pelo
/// cabeçalho, menu, Início e Meu perfil. Recarrega sozinho quando a sessão muda.
class PerfilController extends AsyncNotifier<Perfil> {
  @override
  Future<Perfil> build() => ref.watch(apiProvider).perfil();

  /// Atualização local depois de salvar (foto, contatos) — sem nova ida à rede.
  void definir(Perfil perfil) => state = AsyncData(perfil);

  Future<void> recarregar() async {
    state = await AsyncValue.guard(() => ref.read(apiProvider).perfil());
  }
}

final perfilProvider = AsyncNotifierProvider<PerfilController, Perfil>(PerfilController.new);
