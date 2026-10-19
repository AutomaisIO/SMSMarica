import 'package:agente/features/navegacao/domain/translado_navegacao.dart';

/// O que o tablet registra na viagem e manda à central
/// (`POST /rastreamento/dispositivos/viagem/eventos`). O que conta é o
/// transporte feito: o desembarque do paciente no destino marca a viagem como
/// realizada e a ausência na coleta, como não realizada; o painel corrige.
enum TipoEventoViagem {
  inicio('Inicio'),
  embarque('Embarque'),
  ausencia('Ausencia'),
  desembarque('Desembarque'),
  conclusao('Conclusao');

  const TipoEventoViagem(this.nomeServidor);

  final String nomeServidor;

  static TipoEventoViagem doServidor(String nome) =>
      values.firstWhere((t) => t.nomeServidor == nome);
}

enum PessoaViagem {
  paciente('Paciente'),
  acompanhante('Acompanhante');

  const PessoaViagem(this.nomeServidor);

  final String nomeServidor;

  static PessoaViagem? doServidor(String? nome) =>
      values.where((p) => p.nomeServidor == nome).firstOrNull;
}

/// Uma marcação esperando rede. [idLocal] faz o reenvio não duplicar.
class EventoViagemPendente {
  const EventoViagemPendente({
    required this.idLocal,
    required this.rotaId,
    required this.tipo,
    required this.ocorridoEm,
    this.sessaoId,
    this.pessoa,
    this.acompanhanteOrdem,
    this.acompanhanteId,
    this.motoristaId,
    this.latitude,
    this.longitude,
    this.precisaoM,
    this.resgate = false,
  });

  factory EventoViagemPendente.fromJson(Map<String, dynamic> j) =>
      EventoViagemPendente(
        idLocal: j['idLocal'] as String,
        rotaId: j['rotaId'] as String,
        tipo: TipoEventoViagem.doServidor(j['tipo'] as String),
        ocorridoEm: DateTime.parse(j['ocorridoEm'] as String),
        sessaoId: j['sessaoId'] as String?,
        pessoa: PessoaViagem.doServidor(j['pessoa'] as String?),
        acompanhanteOrdem: (j['acompanhanteOrdem'] as num?)?.toInt(),
        acompanhanteId: j['acompanhanteId'] as String?,
        motoristaId: j['motoristaId'] as String?,
        latitude: (j['latitude'] as num?)?.toDouble(),
        longitude: (j['longitude'] as num?)?.toDouble(),
        precisaoM: (j['precisaoM'] as num?)?.toDouble(),
        // `volta` é o nome antigo (fila gravada antes da troca para resgate).
        resgate: j['resgate'] as bool? ?? j['volta'] as bool? ?? false,
      );

  final String idLocal;
  final String rotaId;
  final TipoEventoViagem tipo;

  /// UTC.
  final DateTime ocorridoEm;
  final String? sessaoId;
  final PessoaViagem? pessoa;

  /// Posição do acompanhante na lista da sessão (1 ou 2).
  final int? acompanhanteOrdem;
  final String? acompanhanteId;

  /// Quem estava ao volante (o motorista conferido no tablet).
  final String? motoristaId;
  final double? latitude;
  final double? longitude;
  final double? precisaoM;

  /// Marcação do resgate (embarque na unidade, chegada em casa).
  final bool resgate;

  Map<String, dynamic> toJson() => {
    'idLocal': idLocal,
    'rotaId': rotaId,
    'tipo': tipo.nomeServidor,
    'ocorridoEm': ocorridoEm.toUtc().toIso8601String(),
    'sessaoId': sessaoId,
    'pessoa': pessoa?.nomeServidor,
    'acompanhanteOrdem': acompanhanteOrdem,
    'acompanhanteId': acompanhanteId,
    'motoristaId': motoristaId,
    'latitude': latitude,
    'longitude': longitude,
    'precisaoM': precisaoM,
    'resgate': resgate,
  };
}

