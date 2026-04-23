enum StatusParada { pendente, chegou, concluida }

class Parada {
  const Parada({
    required this.id,
    required this.ordem,
    required this.pacienteNome,
    required this.endereco,
    required this.horarioPrevisto,
    required this.status,
  });

  final String id;
  final int ordem;
  final String pacienteNome;
  final String endereco;
  final DateTime horarioPrevisto;
  final StatusParada status;
}
