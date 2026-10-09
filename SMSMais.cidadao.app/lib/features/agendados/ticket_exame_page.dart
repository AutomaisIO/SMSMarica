import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';
import 'package:uuid/uuid.dart';

const _rotuloCanal = {
  'app': 'pelo aplicativo',
  'whatsapp-link': 'pelo link do WhatsApp',
  'whatsapp-quickreply': 'pelo WhatsApp',
  'ligacao': 'por ligação',
  'telefone': 'por ligação',
  'presencial': 'presencialmente na unidade',
  'sandbox': 'teste (sandbox)',
};

String _canalTexto(String? canal) => canal == null ? '' : (_rotuloCanal[canal] ?? 'por $canal');

/// "Ticket" do exame agendado (`TicketExame.tsx`): cabeçalho vinho→Maricá, picote, dados do
/// agendamento, rastro da confirmação, chave de acesso do dia e o QR para a recepção.
class TicketExamePage extends ConsumerStatefulWidget {
  const TicketExamePage({required this.id, super.key});

  final String id;

  @override
  ConsumerState<TicketExamePage> createState() => _TicketExamePageState();
}

class _TicketExamePageState extends ConsumerState<TicketExamePage> {
  AgendamentoExameDetalhe? _detalhe;
  String? _erro;

  // Chave de acesso: nunca fica guardada no aparelho — some ao sair da tela.
  ChaveAcessoExame? _chave;
  bool _lendoChave = false;
  String? _erroChave;

