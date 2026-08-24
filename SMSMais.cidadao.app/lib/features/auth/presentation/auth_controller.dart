import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/features/auth/data/auth_repository.dart';
import 'package:sms_mais_cidadao/features/auth/domain/usuario_autenticado.dart';

/// Estado atual da autenticação do app.
sealed class EstadoAutenticacao {
  const EstadoAutenticacao();
}

class AutenticacaoInicial extends EstadoAutenticacao {
  const AutenticacaoInicial();
}

class AutenticacaoCarregando extends EstadoAutenticacao {
  const AutenticacaoCarregando();
}

class AutenticacaoEntrada extends EstadoAutenticacao {
  const AutenticacaoEntrada(this.usuario);
  final UsuarioAutenticado usuario;
}

class AutenticacaoFalhou extends EstadoAutenticacao {
  const AutenticacaoFalhou(this.mensagem);
  final String mensagem;
}

class AuthController extends Notifier<EstadoAutenticacao> {
  @override
  EstadoAutenticacao build() => const AutenticacaoInicial();

  Future<void> entrar({required String cpf, required String senha}) async {
    state = const AutenticacaoCarregando();
    try {
      final usuario = await ref
          .read(authRepositoryProvider)
          .entrar(cpf: cpf, senha: senha);
      state = AutenticacaoEntrada(usuario);
    } on FalhaDeAutenticacao catch (e) {
      state = AutenticacaoFalhou(e.mensagem);
    } on Exception {
      state = const AutenticacaoFalhou('Falha ao entrar. Tente novamente.');
    }
  }

  Future<void> sair() async {
    await ref.read(authRepositoryProvider).sair();
    state = const AutenticacaoInicial();
  }
}

final authControllerProvider =
    NotifierProvider<AuthController, EstadoAutenticacao>(AuthController.new);

/// Projeção apenas do usuário autenticado (ou null se não estiver).
final usuarioAtualProvider = Provider<UsuarioAutenticado?>((ref) {
  final estado = ref.watch(authControllerProvider);
  return estado is AutenticacaoEntrada ? estado.usuario : null;
});
