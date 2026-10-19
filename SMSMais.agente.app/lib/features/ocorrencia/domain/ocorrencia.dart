import 'dart:math';

import 'package:flutter/material.dart';

/// O que o motorista reporta pelo botão REPORT (contrato em
/// `docs/modulos/tfd/deslocamento-tablet.md`). [apiValor] é o nome do enum
/// `TipoOcorrenciaVeiculo` no servidor.
enum TipoOcorrencia {
  acidente('Acidente', 'Acidente', Icons.car_crash),
  paneVeiculo('Pane no veículo', 'PaneVeiculo', Icons.car_repair),
  pacienteMalEstar(
    'Paciente passou mal',
    'PacienteMalEstar',
    Icons.medical_services,
  ),
  outro('Outro', 'Outro', Icons.report);

  const TipoOcorrencia(this.rotulo, this.apiValor, this.icone);

  final String rotulo;
  final String apiValor;
  final IconData icone;

  /// "Outro" só vai com o motorista dizendo o que houve.
  bool get exigeDescricao => this == TipoOcorrencia.outro;
}

/// REPORT ainda não aceito pelo servidor (sem rede, por exemplo). O
/// [idLocal] faz o reenvio devolver a mesma ocorrência em vez de duplicar.
class OcorrenciaPendente {
  const OcorrenciaPendente({
    required this.idLocal,
    required this.tipo,
    required this.ocorridaEm,
    this.descricao,
    this.latitude,
    this.longitude,
    this.precisaoM,
  });

  factory OcorrenciaPendente.fromJson(Map<String, dynamic> json) =>
      OcorrenciaPendente(
        idLocal: json['idLocal'] as String,
        tipo: TipoOcorrencia.values.firstWhere(
          (t) => t.apiValor == json['tipo'],
          orElse: () => TipoOcorrencia.outro,
        ),
        descricao: json['descricao'] as String?,
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
        precisaoM: (json['precisaoM'] as num?)?.toDouble(),
        ocorridaEm: DateTime.parse(json['ocorridaEm'] as String),
      );

  final String idLocal;
  final TipoOcorrencia tipo;
  final String? descricao;
  final double? latitude;
  final double? longitude;
  final double? precisaoM;
  final DateTime ocorridaEm;

  Map<String, dynamic> toJson() => <String, dynamic>{
    'idLocal': idLocal,
    'tipo': tipo.apiValor,
    'descricao': descricao,
    'latitude': latitude,
    'longitude': longitude,
    'precisaoM': precisaoM,
    'ocorridaEm': ocorridaEm.toUtc().toIso8601String(),
  };
}

/// UUID v4 (RFC 4122) para o [OcorrenciaPendente.idLocal].
String novoIdLocal([Random? aleatorio]) {
  final rng = aleatorio ?? Random.secure();
  final b = List<int>.generate(16, (_) => rng.nextInt(256));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  String hex(int i, int j) =>
      b.sublist(i, j).map((x) => x.toRadixString(16).padLeft(2, '0')).join();
  return '${hex(0, 4)}-${hex(4, 6)}-${hex(6, 8)}-${hex(8, 10)}-${hex(10, 16)}';
}
