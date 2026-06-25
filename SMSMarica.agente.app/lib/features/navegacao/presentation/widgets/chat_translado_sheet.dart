import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:flutter/material.dart';

/// Conversa objetiva motorista ↔ paciente, durante o translado.
///
/// Pensado para uso rápido ao volante: respostas prontas (1 toque) +
/// histórico curto. O envio real será via backend/push no app do cidadão.
class ChatTransladoSheet extends StatefulWidget {
  const ChatTransladoSheet({required this.passageiro, super.key});

  final PassageiroTranslado passageiro;

  @override
  State<ChatTransladoSheet> createState() => _ChatTransladoSheetState();
}

class _ChatTransladoSheetState extends State<ChatTransladoSheet> {
  late final List<MensagemTranslado> _mensagens =
      List.of(widget.passageiro.mensagens);

  static const _respostasRapidas = [
    'Estou a caminho.',
    'Chego em 5 minutos.',
    'Já estou na portaria.',
    'Pode descer, por favor.',
  ];

  void _enviar(String texto) {
    setState(() {
      _mensagens.add(
        MensagemTranslado(
          texto: texto,
          // Sem relógio real aqui; o backend carimba o horário ao persistir.
          quando: widget.passageiro.mensagens.isNotEmpty
              ? widget.passageiro.mensagens.last.quando
              : DateTime.fromMillisecondsSinceEpoch(0),
          doMotorista: true,
        ),
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 12,
        bottom: MediaQuery.of(context).viewInsets.bottom + 16,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Center(
            child: Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(
                color: Colors.black12,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              const Icon(Icons.person, color: MaricaTheme.vermelho),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  widget.passageiro.nome,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
              ),
              if (widget.passageiro.telefone != null)
                IconButton(
                  icon: const Icon(Icons.phone, color: Colors.green),
                  tooltip: 'Ligar',
                  onPressed: () {},
                ),
            ],
          ),
          const Divider(),
          ConstrainedBox(
            constraints: const BoxConstraints(maxHeight: 220),
            child: _mensagens.isEmpty
                ? const Padding(
                    padding: EdgeInsets.symmetric(vertical: 24),
                    child: Center(
                      child: Text(
                        'Sem mensagens ainda.',
                        style: TextStyle(color: Colors.black45),
                      ),
                    ),
                  )
                : ListView.builder(
                    shrinkWrap: true,
                    itemCount: _mensagens.length,
                    itemBuilder: (_, i) => _Bolha(mensagem: _mensagens[i]),
                  ),
          ),
          const SizedBox(height: 12),
          const Text(
            'Respostas rápidas',
            style: TextStyle(
              fontSize: 12,
              color: Colors.black54,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: _respostasRapidas
                .map(
                  (r) => ActionChip(
                    label: Text(r),
                    onPressed: () => _enviar(r),
                    backgroundColor: MaricaTheme.cinzaFundo,
                  ),
                )
                .toList(),
          ),
        ],
      ),
    );
  }
}

class _Bolha extends StatelessWidget {
  const _Bolha({required this.mensagem});
  final MensagemTranslado mensagem;

  @override
  Widget build(BuildContext context) {
    final doMotorista = mensagem.doMotorista;
    return Align(
      alignment: doMotorista ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        constraints: const BoxConstraints(maxWidth: 280),
        decoration: BoxDecoration(
          color: doMotorista ? MaricaTheme.vermelho : MaricaTheme.cinzaFundo,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(
          mensagem.texto,
          style: TextStyle(
            color: doMotorista ? Colors.white : Colors.black87,
            fontSize: 13.5,
          ),
        ),
      ),
    );
  }
}
