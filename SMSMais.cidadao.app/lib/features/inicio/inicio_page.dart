import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/perfil/perfil_provider.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

class _Atalho {
  const _Atalho(this.rota, this.rotulo, this.descricao, this.icone, {this.marica = false});

  final String rota;
  final String rotulo;
  final String descricao;
  final IconData icone;

  /// Tom Maricá (`marica`) em vez do clínico (`lagoa`).
  final bool marica;
}

/// Mesmos atalhos do PWA (`ATALHOS` de Home.tsx), na mesma ordem e com os mesmos tons.
const _atalhos = [
  _Atalho('/agendados/consultas', 'Consultas', 'Agendadas e na fila', LucideIcons.calendarClock),
  _Atalho('/agendados/exames', 'Exames', 'Agendados e na fila', LucideIcons.calendarPlus),
  _Atalho('/atendimentos', 'Atendimentos', 'Suas consultas', LucideIcons.stethoscope),
  _Atalho('/exames', 'Exames', 'Resultados', LucideIcons.flaskConical),
  _Atalho('/chat', 'Chat', 'Fale com a Saúde', LucideIcons.messageCircle, marica: true),
  _Atalho('/transporte', 'Transporte', 'TFD e viagens', LucideIcons.calendarHeart, marica: true),
];

/// Início (`Home.tsx`): saudação, o "Cartão do Cidadão" (assinatura visual do app) e a grade de
/// atalhos.
class InicioPage extends ConsumerStatefulWidget {
  const InicioPage({super.key});

  @override
  ConsumerState<InicioPage> createState() => _InicioPageState();
}

class _InicioPageState extends ConsumerState<InicioPage> {
  // Badge: exames agendados que ainda PRECISAM de atenção — cancelados e já confirmados
  // (inclusive presencialmente na recepção) saem da conta; o card continua na lista. Some
  // sozinho quando o exame passa (o backend só devolve futuros). Só conta exame do SISREG (com
  // solicitação): pedido da regulação externa não tem confirmação a responder.
  int _examesAgendados = 0;

  @override
  void initState() {
    super.initState();
    _carregarBadge();
  }

  Future<void> _carregarBadge() async {
    try {
      final lista = await ref.read(apiProvider).agendamentos('exame');
      if (!mounted) return;
      setState(() {
        _examesAgendados = lista
            .where(
              (a) =>
                  a.solicitacaoExameId != null &&
                  a.statusConfirmacao != 'Cancelada' &&
                  a.statusConfirmacao != 'Confirmada',
            )
            .length;
      });
    } on Object {
      /* badge é enfeite — sem rede, fica como estava */
    }
  }

  Future<void> _atualizar() async {
    await Future.wait([ref.read(perfilProvider.notifier).recarregar(), _carregarBadge()]);
  }

  @override
  Widget build(BuildContext context) {
    final perfil = ref.watch(perfilProvider).valueOrNull;
    final sessao = ref.watch(sessaoProvider);
    final nome = perfil?.nomeExibicao ?? sessao?.paciente.nome ?? 'Cidadão';
    final primeiro = nome.trim().split(' ').first;
    final cpf = perfil?.cpf ?? sessao?.paciente.cpf;
    final badges = {'/agendados/exames': _examesAgendados};

    return CorpoPagina(
      aoAtualizar: _atualizar,
      espaco: 28,
      children: [
        // No PWA o `space-y-7` não separa a saudação do cartão (o cartão está dentro de um <a>
        // inline, onde margem vertical não vale): o cartão encosta no `pb-2` da saudação.
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text.rich(
                TextSpan(
                  style: Txt.sans(15, cor: CoresMarica.tintaMute),
                  children: [
                    const TextSpan(text: 'Olá, '),
                    TextSpan(
                      text: primeiro,
                      style: Txt.sans(15, peso: FontWeight.w600),
                    ),
                    const TextSpan(text: '. Bem-vindo de volta.'),
                  ],
                ),
              ),
            ),
            _CartaoCidadao(nome: nome, foto: perfil?.fotoBase64, cpf: cpf, cns: perfil?.cns),
          ],
        ),
        _GradeAtalhos(badges: badges),
      ],
    );
  }
}

/// ASSINATURA — Cartão do Cidadão: gradiente vinho→Maricá com a textura guilloché.
class _CartaoCidadao extends StatelessWidget {
  const _CartaoCidadao({required this.nome, required this.foto, required this.cpf, required this.cns});

  final String nome;
  final String? foto;
  final String? cpf;
  final String? cns;

