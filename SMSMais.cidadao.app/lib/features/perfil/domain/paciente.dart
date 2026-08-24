import 'package:equatable/equatable.dart';

/// DTO de leitura do paciente — espelha `PacienteDto` de
/// `SMSMais.Core`.
class Paciente extends Equatable {
  const Paciente({
    required this.id,
    required this.nomeCompleto,
    required this.cpf,
    required this.latitude,
    required this.longitude,
    required this.ativo,
    required this.cadastradoEm,
    this.cns,
  });

  factory Paciente.fromJson(Map<String, dynamic> json) {
    return Paciente(
      id: json['id'] as String,
      nomeCompleto: json['nomeCompleto'] as String,
      cpf: json['cpf'] as String,
      cns: json['cns'] as String?,
      latitude: (json['latitude'] as num).toDouble(),
      longitude: (json['longitude'] as num).toDouble(),
      ativo: json['ativo'] as bool,
      cadastradoEm: DateTime.parse(json['cadastradoEm'] as String),
    );
  }

  final String id;
  final String nomeCompleto;
  final String cpf;
  final String? cns;
  final double latitude;
  final double longitude;
  final bool ativo;
  final DateTime cadastradoEm;

  String get cpfFormatado {
    if (cpf.length != 11) {
      return cpf;
    }
    return '${cpf.substring(0, 3)}.${cpf.substring(3, 6)}.${cpf.substring(6, 9)}-${cpf.substring(9)}';
  }

  @override
  List<Object?> get props => [
        id,
        nomeCompleto,
        cpf,
        cns,
        latitude,
        longitude,
        ativo,
        cadastradoEm,
      ];
}