  // QR do "ticket" — por ora um UUID aleatório (estável enquanto a tela está aberta).
  final String _qrValor = const Uuid().v4();

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    try {
      final d = await ref.read(apiProvider).agendamentoExame(widget.id);
      if (mounted) setState(() => _detalhe = d);
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    }
  }

  Future<void> _verChave() async {
    setState(() {
      _lendoChave = true;
      _erroChave = null;
    });
    try {
      final c = await ref.read(apiProvider).chaveAcessoExame(widget.id);
      if (mounted) setState(() => _chave = c);
    } on Object catch (e) {
      if (mounted) setState(() => _erroChave = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _lendoChave = false);
    }
  }

  void _voltar() {
    if (context.canPop()) {
      context.pop();
    } else {
      context.go('/agendados/exames');
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_erro != null) {
      return ListView(
        padding: paddingPagina.copyWith(top: 28),
        children: [
          Subir(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [_Voltar(aoVoltar: _voltar), const SizedBox(height: 4), ErroCard(mensagem: _erro!)],
            ),
          ),
        ],
      );
    }
    if (_detalhe == null) {
      return const Padding(padding: EdgeInsets.only(top: 96), child: Align(alignment: Alignment.topCenter, child: Girando()));
    }

    final d = _detalhe!;
    final confirmada = d.statusConfirmacao == 'Confirmada';
    final cancelada = d.statusConfirmacao == 'Cancelada';

    final corpo = <Widget>[
      ?_item(LucideIcons.building2, 'Local do exame (executante)', d.unidadeExecutoraNome),
      ?_item(LucideIcons.mapPin, 'Endereço', d.unidadeExecutoraEndereco),
      ?_item(LucideIcons.phone, 'Telefone da unidade', d.unidadeExecutoraTelefone),
      ?_item(LucideIcons.building2, 'Unidade solicitante', d.unidadeSolicitanteNome),
      ?_item(LucideIcons.stethoscope, 'Solicitante', d.solicitanteNome),
      Column(
        children: [
          Row(
            children: [
              Expanded(child: _ItemPequeno(rotulo: 'Data da solicitação', valor: formatarDataPura(d.dataSolicitacao))),
              const SizedBox(width: 12),
              Expanded(child: _ItemPequeno(rotulo: 'Data da regulação', valor: formatarDataPura(d.dataRegulacao))),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _ItemPequeno(rotulo: 'Nº da solicitação', valor: d.codigoSolicitacao)),
              const SizedBox(width: 12),
              Expanded(child: _ItemPequeno(rotulo: 'Protocolo', valor: d.accessionNumber)),
            ],
          ),
        ],
      ),
      ?_item(LucideIcons.fileText, 'Observações', d.observacoes),
      // Rastro da confirmação/cancelamento
      if (confirmada || cancelada) _Rastro(detalhe: d, confirmada: confirmada),
      // Chave de acesso — o back só libera no dia do atendimento
      if (!cancelada && (d.chaveAcessoDisponivelHoje || d.dataAgendada != null)) _blocoChave(d),
      // QR do ticket
      Column(
        children: [
          const LinhaTracejada(espessura: 1),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: CoresMarica.branco,
              borderRadius: BorderRadius.circular(RaiosMarica.xl),
              border: Border.all(color: CoresMarica.areia),
            ),
            child: QrImageView(
              data: _qrValor,
              size: 140,
              padding: EdgeInsets.zero,
              backgroundColor: CoresMarica.branco,
              eyeStyle: const QrEyeStyle(eyeShape: QrEyeShape.square, color: Color(0xFF7A1420)),
              dataModuleStyle: const QrDataModuleStyle(
                dataModuleShape: QrDataModuleShape.square,
                color: Color(0xFF7A1420),
              ),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Apresente este código na recepção da unidade.',
            textAlign: TextAlign.center,
            style: Txt.sans(11, cor: CoresMarica.tintaMute),
          ),
        ],
      ),
    ];

    return ListView(
      padding: paddingPagina.copyWith(bottom: 64),
      children: [
        Subir(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Align(alignment: Alignment.centerLeft, child: _Voltar(aoVoltar: _voltar)),
              const SizedBox(height: 20),
              DecoratedBox(
                decoration: BoxDecoration(
                  color: CoresMarica.branco,
                  borderRadius: BorderRadius.circular(RaiosMarica.x3l),
                  boxShadow: SombrasMarica.cartao,
                ),
                child: Container(
                  clipBehavior: Clip.antiAlias,
                  decoration: BoxDecoration(
                    color: CoresMarica.branco,
                    borderRadius: BorderRadius.circular(RaiosMarica.x3l),
                    border: Border.all(color: CoresMarica.areia),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _Cabecalho(detalhe: d),
                      const _Picote(),
                      Padding(
                        padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            for (var i = 0; i < corpo.length; i++) ...[
                              if (i > 0) const SizedBox(height: 16),
                              corpo[i],
                            ],
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget? _item(IconData icone, String rotulo, String? valor) {
    if (valor == null || valor.isEmpty) return null;
    return _Item(icone: icone, rotulo: rotulo, valor: valor);
  }

  Widget _blocoChave(AgendamentoExameDetalhe d) {
    final Widget conteudo;
    if (_chave != null) {
      conteudo = Subir(
        child: Column(
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(LucideIcons.keyRound, size: 14, color: CoresMarica.marica),
                const SizedBox(width: 6),
                Text('CHAVE DE ACESSO', style: Txt.sobretitulo(espacamentoEm: 0.2)),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              _chave!.chave,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontFamily: FontesMarica.mono,
                fontSize: 36,
                fontWeight: FontWeight.w700,
                letterSpacing: 36 * 0.3,
                color: CoresMarica.tinta,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Informe esta chave na recepção. Solicitação nº ${_chave!.codigoSolicitacao}.',
              textAlign: TextAlign.center,
              style: Txt.sans(11, cor: CoresMarica.tintaMute),
            ),
          ],
        ),
      );
    } else if (d.chaveAcessoDisponivelHoje) {
      conteudo = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          BotaoPrimario(
            rotulo: 'Visualizar chave de acesso',
            icone: LucideIcons.keyRound,
            carregando: _lendoChave,
            onPressed: _verChave,
          ),
          const SizedBox(height: 8),
          Text(
            'A chave é a sua confirmação no SISREG. Apresente-a na recepção hoje.',
            textAlign: TextAlign.center,
            style: Txt.sans(11, cor: CoresMarica.tintaMute),
          ),
          if (_erroChave != null) ...[
            const SizedBox(height: 8),
            Text(
              _erroChave!,
              textAlign: TextAlign.center,
              style: Txt.sans(14, cor: CoresMarica.vermelho700),
            ),
          ],
        ],
      );
    } else {
      final base = Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43);
      final negrito = base.copyWith(fontWeight: FontWeight.w700);
      conteudo = Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Padding(
            padding: EdgeInsets.only(top: 2),
            child: Icon(LucideIcons.keyRound, size: 16, color: CoresMarica.marica),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text.rich(
              TextSpan(
                style: base,
                children: [
                  const TextSpan(text: 'A '),
                  TextSpan(text: 'chave de acesso', style: negrito),
                  const TextSpan(text: ' aparece aqui '),
                  TextSpan(text: 'no dia do exame', style: negrito),
                  const TextSpan(text: '.'),
                ],
              ),
            ),
          ),
        ],
      );
    }

    return BordaTracejada(
      cor: CoresMarica.marica.withValues(alpha: 0.4),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: CoresMarica.marica.withValues(alpha: 0.05),
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        ),
        child: conteudo,
      ),
    );
  }
}