  @override
  Widget build(BuildContext context) {
    final branco85 = CoresMarica.branco.withValues(alpha: 0.85);
    return Semantics(
      button: true,
      label: 'Ver meu perfil',
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: () => context.go('/perfil'),
        child: DecoratedBox(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(RaiosMarica.x3l),
            boxShadow: SombrasMarica.cartao,
          ),
          child: Guilloche(
            gradiente: CoresMarica.gradienteCivico,
            raio: RaiosMarica.x3l,
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(LucideIcons.heartPulse, size: 14, color: branco85),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          'CARTÃO DO CIDADÃO',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: Txt.sans(11, peso: FontWeight.w600, cor: branco85, espacamento: 11 * 0.2),
                        ),
                      ),
                      Text(
                        'Saúde Maricá',
                        style: Txt.display(14, cor: CoresMarica.branco.withValues(alpha: 0.9), espacamento: -0.2),
                      ),
                    ],
                  ),
                  const SizedBox(height: 24),
                  Row(
                    children: [
                      Avatar(
                        nome: nome,
                        foto: foto,
                        tamanho: 60,
                        fundo: CoresMarica.branco.withValues(alpha: 0.15),
                        corTexto: CoresMarica.branco,
                        anel: Border.all(color: CoresMarica.branco.withValues(alpha: 0.3), width: 2),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              nome,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Txt.display(20, cor: CoresMarica.branco, altura: 1.25),
                            ),
                            if (cpf != null && cpf!.isNotEmpty)
                              Text(
                                'CPF ${formatarCpf(cpf!)}',
                                style: Txt.sans(14, cor: CoresMarica.branco.withValues(alpha: 0.8)),
                              ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'CARTÃO SUS',
                              style: Txt.sans(
                                10,
                                cor: CoresMarica.branco.withValues(alpha: 0.6),
                                espacamento: 10 * 0.1,
                              ),
                            ),
                            Text(
                              (cns?.isNotEmpty ?? false) ? formatarCns(cns!) : '— — —',
                              style: TextStyle(
                                fontFamily: FontesMarica.mono,
                                fontSize: 14,
                                letterSpacing: 14 * 0.05,
                                color: CoresMarica.branco.withValues(alpha: 0.9),
                              ),
                            ),
                          ],
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                        decoration: BoxDecoration(
                          color: CoresMarica.branco.withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              'Ver perfil',
                              style: Txt.sans(12, peso: FontWeight.w500, cor: CoresMarica.branco),
                            ),
                            const SizedBox(width: 4),
                            const Icon(LucideIcons.chevronRight, size: 14, color: CoresMarica.branco),
                          ],
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Grade 2×N de atalhos (`grid grid-cols-2 gap-3`), com as linhas na mesma altura.
class _GradeAtalhos extends StatelessWidget {
  const _GradeAtalhos({required this.badges});

  final Map<String, int> badges;

  @override
  Widget build(BuildContext context) {
    final linhas = <Widget>[];
    for (var i = 0; i < _atalhos.length; i += 2) {
      if (i > 0) linhas.add(const SizedBox(height: 12));
      linhas.add(
        IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: _CartaoAtalho(atalho: _atalhos[i], badge: badges[_atalhos[i].rota] ?? 0),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: i + 1 < _atalhos.length
                    ? _CartaoAtalho(atalho: _atalhos[i + 1], badge: badges[_atalhos[i + 1].rota] ?? 0)
                    : const SizedBox.shrink(),
              ),
            ],
          ),
        ),
      );
    }
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: linhas);
  }
}

class _CartaoAtalho extends StatelessWidget {
  const _CartaoAtalho({required this.atalho, required this.badge});

  final _Atalho atalho;
  final int badge;

  @override
  Widget build(BuildContext context) {
    return Pressionavel(
      onTap: () => context.go(atalho.rota),
      escala: 0.98,
      semantica: '${atalho.rotulo}: ${atalho.descricao}',
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: CoresMarica.branco,
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
          border: Border.all(color: CoresMarica.areia),
          boxShadow: SombrasMarica.carta,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                Selo(icone: atalho.icone, destaque: atalho.marica),
                if (badge > 0)
                  Positioned(
                    // -right-1.5 -top-1.5 + o ring-2 branco, que no PWA fica por fora da caixa.
                    right: -8,
                    top: -8,
                    child: Container(
                      constraints: const BoxConstraints(minWidth: 24, minHeight: 24),
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: CoresMarica.marica,
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: CoresMarica.branco, width: 2),
                      ),
                      child: Text(
                        badge > 9 ? '9+' : '$badge',
                        style: Txt.sans(11, peso: FontWeight.w700, cor: CoresMarica.branco, altura: 1),
                      ),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 12),
            Text(atalho.rotulo, style: Txt.display(16)),
            Text(atalho.descricao, style: Txt.sans(12, cor: CoresMarica.tintaMute)),
          ],
        ),
      ),
    );
  }
}
