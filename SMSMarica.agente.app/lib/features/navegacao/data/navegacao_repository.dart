import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Fonte do translado em navegação.
///
/// Mock por enquanto (A1). Em A2 o repositório real busca
/// `GET /rotas/{id}` (com alocações + assentos + confirmações) e dispara a
/// otimização da rota pelo backend (Routes API) antes de lançar no
/// Navigation SDK.
class NavegacaoRepository {
  const NavegacaoRepository();

  Future<TransladoNavegacao> obterTransladoAtual() async {
    await Future<void>.delayed(const Duration(milliseconds: 400));
    final hoje = DateTime.now();
    DateTime hora(int h, int m) =>
        DateTime(hoje.year, hoje.month, hoje.day, h, m);

    return TransladoNavegacao(
      id: 'transl-001',
      veiculo: 'Van — Placa ABC-1D23',
      paradas: [
        ParadaNavegacao(
          id: 'p1',
          ordem: 1,
          tipo: TipoParada.coleta,
          rotulo: 'Maria das Graças',
          endereco: 'Rua das Flores, 123 — Centro',
          latitude: -22.9186,
          longitude: -42.8186,
          horarioPrevisto: hora(7, 30),
          passageiros: [
            PassageiroTranslado(
              id: 'pac-1',
              nome: 'Maria das Graças',
              telefone: '+55 21 99999-0001',
              observacao: 'Cadeirante — precisa de rampa.',
              confirmacao: StatusConfirmacao.confirmado,
              assento: 'Banco 1 — porta',
              acompanhante: const Acompanhante(
                nome: 'José (filho)',
                confirmacao: StatusConfirmacao.confirmado,
                assento: 'Banco 1 — meio',
              ),
              mensagens: [
                MensagemTranslado(
                  texto: 'Estou pronta na portaria, pode chegar.',
                  quando: hora(7, 10),
                  doMotorista: false,
                ),
              ],
            ),
          ],
        ),
        ParadaNavegacao(
          id: 'p2',
          ordem: 2,
          tipo: TipoParada.coleta,
          rotulo: 'João Silva',
          endereco: 'Av. Roberto Silveira, 456 — São José',
          latitude: -22.9290,
          longitude: -42.8270,
          horarioPrevisto: hora(7, 55),
          passageiros: const [
            PassageiroTranslado(
              id: 'pac-2',
              nome: 'João Silva',
              telefone: '+55 21 99999-0002',
              confirmacao: StatusConfirmacao.pendente,
              assento: 'Banco 2 — janela',
            ),
          ],
        ),
        ParadaNavegacao(
          id: 'p3',
          ordem: 3,
          tipo: TipoParada.destino,
          rotulo: 'Hospital Municipal de Maricá',
          endereco: 'R. Barão de Inoã, s/n — Centro',
          latitude: -22.9215,
          longitude: -42.8190,
          horarioPrevisto: hora(8, 30),
          passageiros: const [
            PassageiroTranslado(
              id: 'pac-1',
              nome: 'Maria das Graças',
              confirmacao: StatusConfirmacao.confirmado,
              assento: 'Banco 1 — porta',
              acompanhante: Acompanhante(
                nome: 'José (filho)',
                confirmacao: StatusConfirmacao.confirmado,
              ),
            ),
            PassageiroTranslado(
              id: 'pac-2',
              nome: 'João Silva',
              confirmacao: StatusConfirmacao.pendente,
              assento: 'Banco 2 — janela',
            ),
          ],
        ),
      ],
    );
  }
}

final navegacaoRepositoryProvider = Provider<NavegacaoRepository>(
  (_) => const NavegacaoRepository(),
);

final transladoAtualProvider = FutureProvider<TransladoNavegacao>((ref) {
  return ref.watch(navegacaoRepositoryProvider).obterTransladoAtual();
});
