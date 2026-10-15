import 'dart:math';

/// Desafio de vivacidade (liveness) ativo, no próprio tablet.
///
/// Pede uma **piscada** e depois que o motorista **vire o rosto para um lado
/// sorteado** e volte de frente. Só a piscada, um vídeo da pessoa piscando
/// passaria; o lado sorteado obriga a reagir na hora. A foto que vai para o
/// reconhecimento é tirada no fim, com o rosto de frente.
enum EtapaLiveness {
  /// Aguardando um rosto bem posicionado e frontal.
  procurandoRosto,

  /// Rosto OK; pedindo para piscar.
  aguardandoPiscada,

  /// Piscou; pedindo para virar o rosto para [DesafioLiveness.lado].
  aguardandoVirar,

  /// Virou; pedindo para voltar de frente.
  aguardandoFrente,

  /// Tudo feito; pronto para capturar a foto.
  aprovado,
}

/// Para que lado o motorista deve virar o rosto (do ponto de vista DELE).
enum LadoVirar { esquerda, direita }

/// O que a máquina precisa saber de um quadro da câmera.
class MedidaRosto {
  const MedidaRosto({
    required this.quantidade,
    this.frontal = false,
    this.olhosAbertos,
    this.yaw,
    this.trackingId,
  });

  /// Quantos rostos o detector achou.
  final int quantidade;

  /// Um rosto só, de frente e perto o bastante.
  final bool frontal;

  /// Média da probabilidade de olhos abertos (null = o detector não disse).
  final double? olhosAbertos;

  /// Giro da cabeça no eixo vertical, em graus (ML Kit `headEulerAngleY`).
  final double? yaw;

  /// Identificador que o detector mantém enquanto é o mesmo rosto.
  final int? trackingId;
}

/// Máquina de estados do desafio. Olho "fechado" abaixo de [limiarFechado]
/// e "aberto" acima de [limiarAberto] (a histerese evita falso positivo).
/// Cada etapa tem [prazoEtapa]; estourou, recomeça e [expirou] fica true.
class DesafioLiveness {
  DesafioLiveness({
    this.limiarAberto = 0.6,
    this.limiarFechado = 0.3,
    this.grausVirar = 20,
    this.prazoEtapa = const Duration(seconds: 12),
    Random? sorteio,
    DateTime Function()? relogio,
  }) : _sorteio = sorteio ?? Random.secure(),
       _relogio = relogio ?? DateTime.now;

  final double limiarAberto;
  final double limiarFechado;

  /// Quanto o rosto precisa girar para contar como "virou".
  final double grausVirar;
  final Duration prazoEtapa;

  final Random _sorteio;
  final DateTime Function() _relogio;

  EtapaLiveness _etapa = EtapaLiveness.procurandoRosto;
  LadoVirar _lado = LadoVirar.esquerda;
  DateTime? _inicioEtapa;
  int? _trackingId;
  bool _viuOlhosAbertos = false;
  bool _viuOlhosFechados = false;
  bool _expirou = false;

  EtapaLiveness get etapa => _etapa;
  LadoVirar get lado => _lado;
  bool get aprovado => _etapa == EtapaLiveness.aprovado;

  /// A última tentativa recomeçou por demora (a tela pode avisar).
  bool get expirou => _expirou;

  /// No quadro cru da câmera frontal (sem espelho), virar para a esquerda
  /// DA PESSOA leva o nariz para a direita da imagem, que o ML Kit dá como
  /// yaw positivo.
  static bool virouPara(LadoVirar lado, double yaw, double graus) =>
      switch (lado) {
        LadoVirar.esquerda => yaw >= graus,
        LadoVirar.direita => yaw <= -graus,
      };

  void reiniciar() {
    _etapa = EtapaLiveness.procurandoRosto;
    _inicioEtapa = null;
    _trackingId = null;
    _viuOlhosAbertos = false;
    _viuOlhosFechados = false;
  }

  EtapaLiveness avaliar(MedidaRosto m) {
    if (_etapa == EtapaLiveness.aprovado) return _etapa;

    if (m.quantidade != 1 || _trocouDePessoa(m.trackingId)) {
      reiniciar();
      return _etapa;
    }
    if (_inicioEtapa != null &&
        _relogio().difference(_inicioEtapa!) > prazoEtapa) {
      reiniciar();
      _expirou = true;
      return _etapa;
    }

    switch (_etapa) {
      case EtapaLiveness.procurandoRosto:
        if (m.frontal) {
          _trackingId = m.trackingId;
          _avancar(EtapaLiveness.aguardandoPiscada);
        }
      case EtapaLiveness.aguardandoPiscada:
        if (!m.frontal) {
          reiniciar();
        } else if (_piscou(m.olhosAbertos)) {
          _lado = _sorteio.nextBool() ? LadoVirar.esquerda : LadoVirar.direita;
          _avancar(EtapaLiveness.aguardandoVirar);
        }
      case EtapaLiveness.aguardandoVirar:
        final yaw = m.yaw;
        if (yaw != null && virouPara(_lado, yaw, grausVirar)) {
          _avancar(EtapaLiveness.aguardandoFrente);
        }
      case EtapaLiveness.aguardandoFrente:
        if (m.frontal) _avancar(EtapaLiveness.aprovado);
      case EtapaLiveness.aprovado:
        break;
    }
    return _etapa;
  }

  void _avancar(EtapaLiveness proxima) {
    _etapa = proxima;
    _inicioEtapa = _relogio();
    _expirou = false;
  }

  bool _trocouDePessoa(int? trackingId) =>
      _trackingId != null && trackingId != null && trackingId != _trackingId;

  /// Aberto → fechado → aberto. Só conta o fechar depois de ver aberto.
  bool _piscou(double? prob) {
    if (prob == null) return false;
    if (prob >= limiarAberto) {
      if (_viuOlhosFechados && _viuOlhosAbertos) return true;
      _viuOlhosAbertos = true;
    } else if (prob <= limiarFechado && _viuOlhosAbertos) {
      _viuOlhosFechados = true;
    }
    return false;
  }
}
