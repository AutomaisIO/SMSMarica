import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Estado de autenticação do motorista.
/// Mockado para A1; em A2 o repositório real substitui o stub.
class Sessao {
  const Sessao({required this.motoristaId, required this.nome});

  final String motoristaId;
  final String nome;
}

class SessaoController extends StateNotifier<Sessao?> {
  SessaoController() : super(null);

  Future<void> entrar({required String usuario, required String senha}) async {
    await Future<void>.delayed(const Duration(milliseconds: 400));
    state = Sessao(motoristaId: 'mock-001', nome: usuario);
  }

  void sair() {
    state = null;
  }
}

final sessaoProvider = StateNotifierProvider<SessaoController, Sessao?>(
  (ref) => SessaoController(),
);
