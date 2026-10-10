import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/sessao/sair.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

const _secoesConsultas = {
  'Proximo': 'Próximas consultas',
  'NaFila': 'Na fila, aguardando vaga',
  'Passado': 'Consultas anteriores',
};

const _secoesExames = {
  'Proximo': 'Próximos exames',
  'NaFila': 'Na fila, aguardando vaga',
  'Passado': 'Exames anteriores',
};

/// Consultas próximas, na fila da regulação e anteriores (`ConsultasAgendadas` de Agendados.tsx).
class ConsultasAgendadasPage extends ConsumerWidget {
  const ConsultasAgendadasPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Lista<Agendamento>(
      sobretitulo: 'Agenda',
      titulo: 'Consultas',
      carregar: () => ref.read(apiProvider).agendamentos('consulta'),
      iconeVazio: LucideIcons.calendarClock,
      tituloVazio: 'Nenhuma consulta por aqui',
      descricaoVazio:
          'Suas consultas aparecem aqui: as marcadas, com data, profissional e local; as que estão na fila da regulação (SISREG, SER, SERNIT ou São Gonçalo); e as que já passaram.',
      secao: (a) => _secoesConsultas[a.momentoEfetivo] ?? '',
      item: (_, a, __) => AgendamentoCard(agendamento: a),
    );
  }
}

/// Exames próximos, na fila e anteriores (`ExamesAgendados`). Quando o link do WhatsApp acabou de confirmar
/// a presença, mostra o modal "Agenda confirmada" e destaca o card — no PWA isto chega pelo
/// `sessionStorage`; aqui, pelo [confirmacaoPendenteProvider].
class ExamesAgendadosPage extends ConsumerStatefulWidget {
  const ExamesAgendadosPage({super.key});

  @override
  ConsumerState<ExamesAgendadosPage> createState() => _ExamesAgendadosPageState();
}

class _ExamesAgendadosPageState extends ConsumerState<ExamesAgendadosPage> {
  String? _destacadoId;

  @override
  void initState() {
    super.initState();
    // Provider não pode mudar durante o build: lê e zera depois do 1º quadro.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final confirmacao = ref.read(confirmacaoPendenteProvider);
      if (confirmacao == null) return;
      ref.read(confirmacaoPendenteProvider.notifier).state = null;
      setState(() => _destacadoId = confirmacao.solicitacaoExameId);
      _mostrarAgendaConfirmada(context, confirmacao);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Lista<Agendamento>(
      sobretitulo: 'Agenda',
      titulo: 'Exames',
      carregar: () => ref.read(apiProvider).agendamentos('exame'),
      iconeVazio: LucideIcons.calendarPlus,
      tituloVazio: 'Nenhum exame por aqui',
      descricaoVazio:
          'Seus exames aparecem aqui: os marcados, com data, tipo e local; os que estão na fila da regulação (SISREG, SER, SERNIT ou São Gonçalo); e os que já passaram.',
      secao: (a) => _secoesExames[a.momentoEfetivo] ?? '',
      item: (_, a, recarregar) => AgendamentoCard(
        agendamento: a,
        destacado: a.solicitacaoExameId != null && a.solicitacaoExameId == _destacadoId,
        aoResponder: recarregar,
      ),
    );
  }
}

