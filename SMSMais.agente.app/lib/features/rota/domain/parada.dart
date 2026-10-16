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
  /// Nulo quando a central não definiu o horário.
  final DateTime? horarioPrevisto;
  final StatusParada status;
}
