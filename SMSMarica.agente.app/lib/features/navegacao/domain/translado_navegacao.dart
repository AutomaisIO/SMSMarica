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

  int get mensagensNaoLidas =>
      mensagens.where((m) => !m.doMotorista).length;
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

  ParadaNavegacao copyWith({bool? concluida}) => ParadaNavegacao(
        id: id,
        ordem: ordem,
        tipo: tipo,
        rotulo: rotulo,
        endereco: endereco,
        latitude: latitude,
        longitude: longitude,
        horarioPrevisto: horarioPrevisto,
        passageiros: passageiros,
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
}
