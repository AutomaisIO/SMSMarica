/// Modelo de domínio da navegação assistida do motorista (TFD).
///
/// Um **translado** é lançado no Navigation SDK como **uma única rota** com
/// várias **paradas (waypoints)**: as de *coleta* (pegar pacientes) e as de
/// *destino* (deixar pacientes). A rota já vem **otimizada pelo Google**
/// (menor percurso / mais eficiente) — ver
/// `docs/modulos/tfd/navegacao-motorista.md`.
/// Uma rota = uma sessão cobrada; por isso evitamos recalcular à toa.
library;

/// O que a parada representa no fluxo do translado.
enum TipoParada {
  /// Embarque — buscar o(s) paciente(s) no endereço.
  coleta,

  /// Desembarque — deixar o(s) paciente(s) no destino (unidade/hospital).
  destino,
}

/// Estado da confirmação de uma pessoa (paciente ou acompanhante).
enum StatusConfirmacao {
  /// Ainda não respondeu / motorista ainda não conferiu.
  pendente,

  /// Confirmou presença (pelo app do cidadão ou conferido na parada).
  confirmado,

  /// Marcado como ausente / não embarcou.
  ausente,
}

/// Acompanhante alocado ao paciente no planejamento.
class Acompanhante {
  const Acompanhante({
    required this.nome,
    required this.confirmacao,
    this.assento,
  });

  final String nome;
  final StatusConfirmacao confirmacao;

  /// Assento alocado no planejamento (ex.: "Banco 2 — janela").
  final String? assento;
}

/// Mensagem trocada entre paciente e motorista (dentro do app).
class MensagemTranslado {
  const MensagemTranslado({
    required this.texto,
    required this.quando,
    required this.doMotorista,
  });

  final String texto;
  final DateTime quando;

  /// `true` = enviada pelo motorista; `false` = enviada pelo paciente.
  final bool doMotorista;
}

/// Paciente embarcado/desembarcado numa parada.
class PassageiroTranslado {
  const PassageiroTranslado({
    required this.id,
    required this.nome,
    required this.confirmacao,
    this.telefone,
    this.observacao,
    this.assento,
    this.acompanhantes = const [],
    this.mensagens = const [],
  });

  final String id;
  final String nome;
  final StatusConfirmacao confirmacao;
  final String? telefone;

  /// Necessidades/observações relevantes ao motorista (ex.: "cadeirante").
  final String? observacao;

  /// Assento do paciente, conforme alocado no planejamento.
  final String? assento;

  /// Quem acompanha o paciente nesta sessão (até 2, pela regra do
  /// atendimento), cada um com o seu assento.
  final List<Acompanhante> acompanhantes;

  /// Conversa objetiva paciente ↔ motorista.
  final List<MensagemTranslado> mensagens;

  bool get temAcompanhante => acompanhantes.isNotEmpty;

  /// Id do [i]-ésimo acompanhante deste paciente na conferência da parada
  /// (o primeiro é `<paciente>:acompanhante`).
  String idAcompanhante([int i = 0]) =>
      i == 0 ? '$id:acompanhante' : '$id:acompanhante:${i + 1}';

  int get mensagensNaoLidas =>
      mensagens.where((m) => !m.doMotorista).length;

  /// O mesmo paciente, sem os acompanhantes que faltaram na coleta
  /// ([ausentes] são ids de pessoa, como em [idAcompanhante]).
  PassageiroTranslado semAusentes(Set<String> ausentes) => PassageiroTranslado(
        id: id,
        nome: nome,
        confirmacao: confirmacao,
        telefone: telefone,
        observacao: observacao,
        assento: assento,
        acompanhantes: [
          for (var i = 0; i < acompanhantes.length; i++)
            if (!ausentes.contains(idAcompanhante(i))) acompanhantes[i],
        ],
        mensagens: mensagens,
      );
}

/// Uma parada (waypoint) da rota otimizada.
class ParadaNavegacao {
  const ParadaNavegacao({
    required this.id,
    required this.ordem,
    required this.tipo,
    required this.rotulo,
    required this.endereco,
    required this.latitude,
    required this.longitude,
    required this.horarioPrevisto,
    this.observacoes,
    this.passageiros = const [],
    this.concluida = false,
  });

  final String id;

  /// Ordem otimizada pelo Google dentro do translado.
  final int ordem;
  final TipoParada tipo;

  /// Nome do paciente (coleta) ou da unidade/destino (destino).
  final String rotulo;
  final String endereco;

  /// Ponto no mapa; nulo quando o endereço não foi localizado (a navegação
  /// pula a parada e o motorista segue pelo endereço).
  final double? latitude;
  final double? longitude;

  /// Horário previsto, quando a central definiu.
  final DateTime? horarioPrevisto;

  /// Para o motorista: ponto de referência (coleta) ou orientação de chegada
  /// da unidade (destino).
  final String? observacoes;

  bool get temPonto => latitude != null && longitude != null;

  /// Quem embarca (coleta) ou desembarca (destino) nesta parada.
  final List<PassageiroTranslado> passageiros;

  final bool concluida;

  ParadaNavegacao copyWith({
    bool? concluida,
    List<PassageiroTranslado>? passageiros,
  }) =>
      ParadaNavegacao(
        id: id,
        ordem: ordem,
        tipo: tipo,
        rotulo: rotulo,
        endereco: endereco,
        latitude: latitude,
        longitude: longitude,
        horarioPrevisto: horarioPrevisto,
        observacoes: observacoes,
        passageiros: passageiros ?? this.passageiros,
        concluida: concluida ?? this.concluida,
      );
}

/// O translado inteiro = uma rota com paradas, lançado em uma só sessão do SDK.
class TransladoNavegacao {
  const TransladoNavegacao({
    required this.id,
    required this.veiculo,
    required this.paradas,
  });

  final String id;
  final String veiculo;
  final List<ParadaNavegacao> paradas;

  /// Próxima parada não concluída (destino atual da navegação).
  ParadaNavegacao? get proximaParada {
    for (final p in paradas) {
      if (!p.concluida) return p;
    }
    return null;
  }

  /// Paradas com o andamento aplicado: [concluidas] (ids de parada) marcadas
  /// e, no destino, só desembarca quem embarcou — [ausentes] são ids de
  /// pessoa (paciente ou [PassageiroTranslado.idAcompanhante]) que faltaram
  /// na coleta. Paciente ausente sai do destino junto com os acompanhantes.
  /// Destino sem ninguém para desembarcar fica **dispensado** (conta como
  /// concluído e sai da rota).
  List<ParadaNavegacao> paradasComAndamento({
    required Set<String> concluidas,
    required Set<String> ausentes,
  }) => [
        for (final p in paradas)
          if (p.tipo == TipoParada.destino)
            _destinoSemAusentes(p, concluidas, ausentes)
          else
            p.copyWith(concluida: concluidas.contains(p.id)),
      ];

  static ParadaNavegacao _destinoSemAusentes(
    ParadaNavegacao parada,
    Set<String> concluidas,
    Set<String> ausentes,
  ) {
    final quemDesce = [
      for (final p in parada.passageiros)
        if (!ausentes.contains(p.id))
          p.semAusentes(ausentes),
    ];
    return parada.copyWith(
      passageiros: quemDesce,
      concluida: concluidas.contains(parada.id) || quemDesce.isEmpty,
    );
  }
}
