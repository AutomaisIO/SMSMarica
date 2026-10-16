import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/rota/data/rota_repository.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Nenhuma rota hoje para este veículo (a central ainda não montou).
class SemTransladoHoje implements Exception {
  const SemTransladoHoje();

  @override
  String toString() =>
      'Nenhum translado hoje para este veículo. Confira com a central.';
}

/// Fonte do translado em navegação: a rota real de hoje do veículo
/// (Translados do painel), a mesma da tela Rota do dia.
class NavegacaoRepository {
  const NavegacaoRepository(this._ref);

  final Ref _ref;

  Future<TransladoNavegacao> obterTransladoAtual() async {
    final rota = await _ref.read(rotaAtualProvider.future);
    if (rota == null) throw const SemTransladoHoje();
    return rota.translado;
  }
}

final navegacaoRepositoryProvider = Provider<NavegacaoRepository>(
  NavegacaoRepository.new,
);