/// Onde a viagem de uma rota está: paradas resolvidas, quem faltou, se já
/// começou e se acabou. Fica guardado no tablet e vem também da central
/// (o que ela já recebeu), para retomar depois de reiniciar.
class AndamentoViagem {
  const AndamentoViagem({
    required this.rotaId,
    this.concluidas = const {},
    this.ausentes = const {},
    this.iniciada = false,
    this.encerrada = false,
  });

  factory AndamentoViagem.fromJson(Map<String, dynamic> j) => AndamentoViagem(
    rotaId: j['rotaId'] as String,
    concluidas: {...(j['concluidas'] as List<dynamic>? ?? const []).cast()},
    ausentes: {...(j['ausentes'] as List<dynamic>? ?? const []).cast()},
    iniciada: j['iniciada'] as bool? ?? false,
    encerrada: j['encerrada'] as bool? ?? false,
  );

  final String rotaId;

  /// Ids de parada.
  final Set<String> concluidas;

  /// Ids de pessoa (paciente ou [PassageiroTranslado.idAcompanhante]).
  final Set<String> ausentes;
  final bool iniciada;
  final bool encerrada;

  /// O que um sabe e o outro não (tablet + central, da mesma rota).
  AndamentoViagem unir(AndamentoViagem? outro) =>
      outro == null || outro.rotaId != rotaId
      ? this
      : AndamentoViagem(
          rotaId: rotaId,
          concluidas: {...concluidas, ...outro.concluidas},
          ausentes: {...ausentes, ...outro.ausentes},
          iniciada: iniciada || outro.iniciada,
          encerrada: encerrada || outro.encerrada,
        );

  AndamentoViagem copyWith({
    Set<String>? concluidas,
    Set<String>? ausentes,
    bool? iniciada,
    bool? encerrada,
  }) => AndamentoViagem(
    rotaId: rotaId,
    concluidas: concluidas ?? this.concluidas,
    ausentes: ausentes ?? this.ausentes,
    iniciada: iniciada ?? this.iniciada,
    encerrada: encerrada ?? this.encerrada,
  );

  Map<String, dynamic> toJson() => {
    'rotaId': rotaId,
    'concluidas': [...concluidas],
    'ausentes': [...ausentes],
    'iniciada': iniciada,
    'encerrada': encerrada,
  };
}

/// As marcações de uma parada confirmada, para a central. [marcas] vem da
/// conferência (id de pessoa → embarcou/desembarcou). Na coleta, `false` é
/// ausência; no destino, só vai quem desceu.
List<EventoViagemPendente> eventosDaParada({
  required String rotaId,
  required ParadaNavegacao parada,
  required Map<String, bool> marcas,
  required String Function() novoId,
  required DateTime agora,
  String? motoristaId,
  double? latitude,
  double? longitude,
  double? precisaoM,
}) {
  final coleta = parada.tipo == TipoParada.coleta;
  EventoViagemPendente? evento(
    String sessaoId,
    PessoaViagem pessoa, {
    required bool? marca,
    int? ordem,
    String? acompanhanteId,
  }) {
    if (marca == null || (!coleta && !marca)) return null;
    return EventoViagemPendente(
      idLocal: novoId(),
      rotaId: rotaId,
      tipo: coleta
          ? (marca ? TipoEventoViagem.embarque : TipoEventoViagem.ausencia)
          : TipoEventoViagem.desembarque,
      ocorridoEm: agora.toUtc(),
      sessaoId: sessaoId,
      pessoa: pessoa,
      acompanhanteOrdem: ordem,
      acompanhanteId: acompanhanteId,
      motoristaId: motoristaId,
      latitude: latitude,
      longitude: longitude,
      precisaoM: precisaoM,
      resgate: parada.resgate,
    );
  }

  return [
    for (final p in parada.passageiros) ...[
      ?evento(p.id, PessoaViagem.paciente, marca: marcas[p.id]),
      for (var i = 0; i < p.acompanhantes.length; i++)
        ?evento(
          p.id,
          PessoaViagem.acompanhante,
          marca: marcas[p.idAcompanhante(i)],
          ordem: p.ordemDoAcompanhante(i),
          acompanhanteId: p.acompanhantes[i].id,
        ),
    ],
  ];
}
