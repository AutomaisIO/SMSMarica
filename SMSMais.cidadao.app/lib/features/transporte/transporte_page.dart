import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/transporte/meus_acompanhantes.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Status da viagem na língua do paciente (`ROTULO_STATUS` do `Transporte.tsx`).
const _rotuloStatus = {
  'Pendente': 'Agendada',
  'Confirmada': 'Confirmada',
  'AguardandoRetorno': 'Aguardando retorno',
  'Realizada': 'Realizada',
  'NaoRealizada': 'Não realizada',
  'Cancelada': 'Cancelada',
};

/// Transporte de Pacientes (TFD): as viagens agendadas e, embaixo, quem pode acompanhar.
class TransportePage extends ConsumerWidget {
  const TransportePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Lista<ViagemTransporte>(
      sobretitulo: 'Transporte de Pacientes',
      titulo: 'Minhas viagens',
      carregar: () => ref.read(apiProvider).translados(),
      iconeVazio: LucideIcons.calendarHeart,
      tituloVazio: 'Nenhuma viagem agendada',
      descricaoVazio:
          'Quando o transporte da Prefeitura estiver agendado para o seu atendimento, as viagens aparecem aqui.',
      item: (_, v, __) => _CartaoViagem(viagem: v),
      depois: const [MeusAcompanhantes()],
    );
  }
}

class _CartaoViagem extends StatelessWidget {
  const _CartaoViagem({required this.viagem});

  final ViagemTransporte viagem;

  @override
  Widget build(BuildContext context) {
    final v = viagem;
    final textoMute = Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43);
    final tipoTratamento = v.tipoTratamento;
    final horaBusca = v.horaBusca;

    return Cartao(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Data pura (sem fuso): vale como está.
                    Text(capitalizar(formatarDiaSemana(v.data)), style: Txt.display(16)),
                    const SizedBox(height: 4),
                    Row(
                      children: [
                        const Icon(LucideIcons.mapPin, size: 16, color: CoresMarica.marica),
                        const SizedBox(width: 6),
                        Flexible(
                          child: Text(
                            '${v.destino}${v.cidade != null && v.cidade!.isNotEmpty ? ' · ${v.cidade}' : ''}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: Txt.sans(14, altura: 1.43),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Etiqueta(status: _rotuloStatus[v.status] ?? v.status),
            ],
          ),
          if (tipoTratamento != null && tipoTratamento.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(tipoTratamento, style: textoMute),
          ],
          const SizedBox(height: 8),
          _LinhaIcone(
            icone: LucideIcons.clock,
            texto: horaBusca != null && horaBusca.length >= 5
                ? 'Busca prevista às ${horaBusca.substring(0, 5)}'
                : 'O horário de busca é informado na véspera.',
          ),
          if (v.acompanhantes.isNotEmpty) ...[
            const SizedBox(height: 4),
            _LinhaIcone(icone: LucideIcons.users, texto: 'Acompanhante: ${v.acompanhantes.join(', ')}'),
          ],
        ],
      ),
    );
  }
}

class _LinhaIcone extends StatelessWidget {
  const _LinhaIcone({required this.icone, required this.texto});

  final IconData icone;
  final String texto;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(top: 2),
          child: Icon(icone, size: 16, color: CoresMarica.tintaMute),
        ),
        const SizedBox(width: 6),
        Expanded(child: Text(texto, style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43))),
      ],
    );
  }
}