class _Voltar extends StatelessWidget {
  const _Voltar({required this.aoVoltar});

  final VoidCallback aoVoltar;

  @override
  Widget build(BuildContext context) {
    return Pressionavel(
      onTap: aoVoltar,
      escala: 0.95,
      semantica: 'Voltar',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(LucideIcons.arrowLeft, size: 16, color: CoresMarica.tintaMute),
          const SizedBox(width: 6),
          Text('Voltar', style: Txt.sans(14, peso: FontWeight.w500, cor: CoresMarica.tintaMute)),
        ],
      ),
    );
  }
}

class _Cabecalho extends StatelessWidget {
  const _Cabecalho({required this.detalhe});

  final AgendamentoExameDetalhe detalhe;

  @override
  Widget build(BuildContext context) {
    final d = detalhe;
    final etiqueta = switch (d.statusConfirmacao) {
      'Confirmada' => 'Confirmada',
      'Cancelada' => 'Não poderá comparecer',
      _ => 'Aguardando sua confirmação',
    };
    final branco85 = CoresMarica.branco.withValues(alpha: 0.85);
    return Container(
      decoration: const BoxDecoration(gradient: CoresMarica.gradienteCivico),
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.calendarPlus, size: 14, color: branco85),
              const SizedBox(width: 6),
              Text(
                'EXAME AGENDADO',
                style: Txt.sans(11, peso: FontWeight.w600, cor: branco85, espacamento: 11 * 0.2),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(d.tipoExame, style: Txt.display(24, peso: FontWeight.w700, cor: CoresMarica.branco, altura: 1.25)),
          if (d.dataAgendada != null) ...[
            const SizedBox(height: 4),
            Row(
              children: [
                Icon(LucideIcons.clock, size: 16, color: CoresMarica.branco.withValues(alpha: 0.9)),
                const SizedBox(width: 6),
                Text(
                  formatarDataHora(d.dataAgendada!),
                  style: Txt.sans(14, cor: CoresMarica.branco.withValues(alpha: 0.9)),
                ),
              ],
            ),
          ],
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
            decoration: BoxDecoration(
              color: CoresMarica.branco.withValues(alpha: 0.2),
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(etiqueta, style: Txt.sans(12, peso: FontWeight.w600, cor: CoresMarica.branco)),
          ),
        ],
      ),
    );
  }
}

/// Recorte perfurado do ticket: meia-lua de cada lado + linha tracejada.
class _Picote extends StatelessWidget {
  const _Picote();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 24,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Positioned(left: -12, top: 0, child: _furo()),
          Positioned(right: -12, top: 0, child: _furo()),
          const Positioned(left: 16, right: 16, top: 11, child: LinhaTracejada()),
        ],
      ),
    );
  }

  Widget _furo() => Container(
        width: 24,
        height: 24,
        decoration: const BoxDecoration(color: CoresMarica.areia, shape: BoxShape.circle),
      );
}

