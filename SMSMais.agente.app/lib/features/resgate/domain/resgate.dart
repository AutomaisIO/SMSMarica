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
    this.maca = false,
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
        maca: j['mobilidade'] == 'Maca',
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
  final bool maca;
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
    this.carro,
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
    carro: j['carro'] is Map<String, dynamic>
        ? CarroAgora.fromJson(j['carro'] as Map<String, dynamic>)
        : null,
  );

  final List<ResgateDisponivel> disponiveis;
  final List<ResgateMarcado> marcados;
  final int? lugaresLivres;
  final int? lugaresCadeiranteLivres;

  /// O carro agora, para refazer a conta a cada paciente escolhido.
  final CarroAgora? carro;
}

/// Lugares livres do carro por tipo e quem já está nele. A conta é a mesma
/// da central (`AcomodacaoNoVeiculo`): cadeirante só em assento de
/// cadeirante; maca só em ambulância, uma por vez; isolamento não divide o
/// carro; cada acompanhante leva um lugar. A central confere de novo ao
/// marcar.
class CarroAgora {
  const CarroAgora({
    this.passageiro = 0,
    this.acompanhante = 0,
    this.cadeirante = 0,
    this.ambulancia = false,
    this.pacientesABordo = 0,
    this.isolamentoABordo = false,
    this.macaABordo = false,
  });

  factory CarroAgora.fromJson(Map<String, dynamic> j) => CarroAgora(
    passageiro: (j['passageiro'] as num?)?.toInt() ?? 0,
    acompanhante: (j['acompanhante'] as num?)?.toInt() ?? 0,
    cadeirante: (j['cadeirante'] as num?)?.toInt() ?? 0,
    ambulancia: j['ambulancia'] as bool? ?? false,
    pacientesABordo: (j['pacientesABordo'] as num?)?.toInt() ?? 0,
    isolamentoABordo: j['isolamentoABordo'] as bool? ?? false,
    macaABordo: j['macaABordo'] as bool? ?? false,
  );

  final int passageiro;
  final int acompanhante;
  final int cadeirante;
  final bool ambulancia;
  final int pacientesABordo;
  final bool isolamentoABordo;
  final bool macaABordo;

  int get livres => passageiro + acompanhante + cadeirante;

  /// O que sobra depois de levar [quem] (o paciente e os acompanhantes de
  /// cada um); nulo quando não cabem todos juntos.
  CarroAgora? levando(List<ResgateDisponivel> quem) {
    if (pacientesABordo + quem.length > 1 &&
        (isolamentoABordo || quem.any((q) => q.isolamento))) {
      return null;
    }
    final macas = (macaABordo ? 1 : 0) + quem.where((q) => q.maca).length;
    if (macas > (ambulancia ? 1 : 0)) return null;

    var cad = cadeirante;
    var pas = passageiro;
    var aco = acompanhante;
    // Primeiro quem tem lugar certo (cadeira, maca); o banco comum fica para
    // os outros. Acompanhante: o de acompanhante e, sem ele, o de passageiro.
    for (final _ in quem.where((q) => q.cadeirante)) {
      if (cad == 0) return null;
      cad--;
    }
    for (final _ in quem.where((q) => q.maca)) {
      if (cad > 0) {
        cad--;
      } else if (pas > 0) {
        pas--;
      } else {
        return null;
      }
    }
    for (final _ in quem.where((q) => !q.cadeirante && !q.maca)) {
      if (pas > 0) {
        pas--;
      } else if (aco > 0) {
        aco--;
      } else {
        return null;
      }
    }
    for (var i = 0; i < quem.fold(0, (n, q) => n + q.acompanhantes); i++) {
      if (aco > 0) {
        aco--;
      } else if (pas > 0) {
        pas--;
      } else {
        return null;
      }
    }
    return CarroAgora(
      passageiro: pas,
      acompanhante: aco,
      cadeirante: cad,
      ambulancia: ambulancia,
      pacientesABordo: pacientesABordo + quem.length,
      isolamentoABordo: isolamentoABordo || quem.any((q) => q.isolamento),
      macaABordo: macaABordo || quem.any((q) => q.maca),
    );
  }

  /// Por que [p] não cabe junto com quem já foi escolhido ([escolhidos]).
  String motivoNaoCabe(
    ResgateDisponivel p,
    List<ResgateDisponivel> escolhidos,
  ) {
    final sobra = levando(escolhidos) ?? this;
    if (sobra.isolamentoABordo) {
      return 'Há um paciente em isolamento no carro (ou escolhido): ninguém '
          'mais embarca com ele.';
    }
    if (p.isolamento && sobra.pacientesABordo > 0) {
      return 'Paciente em isolamento só viaja sozinho (com os acompanhantes '
          'dele).';
    }
    if (p.maca) {
      return ambulancia
          ? 'A ambulância já leva um paciente de maca.'
          : 'Paciente de maca só vai de ambulância.';
    }
    if (p.cadeirante && sobra.cadeirante == 0) {
      return 'Não sobra assento de cadeirante neste carro.';
    }
    final pessoas = 1 + p.acompanhantes;
    final livres = sobra.livres;
    return livres == 0
        ? 'O carro fica cheio com quem já foi escolhido.'
        : 'Não cabe: ${pessoas == 1 ? '1 pessoa' : '$pessoas pessoas'} para '
              '${livres == 1 ? '1 lugar livre' : '$livres lugares livres'}.';
  }
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