Future<void> _mostrarAgendaConfirmada(BuildContext context, ConfirmacaoAgendamento c) {
  final base = Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43);
  return showDialog<void>(
    context: context,
    barrierColor: Colors.black.withValues(alpha: 0.5),
    builder: (ctx) => Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 384),
          child: Material(
            type: MaterialType.transparency,
            child: Cartao(
              padding: const EdgeInsets.all(24),
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(LucideIcons.circleCheck, size: 48, color: CoresMarica.verde600),
                    const SizedBox(height: 12),
                    Text('Agenda confirmada!', style: Txt.display(18, peso: FontWeight.w700)),
                    const SizedBox(height: 8),
                    Text.rich(
                      TextSpan(
                        style: base,
                        children: [
                          const TextSpan(text: 'Sua presença no exame '),
                          TextSpan(text: c.titulo, style: base.copyWith(fontWeight: FontWeight.w700)),
                          if (c.inicioEm != null) TextSpan(text: ' em ${formatarDataHora(c.inicioEm!)}'),
                          if (c.unidade != null) TextSpan(text: ', ${c.unidade},'),
                          const TextSpan(text: ' está confirmada. Obrigado! 😊'),
                        ],
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 12),
                    const AvisoGuia(),
                    const SizedBox(height: 20),
                    BotaoPrimario(rotulo: 'Ok, entendi', onPressed: () => Navigator.of(ctx).pop()),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class AgendamentoCard extends StatelessWidget {
  const AgendamentoCard({required this.agendamento, this.destacado = false, this.aoResponder, super.key});

  final Agendamento agendamento;
  final bool destacado;
  final VoidCallback? aoResponder;

  @override
  Widget build(BuildContext context) {
    final a = agendamento;
    final mute = Txt.sans(14, cor: CoresMarica.tintaMute);
    final passado = a.momentoEfetivo == 'Passado';
    // Pedidos do SISREG (exame ou consulta) têm ticket com detalhes; os das outras regulações, não.
    final abrirTicket = a.solicitacaoExameId == null
        ? null
        : () => context.push('/agendados/exames/${a.solicitacaoExameId}');

    Widget linha(IconData icone, String texto) => Row(
          children: [
            Icon(icone, size: 16, color: CoresMarica.tintaMute),
            const SizedBox(width: 6),
            Expanded(child: Text(texto, maxLines: 1, overflow: TextOverflow.ellipsis, style: mute)),
          ],
        );

    final detalhes = <Widget>[
      if (a.profissional != null) linha(LucideIcons.stethoscope, a.profissional!),
      if (a.unidade != null) linha(LucideIcons.mapPin, a.unidade!),
      // Na fila: só "está na fila" — nunca motivo de pendência, posição ou previsão.
      if (a.naFila)
        Text(
          'Está na fila ${a.origem != null ? 'da ${a.origem}' : 'da regulação'}, aguardando vaga. Quando for agendado, aparece aqui com data e local.',
          style: mute.copyWith(height: 1.375),
        )
      else if (a.origem != null)
        linha(LucideIcons.landmark, a.inicioEm != null ? 'Marcado pela ${a.origem}' : 'Pedido na ${a.origem}'),
    ];

    return Cartao(
      padding: const EdgeInsets.all(16),
      destaque: destacado ? CoresMarica.verde500 : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Semantics(
            button: abrirTicket != null,
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: abrirTicket,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(a.titulo, maxLines: 1, overflow: TextOverflow.ellipsis, style: Txt.display(16)),
                            const SizedBox(height: 2),
                            Row(
                              children: [
                                Icon(
                                  a.inicioEm != null ? LucideIcons.clock : LucideIcons.hourglass,
                                  size: 16,
                                  color: CoresMarica.tintaMute,
                                ),
                                const SizedBox(width: 6),
                                Flexible(
                                  child: Text(
                                    a.inicioEm != null
                                        ? (a.temHora ? formatarDataHora(a.inicioEm!) : formatarData(a.inicioEm!))
                                        : (passado ? 'Sem data marcada' : 'Ainda sem data'),
                                    style: mute,
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 12),
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Etiqueta(status: _rotuloEtiqueta(a)),
                          if (abrirTicket != null) ...[
                            const SizedBox(width: 4),
                            const Icon(LucideIcons.chevronRight, size: 16, color: CoresMarica.tintaMute),
                          ],
                        ],
                      ),
                    ],
                  ),
                  if (detalhes.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    for (var i = 0; i < detalhes.length; i++) ...[
                      if (i > 0) const SizedBox(height: 6),
                      detalhes[i],
                    ],
                  ],
                ],
              ),
            ),
          ),
          if (a.podeResponder && a.solicitacaoExameId != null)
            _RespostaConfirmacao(solicitacaoExameId: a.solicitacaoExameId!, aoResponder: aoResponder),
        ],
      ),
    );
  }
}

String _rotuloEtiqueta(Agendamento a) => switch (a.statusConfirmacao) {
      'Confirmada' => 'Confirmada',
      'Cancelada' => 'Cancelada',
      _ => a.status,
    };

/// Texto amigável para a falha ao responder o agendamento. Traduzimos o código de negócio para
/// uma frase da linguagem do cidadão — a mensagem do servidor nunca é exibida.
String _mensagemDeFalha(Object erro) {
  final (:status, :codigo) = classificarErro(erro);
  if (codigo == 'confirmacao.ja_respondida') {
    return 'Este agendamento já foi respondido. Puxe a tela para baixo para atualizar.';
  }
  if (codigo == 'confirmacao.exame_passado') {
    return 'A data deste agendamento já passou. Procure a unidade para remarcar.';
  }
  if (status == 404) return 'Não encontramos este agendamento. Puxe a tela para baixo para atualizar.';
  return 'Não foi possível registrar. Tente novamente.';
}

