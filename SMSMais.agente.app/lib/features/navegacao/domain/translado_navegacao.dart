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
    this.acompanhante,
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

  final Acompanhante? acompanhante;

  /// Conversa objetiva paciente ↔ motorista.
  final List<MensagemTranslado> mensagens;

  bool get temAcompanhante => acompanhante != null;

  /// Id da pessoa "acompanhante deste paciente" na conferência da parada.
  String get idAcompanhante => '$id:acompanhante';

  int get mensagensNaoLidas =>
      mensagens.where((m) => !m.doMotorista).length;

  /// O mesmo paciente, sem o acompanhante (que faltou na coleta).
  PassageiroTranslado semAcompanhante() => PassageiroTranslado(
        id: id,
        nome: nome,
        confirmacao: confirmacao,
        telefone: telefone,
        observacao: observacao,
        assento: assento,
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
  final double latitude;
  final double longitude;
  final DateTime horarioPrevisto;

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
  /// na coleta. Paciente ausente sai do destino junto com o acompanhante.
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
          ausentes.contains(p.idAcompanhante) ? p.semAcompanhante() : p,
    ];
    return parada.copyWith(
      passageiros: quemDesce,
      concluida: concluidas.contains(parada.id) || quemDesce.isEmpty,
    );
  }
}
