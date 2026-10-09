import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';
import 'package:url_launcher/url_launcher.dart';

/// Reserva: o número que o PWA usa hoje, para o botão funcionar mesmo sem rede.
const _whatsappReserva = '552137315313';
const _mensagem = 'Olá! Preciso de ajuda com a Saúde de Maricá.';

/// Número oficial do WhatsApp da Secretaria — vem do cadastro institucional
/// (`GET /publico/instituicao`), não do código (instância por município, ADR-0043).
final _whatsappProvider = FutureProvider<String>((ref) async {
  try {
    final numero = soDigitos((await ref.watch(apiProvider).instituicao()).whatsAppNumeroPublico);
    return numero.isNotEmpty ? numero : _whatsappReserva;
  } on Object {
    return _whatsappReserva;
  }
});

/// (21) 3731-5313 a partir de 552137315313.
String _formatarNumero(String numero) {
  final nacional = numero.startsWith('55') && numero.length >= 12 ? numero.substring(2) : numero;
  return mascararTelefone(nacional);
}

/// Chat com a Secretaria de Saúde (`Chat.tsx`): abre a conversa no WhatsApp oficial — do outro
/// lado, a equipe atende pela Central de Atendimento do painel (módulo Conversas). Um toque, sem
/// cadastro nem tela nova para aprender.
class ChatPage extends ConsumerWidget {
  const ChatPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final numero = ref.watch(_whatsappProvider).valueOrNull ?? _whatsappReserva;

    Future<void> abrir() async {
      final url = Uri.parse('https://wa.me/$numero?text=${Uri.encodeComponent(_mensagem)}');
      await launchUrl(url, mode: LaunchMode.externalApplication);
    }

    // `space-y-5` (20) + `mb-3` do cabeçalho (12): no navegador as margens colapsam e vale 20 —
    // o cabeçalho já traz 12, faltam 8.
    return CorpoPagina(
      espaco: 8,
      children: [
        const CabecalhoSecao(sobretitulo: 'Fale com a gente', titulo: 'Chat'),
        Container(
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: CoresMarica.branco,
            borderRadius: BorderRadius.circular(RaiosMarica.x3l),
            border: Border.all(color: CoresMarica.areia),
            boxShadow: SombrasMarica.carta,
          ),
          child: Column(
            children: [
              Container(
                width: 64,
                height: 64,
                decoration: const BoxDecoration(color: CoresMarica.lagoaClaro, shape: BoxShape.circle),
                child: const Icon(LucideIcons.messageCircle, size: 32, color: CoresMarica.lagoa),
              ),
              const SizedBox(height: 16),
              Text(
                'Converse com a Saúde de Maricá',
                textAlign: TextAlign.center,
                style: Txt.display(18),
              ),
              const SizedBox(height: 8),
              Text(
                'Tire dúvidas sobre exames, agendamentos e documentos direto pelo WhatsApp. Nossa equipe responde em horário de atendimento.',
                textAlign: TextAlign.center,
                style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
              ),
              const SizedBox(height: 20),
              BotaoPrimario(rotulo: 'Abrir conversa no WhatsApp', icone: LucideIcons.messageCircle, onPressed: abrir),
              const SizedBox(height: 12),
              Text.rich(
                TextSpan(
                  style: Txt.sans(12, cor: CoresMarica.tintaMute),
                  children: [
                    const TextSpan(text: 'Número oficial: '),
                    TextSpan(text: _formatarNumero(numero), style: Txt.sans(12, peso: FontWeight.w500)),
                  ],
                ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ],
    );
  }
}