class _Item extends StatelessWidget {
  const _Item({required this.icone, required this.rotulo, required this.valor});

  final IconData icone;
  final String rotulo;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(top: 2),
          child: Selo(icone: icone, tamanho: 32, tamanhoIcone: 16, raio: RaiosMarica.lg),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                rotulo.toUpperCase(),
                style: Txt.sans(11, peso: FontWeight.w500, cor: CoresMarica.tintaMute, espacamento: 11 * 0.025),
              ),
              Text(valor, style: Txt.sans(14, peso: FontWeight.w500)),
            ],
          ),
        ),
      ],
    );
  }
}

class _ItemPequeno extends StatelessWidget {
  const _ItemPequeno({required this.rotulo, required this.valor});

  final String rotulo;
  final String? valor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: CoresMarica.papel.withValues(alpha: 0.6),
        borderRadius: BorderRadius.circular(RaiosMarica.xl),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            rotulo.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Txt.sans(10, peso: FontWeight.w500, cor: CoresMarica.tintaMute, espacamento: 10 * 0.025),
          ),
          Text(
            (valor?.isNotEmpty ?? false) ? valor! : '—',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Txt.sans(14, peso: FontWeight.w500),
          ),
        ],
      ),
    );
  }
}

class _Rastro extends StatelessWidget {
  const _Rastro({required this.detalhe, required this.confirmada});

  final AgendamentoExameDetalhe detalhe;
  final bool confirmada;

  @override
  Widget build(BuildContext context) {
    final d = detalhe;
    final cor = confirmada ? CoresMarica.verde800 : CoresMarica.vermelho800;
    final base = Txt.sans(14, cor: cor, altura: 1.43);
    final negrito = base.copyWith(fontWeight: FontWeight.w700);
    final canal = d.confirmadoCanal != null ? ' ${_canalTexto(d.confirmadoCanal)}' : '';

    final Widget conteudo;
    if (confirmada) {
      conteudo = _linha(
        LucideIcons.circleCheck,
        cor,
        TextSpan(
          style: base,
          children: [
            const TextSpan(text: 'Presença '),
            TextSpan(text: 'confirmada', style: negrito),
            TextSpan(text: canal),
            if (d.confirmadoEm != null) TextSpan(text: ' em ${formatarDataHora(d.confirmadoEm!)}'),
            const TextSpan(text: '.'),
          ],
        ),
      );
    } else {
      conteudo = Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _linha(
            LucideIcons.circleX,
            cor,
            TextSpan(
              style: base,
              children: [
                const TextSpan(text: 'Você informou que '),
                TextSpan(text: 'não poderá comparecer', style: negrito),
                TextSpan(text: canal),
                if (d.confirmacaoCanceladaEm != null)
                  TextSpan(text: ' em ${formatarDataHora(d.confirmacaoCanceladaEm!)}'),
                const TextSpan(text: '.'),
              ],
            ),
          ),
          if (d.motivoCancelamentoPaciente != null) ...[
            const SizedBox(height: 4),
            Padding(
              padding: const EdgeInsets.only(left: 24),
              child: Text(
                'Motivo: ${d.motivoCancelamentoPaciente}',
                style: Txt.sans(14, cor: CoresMarica.vermelho700, altura: 1.43),
              ),
            ),
          ],
        ],
      );
    }

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: confirmada ? CoresMarica.verde50 : CoresMarica.vermelho50,
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
      ),
      child: conteudo,
    );
  }

  Widget _linha(IconData icone, Color cor, TextSpan texto) => Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(padding: const EdgeInsets.only(top: 2), child: Icon(icone, size: 16, color: cor)),
          const SizedBox(width: 8),
          Expanded(child: Text.rich(texto)),
        ],
      );
}
