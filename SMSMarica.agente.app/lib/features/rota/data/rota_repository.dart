import 'package:agente/features/rota/domain/parada.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

class RotaRepository {
  const RotaRepository();

  Future<List<Parada>> obterRotaDoDia() async {
    await Future<void>.delayed(const Duration(milliseconds: 500));
    final hoje = DateTime.now();
    DateTime emHoras(int h, int m) =>
        DateTime(hoje.year, hoje.month, hoje.day, h, m);
    return [
      Parada(
        id: '1',
        ordem: 1,
        pacienteNome: 'Maria das Graças',
        endereco: 'Rua das Flores, 123 — Centro',
        horarioPrevisto: emHoras(7, 30),
        status: StatusParada.pendente,
      ),
      Parada(
        id: '2',
        ordem: 2,
        pacienteNome: 'João Silva',
        endereco: 'Av. Roberto Silveira, 456 — São José',
        horarioPrevisto: emHoras(7, 55),
        status: StatusParada.pendente,
      ),
      Parada(
        id: '3',
        ordem: 3,
        pacienteNome: 'Unidade — Hospital Municipal',
        endereco: 'R. Barão de Inoã, s/n — Centro',
        horarioPrevisto: emHoras(8, 30),
        status: StatusParada.pendente,
      ),
    ];
  }
}

final rotaRepositoryProvider = Provider<RotaRepository>(
  (_) => const RotaRepository(),
);

final rotaDoDiaProvider = FutureProvider<List<Parada>>((ref) {
  return ref.watch(rotaRepositoryProvider).obterRotaDoDia();
});
