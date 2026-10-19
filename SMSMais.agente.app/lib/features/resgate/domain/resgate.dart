/// O resgate: buscar o paciente na unidade depois do atendimento e deixá-lo
/// em casa. A lista "Pacientes disponíveis para resgate" é a mesma para todos
/// os carros (`GET /rastreamento/dispositivos/resgates`): qualquer motorista
/// marca quem vai buscar, mesmo quem não levou, e só se couber no carro
/// naquela hora.
library;

/// Se o paciente já pode ser resgatado.
enum DisponibilidadeResgate {
  /// Avisaram que ele já está no local esperando (paciente, central ou
  /// motorista).
  noLocal('NoLocal'),

  /// Ninguém avisou, mas o tempo do procedimento já passou.
  pelaPrevisao('PelaPrevisao'),

  /// Ainda em atendimento, pela previsão.
  emAtendimento('EmAtendimento'),

  /// Sem previsão: só o aviso diz quando.
  semPrevisao('SemPrevisao');

  const DisponibilidadeResgate(this.nomeServidor);

  final String nomeServidor;

  static DisponibilidadeResgate doServidor(String? nome) => values.firstWhere(
    (d) => d.nomeServidor == nome,
    orElse: () => semPrevisao,
  );
}

/// Um paciente da lista de resgate. [cabe]: se cabe agora neste carro (com
/// os acompanhantes e a condição dele); [motivoNaoCabe] diz por quê.
class ResgateDisponivel {
  const ResgateDisponivel({
    required this.sessaoId,
    required this.pacienteNome,
    required this.unidadeNome,
    required this.disponibilidade,
    this.unidadeEndereco,
    this.foraDoMunicipio = false,
    this.acompanhantes = 0,
    this.cadeirante = false,
    this.isolamento = false,
    this.disponivelAs,
    this.disponivelEmMinutos,
    this.noLocalDesde,
    this.aguardandoMinutos,
    this.avisadoPor,
    this.naoEstavaNoLocalEm,
    this.aindaEmAtendimentoEm,
    this.veiculoDaIda,
    this.distanciaMetros,
    this.cabe,
    this.motivoNaoCabe,
  });

  factory ResgateDisponivel.fromJson(Map<String, dynamic> j) =>
      ResgateDisponivel(
        sessaoId: j['sessaoId'] as String,
        pacienteNome: j['pacienteNome'] as String? ?? 'Paciente',
        unidadeNome: j['unidadeNome'] as String? ?? '',
        unidadeEndereco: j['unidadeEndereco'] as String?,
        foraDoMunicipio: j['foraDoMunicipio'] as bool? ?? false,
        acompanhantes: (j['acompanhantes'] as num?)?.toInt() ?? 0,
        cadeirante: j['mobilidade'] == 'CadeiranteVeiculoAdaptado',
        isolamento: j['isolamento'] as bool? ?? false,
        disponibilidade: DisponibilidadeResgate.doServidor(
          j['disponibilidade'] as String?,
        ),
        disponivelAs: _hhmm(j['disponivelAs'] as String?),
        disponivelEmMinutos: (j['disponivelEmMinutos'] as num?)?.toInt(),
        noLocalDesde: _instante(j['noLocalDesde']),
        aguardandoMinutos: (j['aguardandoMinutos'] as num?)?.toInt(),
        avisadoPor: j['avisadoPor'] as String?,
        naoEstavaNoLocalEm: _instante(j['naoEstavaNoLocalEm']),
        aindaEmAtendimentoEm: _instante(j['aindaEmAtendimentoEm']),
        veiculoDaIda: j['veiculoDaIda'] as String?,
        distanciaMetros: (j['distanciaMetros'] as num?)?.toInt(),
        cabe: j['cabe'] as bool?,
        motivoNaoCabe: j['motivoNaoCabe'] as String?,
      );

  final String sessaoId;
  final String pacienteNome;
  final String unidadeNome;
  final String? unidadeEndereco;
  final bool foraDoMunicipio;
  final int acompanhantes;
  final bool cadeirante;
  final bool isolamento;
  final DisponibilidadeResgate disponibilidade;

  /// "HH:mm" pela previsão.
  final String? disponivelAs;
  final int? disponivelEmMinutos;
  final DateTime? noLocalDesde;

