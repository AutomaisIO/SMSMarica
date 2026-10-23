/// A posição que o paciente compartilha pelo app do cidadão enquanto espera o
/// embarque (ADR-0079). Fica só em memória: nunca entra no cache cifrado da
/// rota.
class PosicaoPassageiro {
  const PosicaoPassageiro({
    required this.sessaoId,
    required this.resgate,
    required this.latitude,
    required this.longitude,
    required this.capturadaEm,
    this.precisaoM,
    this.simulada = false,
  });

  factory PosicaoPassageiro.fromJson(Map<String, dynamic> j) =>
      PosicaoPassageiro(
        sessaoId: j['sessaoId'] as String,
        resgate: j['trecho'] == 'Resgate',
        latitude: (j['latitude'] as num).toDouble(),
        longitude: (j['longitude'] as num).toDouble(),
        precisaoM: (j['precisaoM'] as num?)?.toDouble(),
        capturadaEm: DateTime.parse(j['capturadaEm'] as String),
        simulada: j['simulada'] as bool? ?? false,
      );

  final String sessaoId;

  /// Espera o resgate (na unidade); falso = a ida (a busca em casa).
  final bool resgate;
  final double latitude;
  final double longitude;
  final double? precisaoM;
  final DateTime capturadaEm;

  /// O celular avisou que a posição veio de um app de GPS falso.
  final bool simulada;

  String get chave => chavePosicao(sessaoId, resgate: resgate);
}

/// A posição de um paciente numa parada de busca: a da ida e a do resgate são
/// esperas diferentes.
String chavePosicao(String sessaoId, {required bool resgate}) =>
    '$sessaoId:${resgate ? 'resgate' : 'ida'}';

/// "compartilhando · há 3 min · ±12 m" (e "posição simulada", se for o caso).
String descreverPosicao(PosicaoPassageiro p, {DateTime? agora}) {
  final min = (agora ?? DateTime.now()).difference(p.capturadaEm).inMinutes;
  final precisao = p.precisaoM;
  return [
    'compartilhando',
    if (min < 1) 'agora' else 'há $min min',
    if (precisao != null) '±${precisao.round()} m',
    if (p.simulada) 'posição simulada',
  ].join(' · ');
}