/// Botões Confirmar / Não poderei ir do exame ainda sem resposta (importado do SISREG).
class _RespostaConfirmacao extends ConsumerStatefulWidget {
  const _RespostaConfirmacao({required this.solicitacaoExameId, this.aoResponder});

  final String solicitacaoExameId;
  final VoidCallback? aoResponder;

  @override
  ConsumerState<_RespostaConfirmacao> createState() => _RespostaConfirmacaoState();
}

class _RespostaConfirmacaoState extends ConsumerState<_RespostaConfirmacao> {
  final _motivo = TextEditingController();
  final _foco = FocusNode();
  bool _pedindoMotivo = false;
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _motivo.dispose();
    _foco.dispose();
    super.dispose();
  }

  Future<void> _confirmar() async {
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await ref.read(apiProvider).confirmarExame(widget.solicitacaoExameId);
      widget.aoResponder?.call();
    } on Object catch (e) {
      if (mounted) {
        setState(() {
          _erro = _mensagemDeFalha(e);
          _enviando = false;
        });
      }
    }
  }

  Future<void> _cancelar() async {
    final motivo = _motivo.text.trim();
    if (motivo.isEmpty) {
      setState(() => _erro = 'Conte pra gente o motivo, assim oferecemos a vaga a outra pessoa.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await ref.read(apiProvider).cancelarExame(widget.solicitacaoExameId, motivo);
      widget.aoResponder?.call();
    } on Object catch (e) {
      if (mounted) {
        setState(() {
          _erro = _mensagemDeFalha(e);
          _enviando = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    OutlineInputBorder borda(Color cor) => OutlineInputBorder(
          borderRadius: BorderRadius.circular(RaiosMarica.xl),
          borderSide: BorderSide(color: cor),
        );

    return Container(
      margin: const EdgeInsets.only(top: 16),
      padding: const EdgeInsets.only(top: 12),
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: Colors.black.withValues(alpha: 0.05))),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (!_pedindoMotivo)
            Row(
              children: [
                Expanded(
                  child: BotaoPrimario(rotulo: 'Confirmar presença', onPressed: _enviando ? null : _confirmar),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: BotaoFantasma(
                    rotulo: 'Não poderei ir',
                    onPressed: _enviando
                        ? null
                        : () {
                            setState(() => _pedindoMotivo = true);
                            _foco.requestFocus();
                          },
                  ),
                ),
              ],
            )
          else ...[
            Row(
              children: [
                Expanded(child: Text('Qual o motivo?', style: Txt.sans(14, peso: FontWeight.w500))),
                IconButton(
                  onPressed: () => setState(() {
                    _pedindoMotivo = false;
                    _erro = null;
                  }),
                  tooltip: 'Fechar',
                  visualDensity: VisualDensity.compact,
                  icon: const Icon(LucideIcons.x, size: 16, color: CoresMarica.tintaMute),
                ),
              ],
            ),
            const SizedBox(height: 4),
            TextField(
              controller: _motivo,
              focusNode: _foco,
              minLines: 3,
              maxLines: 3,
              maxLength: 500,
              style: Txt.sans(14),
              decoration: InputDecoration(
                hintText: 'Ex.: estarei viajando, consegui em outro lugar…',
                hintStyle: Txt.sans(14, cor: CoresMarica.tintaMute.withValues(alpha: 0.6)),
                filled: true,
                fillColor: CoresMarica.branco,
                isDense: true,
                counterText: '',
                contentPadding: const EdgeInsets.all(12),
                border: borda(Colors.black.withValues(alpha: 0.1)),
                enabledBorder: borda(Colors.black.withValues(alpha: 0.1)),
                focusedBorder: borda(CoresMarica.marica),
              ),
            ),
            const SizedBox(height: 8),
            BotaoPrimario(rotulo: 'Enviar e avisar a unidade', onPressed: _enviando ? null : _cancelar),
          ],
          if (_erro != null) ...[
            const SizedBox(height: 8),
            TextoErro(_erro!),
          ],
        ],
      ),
    );
  }
}
