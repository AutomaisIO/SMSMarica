/// Veículo ao qual este tablet está vinculado (contrato em
/// `docs/modulos/tfd/deslocamento-tablet.md`).
class VeiculoVinculado {
  const VeiculoVinculado({
    required this.veiculoId,
    required this.placa,
    this.modelo,
  });

  factory VeiculoVinculado.fromJson(Map<String, dynamic> json) =>
      VeiculoVinculado(
        veiculoId: json['veiculoId'] as String,
        placa: (json['veiculoPlaca'] as String?) ?? '',
        modelo: json['veiculoModelo'] as String?,
      );

  final String veiculoId;
  final String placa;
  final String? modelo;

  String get descricao =>
      modelo == null || modelo!.isEmpty ? placa : '$placa · $modelo';
}

/// Ponto enviado em `POST /rastreamento/dispositivos/pontos`.
class PontoDispositivo {
  const PontoDispositivo({
    required this.latitude,
    required this.longitude,
    required this.capturadoEm,
    this.velocidadeKmh,
    this.rumo,
    this.precisaoM,
  });

  final double latitude;
  final double longitude;
  final DateTime capturadoEm;
  final double? velocidadeKmh;
  final double? rumo;
  final double? precisaoM;

  Map<String, dynamic> toJson() => <String, dynamic>{
    'latitude': latitude,
    'longitude': longitude,
    'velocidadeKmh': velocidadeKmh,
    'rumo': rumo,
    'precisaoM': precisaoM,
    'capturadoEm': capturadoEm.toUtc().toIso8601String(),
  };
}
