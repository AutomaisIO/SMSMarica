/// Desafio de vivacidade (liveness) ativo, on-device.
///
/// Estratégia mínima e robusta para câmera fraca: pedir uma **piscada**
/// (olhos abertos → fechados → abertos) enquanto o rosto permanece centralizado
/// e frontal. Barra foto estática impressa sem custo de modelo extra.
enum EtapaLiveness {
  /// Aguardando um rosto bem posicionado e frontal.
  procurandoRosto,

  /// Rosto OK; pedindo para o usuário piscar.
  aguardandoPiscada,

  /// Piscada detectada; pronto para capturar o frame nítido.
  aprovado,
}

/// Máquina de estado da piscada. Considera "olho fechado" quando a
/// probabilidade de olho aberto cai abaixo de [limiarFechado] e "aberto"
/// quando volta acima de [limiarAberto] (histerese evita falso positivo).
class DesafioLiveness {
  DesafioLiveness({
    this.limiarAberto = 0.6,
    this.limiarFechado = 0.3,
  });

  final double limiarAberto;
  final double limiarFechado;

  EtapaLiveness _etapa = EtapaLiveness.procurandoRosto;
  bool _viuOlhosAbertos = false;
  bool _viuOlhosFechados = false;

  EtapaLiveness get etapa => _etapa;
  bool get aprovado => _etapa == EtapaLiveness.aprovado;

  /// Reinicia o desafio (ex.: rosto saiu de quadro).
  void reiniciar() {
    _etapa = EtapaLiveness.procurandoRosto;
    _viuOlhosAbertos = false;
    _viuOlhosFechados = false;
  }

  /// Alimenta uma medição de rosto. [rostoFrontalOk] indica rosto centralizado
  /// e frontal; [probOlhosAbertos] é a média das probabilidades dos dois olhos
  /// (ou `null` quando o classificador não retornou).
  EtapaLiveness avaliar({
    required bool rostoFrontalOk,
    required double? probOlhosAbertos,
  }) {
    if (!rostoFrontalOk) {
      reiniciar();
      return _etapa;
    }

    if (_etapa == EtapaLiveness.procurandoRosto) {
      _etapa = EtapaLiveness.aguardandoPiscada;
    }

    if (probOlhosAbertos == null) return _etapa;

    if (probOlhosAbertos >= limiarAberto) {
      if (_viuOlhosFechados && _viuOlhosAbertos) {
        _etapa = EtapaLiveness.aprovado;
      }
      _viuOlhosAbertos = true;
    } else if (probOlhosAbertos <= limiarFechado) {
      // Só conta o fechar depois de ter visto os olhos abertos uma vez.
      if (_viuOlhosAbertos) _viuOlhosFechados = true;
    }

    return _etapa;
  }
}