  /// Há quantos minutos espera no local (só quando está no local).
  final int? aguardandoMinutos;

  /// `Paciente`, `Central` ou `Motorista`.
  final String? avisadoPor;
  final DateTime? naoEstavaNoLocalEm;
  final DateTime? aindaEmAtendimentoEm;

  /// Placa do carro que levou na ida.
  final String? veiculoDaIda;
  final int? distanciaMetros;
  final bool? cabe;
  final String? motivoNaoCabe;

  /// O que a lista diz da disponibilidade (o mesmo texto do painel): de quem
  /// está no local, há quanto tempo espera.
  String get textoDisponibilidade => switch (disponibilidade) {
    DisponibilidadeResgate.noLocal =>
      aguardandoMinutos == null
          ? 'Aguardando no local'
          : 'Aguardando há ${duracao(aguardandoMinutos!)}',
    DisponibilidadeResgate.pelaPrevisao =>
      'Disponível pela previsão (${disponivelAs ?? '--:--'})',
    DisponibilidadeResgate.emAtendimento =>
      'Disponível em ${disponivelEmMinutos ?? '?'} min '
          '(às ${disponivelAs ?? '--:--'})',
    DisponibilidadeResgate.semPrevisao => 'Sem previsão: aguardando o aviso',
  };

  /// Quem avisou que ele está no local.
  String? get quemAvisou => switch (avisadoPor) {
    'Paciente' => 'avisado pelo paciente',
    'Central' => 'avisado pela central',
    'Motorista' => 'visto por um motorista',
    _ => null,
  };
}

/// Quem este carro já marcou e ainda não embarcou (pode desmarcar).
class ResgateMarcado {
  const ResgateMarcado({
    required this.sessaoId,
    required this.pacienteNome,
    required this.unidadeNome,
    this.acompanhantes = 0,
  });

  factory ResgateMarcado.fromJson(Map<String, dynamic> j) => ResgateMarcado(
    sessaoId: j['sessaoId'] as String,
    pacienteNome: j['pacienteNome'] as String? ?? 'Paciente',
    unidadeNome: j['unidadeNome'] as String? ?? '',
    acompanhantes: (j['acompanhantes'] as num?)?.toInt() ?? 0,
  );

  final String sessaoId;
  final String pacienteNome;
  final String unidadeNome;
  final int acompanhantes;
}

/// A lista de resgate para este carro: quem está disponível, quem ele já
/// marcou e os lugares livres agora.
class ResgatesDaRota {
  const ResgatesDaRota({
    this.disponiveis = const [],
    this.marcados = const [],
    this.lugaresLivres,
    this.lugaresCadeiranteLivres,
  });

  factory ResgatesDaRota.fromJson(Map<String, dynamic> j) => ResgatesDaRota(
    disponiveis: [
      for (final d in j['disponiveis'] as List<dynamic>? ?? const [])
        ResgateDisponivel.fromJson(d as Map<String, dynamic>),
    ],
    marcados: [
      for (final m in j['marcados'] as List<dynamic>? ?? const [])
        ResgateMarcado.fromJson(m as Map<String, dynamic>),
    ],
    lugaresLivres: (j['lugaresLivres'] as num?)?.toInt(),
    lugaresCadeiranteLivres: (j['lugaresCadeiranteLivres'] as num?)?.toInt(),
  );

  final List<ResgateDisponivel> disponiveis;
  final List<ResgateMarcado> marcados;
  final int? lugaresLivres;
  final int? lugaresCadeiranteLivres;
}

/// "25 min", "1 h 05".
String duracao(int minutos) => minutos < 60
    ? '$minutos min'
    : '${minutos ~/ 60} h ${(minutos % 60).toString().padLeft(2, '0')}';

/// "HH:mm" na hora local do tablet.
String hora(DateTime d) {
  final l = d.toLocal();
  return '${l.hour.toString().padLeft(2, '0')}:'
      '${l.minute.toString().padLeft(2, '0')}';
}

String? _hhmm(String? t) =>
    t == null || t.length < 5 ? null : t.substring(0, 5);

DateTime? _instante(Object? v) => v is String ? DateTime.tryParse(v) : null;
