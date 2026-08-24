import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:flutter/material.dart';

/// Chip compacto do estado de confirmação (paciente/acompanhante).
class StatusConfirmacaoChip extends StatelessWidget {
  const StatusConfirmacaoChip({
    required this.rotulo,
    required this.status,
    super.key,
  });

  final String rotulo;
  final StatusConfirmacao status;

  @override
  Widget build(BuildContext context) {
    final (cor, fundo, icone, texto) = switch (status) {
      StatusConfirmacao.confirmado => (
          Colors.green.shade700,
          Colors.green.shade50,
          Icons.check_circle,
          'confirmado',
        ),
      StatusConfirmacao.pendente => (
          Colors.amber.shade800,
          Colors.amber.shade50,
          Icons.hourglass_empty,
          'aguardando',
        ),
      StatusConfirmacao.ausente => (
          Colors.red.shade700,
          Colors.red.shade50,
          Icons.cancel,
          'ausente',
        ),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: fundo,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: cor.withValues(alpha: 0.3)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icone, size: 14, color: cor),
          const SizedBox(width: 5),
          Text(
            '$rotulo: $texto',
            style: TextStyle(
              fontSize: 11.5,
              color: cor,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
