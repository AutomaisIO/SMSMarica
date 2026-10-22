import 'package:agente/app/theme.dart';
import 'package:agente/features/ocorrencia/application/ocorrencias_controller.dart';
import 'package:agente/features/ocorrencia/presentation/report_sheet.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Botão REPORT (acidente, pane, paciente passou mal...), sempre à mão nas
/// telas do motorista. [sobreBarra] = na barra vermelha do topo (fundo
/// branco); senão, sobre o mapa (fundo vermelho). Com aviso preso na fila,
/// mostra quantos estão esperando rede.
class BotaoReport extends ConsumerWidget {
  const BotaoReport({this.sobreBarra = false, super.key});

  final bool sobreBarra;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pendentes = ref.watch(ocorrenciasProvider).pendentes;
    final fundo = sobreBarra ? Colors.white : MaricaTheme.vermelho;
    final frente = sobreBarra ? MaricaTheme.vermelho : Colors.white;
    return Material(
      color: fundo,
      elevation: sobreBarra ? 0 : 6,
      borderRadius: BorderRadius.circular(28),
      child: InkWell(
        borderRadius: BorderRadius.circular(28),
        onTap: () => abrirReport(context),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.warning_amber_rounded, color: frente, size: 26),
              const SizedBox(width: 8),
              Text(
                'REPORT',
                style: TextStyle(
                  color: frente,
                  fontSize: 17,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 1,
                ),
              ),
              if (pendentes > 0) ...[
                const SizedBox(width: 8),
                Tooltip(
                  message: 'Aviso aguardando rede',
                  child: CircleAvatar(
                    radius: 11,
                    backgroundColor: Colors.orange.shade700,
                    child: Text(
                      '$pendentes',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 12,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
