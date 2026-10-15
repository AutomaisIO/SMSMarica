import 'dart:math';

/// Motorista escalado hoje no veículo deste tablet (ADR-0073).
class MotoristaEscalado {
  const MotoristaEscalado({
    required this.motoristaId,
    required this.nome,
    required this.rotaId,
    required this.temReferencia,
    this.fotoHash,
  });

  factory MotoristaEscalado.fromJson(Map<String, dynamic> j) =>
      MotoristaEscalado(
        motoristaId: j['motoristaId'] as String,
        nome: j['nome'] as String,
        rotaId: j['rotaId'] as String,
        temReferencia: j['temReferencia'] as bool? ?? false,
        fotoHash: j['fotoHash'] as String?,
      );

  final String motoristaId;
  final String nome;
  final String rotaId;

  /// Já cadastrou o rosto; se não, entra pelo primeiro acesso.
  final bool temReferencia;

  /// SHA-256 da foto de referência: muda quando a central apaga e o
  /// motorista cadastra de novo.
  final String? fotoHash;

  Map<String, dynamic> toJson() => {
    'motoristaId': motoristaId,
    'nome': nome,
    'rotaId': rotaId,
    'temReferencia': temReferencia,
    'fotoHash': fotoHash,
  };
}

/// Resposta do pedido de código do primeiro acesso.
class CodigoEmitido {
  const CodigoEmitido({
    required this.enviado,
    required this.canal,
    this.codigo,
    this.destinoMascarado,
    this.situacao,
  });

  factory CodigoEmitido.fromJson(Map<String, dynamic> j) => CodigoEmitido(
    enviado: j['enviado'] as bool? ?? false,
    canal: j['canal'] as String? ?? 'nenhum',
    codigo: j['codigo'] as String?,
    destinoMascarado: j['destinoMascarado'] as String?,
    situacao: j['situacao'] as String?,
  );

  final bool enviado;

  /// `whatsapp` ou `tela-teste` (código volta na resposta).
  final String canal;
  final String? codigo;
  final String? destinoMascarado;

  /// Por que nada saiu: `nao-escalado`, `ja-cadastrado` ou `sem-telefone`.
  final String? situacao;
}

/// Código conferido: o ticket autoriza mandar a foto, depois do termo.
class PrimeiroAcessoLiberado {
  const PrimeiroAcessoLiberado({
    required this.ticket,
    required this.motoristaId,
    required this.nome,
    required this.termoVersao,
    required this.termoTexto,
  });

  factory PrimeiroAcessoLiberado.fromJson(Map<String, dynamic> j) {
    final termo = j['termo'] as Map<String, dynamic>;
    return PrimeiroAcessoLiberado(
      ticket: j['ticket'] as String,
      motoristaId: j['motoristaId'] as String,
      nome: j['nome'] as String,
      termoVersao: termo['versao'] as String,
      termoTexto: termo['texto'] as String,
    );
  }

  final String ticket;
  final String motoristaId;
  final String nome;
  final String termoVersao;
  final String termoTexto;
}

/// Em que momento a conferência aconteceu (mesmos nomes do servidor).
enum MomentoConferencia {
  login('Login'),
  inicioTranslado('InicioTranslado');

  const MomentoConferencia(this.apiValor);

  final String apiValor;
}

/// Uma conferência pelo rosto, guardada no tablet até chegar ao servidor.
class ConferenciaPendente {
  const ConferenciaPendente({
    required this.idLocal,
    required this.momento,
    required this.aprovada,
    required this.provaDeVida,
    required this.capturadaEm,
    this.motoristaId,
    this.nota,
    this.limiar,
    this.modeloVersao,
    this.latitude,
    this.longitude,
    this.precisaoM,
    this.rotaId,
    this.selfieBase64,
  });

  factory ConferenciaPendente.fromJson(Map<String, dynamic> j) =>
      ConferenciaPendente(
        idLocal: j['idLocal'] as String,
        motoristaId: j['motoristaId'] as String?,
        momento: MomentoConferencia.values.firstWhere(
          (m) => m.apiValor == j['momento'],
          orElse: () => MomentoConferencia.login,
        ),
        aprovada: j['aprovada'] as bool,
        nota: (j['nota'] as num?)?.toDouble(),
        limiar: (j['limiar'] as num?)?.toDouble(),
        provaDeVida: j['provaDeVida'] as bool,
        modeloVersao: j['modeloVersao'] as String?,
        latitude: (j['latitude'] as num?)?.toDouble(),
        longitude: (j['longitude'] as num?)?.toDouble(),
        precisaoM: (j['precisaoM'] as num?)?.toDouble(),
        rotaId: j['rotaId'] as String?,
        capturadaEm: DateTime.parse(j['capturadaEm'] as String),
        selfieBase64: j['selfieBase64'] as String?,
      );

  final String idLocal;
  final String? motoristaId;
  final MomentoConferencia momento;
  final bool aprovada;
  final double? nota;
  final double? limiar;
  final bool provaDeVida;
  final String? modeloVersao;
  final double? latitude;
  final double? longitude;
  final double? precisaoM;
  final String? rotaId;
  final DateTime capturadaEm;
  final String? selfieBase64;

  Map<String, dynamic> toJson() => {
    'idLocal': idLocal,
    'motoristaId': motoristaId,
    'momento': momento.apiValor,
    'aprovada': aprovada,
    'nota': nota,
    'limiar': limiar,
    'provaDeVida': provaDeVida,
    'modeloVersao': modeloVersao,
    'latitude': latitude,
    'longitude': longitude,
    'precisaoM': precisaoM,
    'rotaId': rotaId,
    'capturadaEm': capturadaEm.toUtc().toIso8601String(),
    'selfieBase64': selfieBase64,
  };
}

/// UUID v4 para o idLocal (o servidor usa para não duplicar).
String novoIdConferencia() {
  final r = Random.secure();
  final b = List<int>.generate(16, (_) => r.nextInt(256));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  String h(int i) => b[i].toRadixString(16).padLeft(2, '0');
  final s = [for (var i = 0; i < 16; i++) h(i)].join();
  return '${s.substring(0, 8)}-${s.substring(8, 12)}-${s.substring(12, 16)}-'
      '${s.substring(16, 20)}-${s.substring(20)}';
}
