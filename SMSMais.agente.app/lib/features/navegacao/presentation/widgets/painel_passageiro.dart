import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/navegacao/presentation/widgets/chat_translado_sheet.dart';
import 'package:agente/features/navegacao/presentation/widgets/status_confirmacao_chip.dart';
import 'package:flutter/material.dart';

/// Painel compacto do passageiro, sobreposto ao mapa.
///
/// Mostra ao motorista só o essencial da próxima parada: tipo, ETA, quem é, se
/// confirmou (paciente + acompanhante), observações e recados — sem roubar o
/// mapa. Botões: falar com o paciente e "Cheguei".
class PainelPassageiro extends StatelessWidget {
  const PainelPassageiro({
    required this.parada,
    required this.onCheguei,
    this.onOcultar,
    super.key,
  });

  final ParadaNavegacao parada;
  final VoidCallback onCheguei;

  /// Se informado, mostra um botão para ocultar o painel (vira FAB no mapa).
  final VoidCallback? onOcultar;

  bool get _isColeta => parada.tipo == TipoParada.coleta;

  @override
  Widget build(BuildContext context) {
    final hh = parada.horarioPrevisto.hour.toString().padLeft(2, '0');
    final mm = parada.horarioPrevisto.minute.toString().padLeft(2, '0');
    final principal =
        parada.passageiros.isNotEmpty ? parada.passageiros.first : null;

    return Material(
      elevation: 8,
      borderRadius: BorderRadius.circular(18),
      color: Colors.white,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: _isColeta ? MaricaTheme.vermelho : Colors.indigo,
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    _isColeta ? 'EMBARQUE' : 'DESEMBARQUE',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.5,
                    ),
                  ),
                ),
                const Spacer(),
                const Icon(Icons.schedule, size: 14, color: Colors.black45),
                const SizedBox(width: 4),
                Text(
                  '$hh:$mm',
                  style: const TextStyle(fontSize: 12, color: Colors.black54),
                ),
                if (onOcultar != null)
                  IconButton(
                    visualDensity: VisualDensity.compact,
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(),
                    icon: const Icon(Icons.keyboard_arrow_down, size: 22),
                    color: Colors.black45,
                    tooltip: 'Ocultar',
                    onPressed: onOcultar,
                  ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              parada.rotulo,
              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 17),
            ),
            Text(
              parada.endereco,
              style: const TextStyle(fontSize: 12.5, color: Colors.black54),
            ),

            if (parada.passageiros.length > 1) ...[
              const SizedBox(height: 8),
              Text(
                '${parada.passageiros.length} pacientes nesta parada',
                style: const TextStyle(fontSize: 12, color: Colors.black54),
              ),
            ],

            if (principal != null) ...[
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                children: [
                  StatusConfirmacaoChip(
                    rotulo: 'Paciente',
                    status: principal.confirmacao,
                  ),
                  if (principal.temAcompanhante)
                    StatusConfirmacaoChip(
                      rotulo: 'Acompanhante',
                      status: principal.acompanhante!.confirmacao,
                    ),
                ],
              ),
              if (principal.observacao != null) ...[
                const SizedBox(height: 8),
                Row(
                  children: [
                    const Icon(
                      Icons.info_outline,
                      size: 15,
                      color: Colors.orange,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        principal.observacao!,
                        style: const TextStyle(
                          fontSize: 12.5,
                          color: Colors.orange,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ],

            const SizedBox(height: 12),
            Row(
              children: [
                if (principal != null)
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => _abrirChat(context, principal),
                      icon: Badge(
                        isLabelVisible: principal.mensagensNaoLidas > 0,
                        label: Text('${principal.mensagensNaoLidas}'),
                        child: const Icon(Icons.chat_bubble_outline, size: 18),
                      ),
                      label: const Text('Falar'),
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size.fromHeight(46),
                        foregroundColor: MaricaTheme.vermelho,
                        side: const BorderSide(color: MaricaTheme.vermelho),
                      ),
                    ),
                  ),
                if (principal != null) const SizedBox(width: 10),
                Expanded(
                  flex: 2,
                  child: ElevatedButton.icon(
                    onPressed: onCheguei,
                    icon: const Icon(Icons.flag, size: 18),
                    label: const Text('Cheguei'),
                    style: ElevatedButton.styleFrom(
                      minimumSize: const Size.fromHeight(46),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _abrirChat(BuildContext context, PassageiroTranslado p) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => ChatTransladoSheet(passageiro: p),
    );
  }
}
