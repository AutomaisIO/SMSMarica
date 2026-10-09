import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';

// Kit visual do app — espelho de `SMSMais.cidadao.pwa/src/components/ui.tsx`. Os nomes seguem os
// do PWA (Card → Cartao, PrimaryButton → BotaoPrimario…) para quem mexe nos dois achar o par.

/// Iniciais a partir do nome, para o avatar sem foto.
String iniciais(String? nome) {
  if (nome == null || nome.trim().isEmpty) return '?';
  final p = nome.trim().split(RegExp(r'\s+'));
  final primeira = p.first.isNotEmpty ? p.first[0] : '';
  final ultima = p.length > 1 && p.last.isNotEmpty ? p.last[0] : '';
  return (primeira + ultima).toUpperCase();
}

/// Bytes de uma foto vinda da API como data URL (`data:image/jpeg;base64,...`) ou base64 puro.
Uint8List? bytesDaFoto(String? foto) {
  if (foto == null || foto.isEmpty) return null;
  final virgula = foto.indexOf(',');
  final b64 = foto.startsWith('data:') && virgula >= 0 ? foto.substring(virgula + 1) : foto;
  try {
    return base64Decode(b64);
  } on FormatException {
    return null;
  }
}

class Avatar extends StatelessWidget {
  const Avatar({
    required this.nome,
    this.foto,
    this.tamanho = 48,
    this.fundo,
    this.corTexto = CoresMarica.vinho,
    this.anel,
    super.key,
  });

  final String? nome;

  /// Data URL / base64 da foto do perfil.
  final String? foto;
  final double tamanho;
  final Color? fundo;
  final Color corTexto;

  /// Anel em volta (`ring-*`).
  final Border? anel;

  @override
  Widget build(BuildContext context) {
    final bytes = bytesDaFoto(foto);
    return Container(
      width: tamanho,
      height: tamanho,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: fundo ?? CoresMarica.vinho.withValues(alpha: 0.1),
        border: anel ?? Border.all(color: Colors.black.withValues(alpha: 0.05)),
      ),
      alignment: Alignment.center,
      child: bytes != null
          ? Image.memory(bytes, fit: BoxFit.cover, width: tamanho, height: tamanho, gaplessPlayback: true)
          : Text(
              iniciais(nome),
              style: Txt.display(tamanho * 0.38, cor: corTexto, altura: 1),
            ),
    );
  }
}

/// `rounded-2xl border border-areia bg-white shadow-carta`.
class Cartao extends StatelessWidget {
  const Cartao({
    required this.child,
    this.padding,
    this.corBorda = CoresMarica.areia,
    this.fundo = CoresMarica.branco,
    this.raio = RaiosMarica.x2l,
    this.destaque,
    this.sombra = SombrasMarica.carta,
    super.key,
  });

  final Widget child;
  final EdgeInsetsGeometry? padding;
  final Color corBorda;
  final Color fundo;
  final double raio;

  /// Anel de destaque (`ring-2 ring-*`), ex.: card aberto pelo link do WhatsApp.
  final Color? destaque;
  final List<BoxShadow> sombra;

  @override
  Widget build(BuildContext context) {
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: fundo,
        borderRadius: BorderRadius.circular(raio),
        border: destaque != null ? Border.all(color: destaque!, width: 2) : Border.all(color: corBorda),
        boxShadow: sombra,
      ),
      padding: padding,
      child: child,
    );
  }
}

/// Retorno de toque do PWA (`active:scale-[.99]`): encolhe de leve enquanto pressionado.
class Pressionavel extends StatefulWidget {
  const Pressionavel({
    required this.child,
    required this.onTap,
    this.escala = 0.99,
    this.semantica,
    super.key,
  });

  final Widget child;
  final VoidCallback? onTap;
  final double escala;
  final String? semantica;

  @override
  State<Pressionavel> createState() => _PressionavelState();
}

class _PressionavelState extends State<Pressionavel> {
  bool _pressionado = false;

  void _marcar(bool v) {
    if (widget.onTap == null) return;
    setState(() => _pressionado = v);
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: widget.semantica,
      enabled: widget.onTap != null,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTapDown: (_) => _marcar(true),
        onTapUp: (_) => _marcar(false),
        onTapCancel: () => _marcar(false),
        onTap: widget.onTap,
        child: AnimatedScale(
          scale: _pressionado ? widget.escala : 1,
          duration: const Duration(milliseconds: 120),
          child: widget.child,
        ),
      ),
    );
  }
}

/// `PrimaryButton`: vermelho Maricá, 52px, cantos 2xl, largura cheia.
class BotaoPrimario extends StatelessWidget {
  const BotaoPrimario({
    required this.rotulo,
    required this.onPressed,
    this.icone,
    this.carregando = false,
    this.larguraCheia = true,
    super.key,
  });

  final String rotulo;
  final VoidCallback? onPressed;
  final IconData? icone;
  final bool carregando;
  final bool larguraCheia;

  @override
  Widget build(BuildContext context) {
    final ativo = onPressed != null && !carregando;
    return Opacity(
      opacity: ativo || carregando ? 1 : 0.5,
      child: Pressionavel(
        onTap: ativo ? onPressed : null,
        semantica: rotulo,
        child: Container(
          constraints: const BoxConstraints(minHeight: 52),
          width: larguraCheia ? double.infinity : null,
          padding: const EdgeInsets.symmetric(horizontal: 20),
          decoration: BoxDecoration(
            color: CoresMarica.marica,
            borderRadius: BorderRadius.circular(RaiosMarica.x2l),
            boxShadow: SombrasMarica.carta,
          ),
          child: Row(
            mainAxisSize: larguraCheia ? MainAxisSize.max : MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (carregando) ...[
                const Girando(cor: CoresMarica.branco, tamanho: 20),
                const SizedBox(width: 8),
              ] else if (icone != null) ...[
                Icon(icone, size: 20, color: CoresMarica.branco),
                const SizedBox(width: 8),
              ],
              Flexible(
                child: Text(
                  rotulo,
                  textAlign: TextAlign.center,
                  style: Txt.sans(16, peso: FontWeight.w600, cor: CoresMarica.branco),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// `GhostButton`: branco com borda areia, 48px.
class BotaoFantasma extends StatelessWidget {
  const BotaoFantasma({
    required this.rotulo,
    required this.onPressed,
    this.icone,
    this.larguraCheia = true,
    this.cor = CoresMarica.tinta,
    super.key,
  });

  final String rotulo;
  final VoidCallback? onPressed;
  final IconData? icone;
  final bool larguraCheia;
  final Color cor;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: onPressed != null ? 1 : 0.5,
      child: Pressionavel(
        onTap: onPressed,
        semantica: rotulo,
        child: Container(
          constraints: const BoxConstraints(minHeight: 48),
          width: larguraCheia ? double.infinity : null,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            color: CoresMarica.branco,
            borderRadius: BorderRadius.circular(RaiosMarica.x2l),
            border: Border.all(color: CoresMarica.areia),
          ),
          child: Row(
            mainAxisSize: larguraCheia ? MainAxisSize.max : MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (icone != null) ...[
                Icon(icone, size: 16, color: cor),
                const SizedBox(width: 8),
              ],
              Flexible(
                child: Text(
                  rotulo,
                  textAlign: TextAlign.center,
                  style: Txt.sans(14, peso: FontWeight.w600, cor: cor),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Link sublinhado discreto (`text-sm font-medium text-tinta-mute underline`).
class LinkDiscreto extends StatelessWidget {
  const LinkDiscreto({required this.rotulo, required this.onTap, super.key});

  final String rotulo;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(6),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
          child: Text(
            rotulo,
            textAlign: TextAlign.center,
            style: Txt.sans(14, peso: FontWeight.w500, cor: CoresMarica.tintaMute).copyWith(
              decoration: TextDecoration.underline,
              decorationColor: CoresMarica.tintaMute,
            ),
          ),
        ),
      ),
    );
  }
}

/// Borda dos campos (`rounded-2xl border border-areia`, foco `border-lagoa ring-4 ring-lagoa/15`).
InputDecoration decoracaoCampo({String? dica, bool grande = false, EdgeInsetsGeometry? preenchimento}) {
  OutlineInputBorder borda(Color cor, [double largura = 1]) => OutlineInputBorder(
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        borderSide: BorderSide(color: cor, width: largura),
      );
  return InputDecoration(
    hintText: dica,
    hintStyle: grande
        ? Txt.display(26, peso: FontWeight.w400, cor: CoresMarica.tintaMute.withValues(alpha: 0.35))
        : Txt.sans(16, cor: CoresMarica.tintaMute.withValues(alpha: 0.6)),
    filled: true,
    fillColor: CoresMarica.branco,
    isDense: true,
    contentPadding: preenchimento ?? const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
    border: borda(CoresMarica.areia),
    enabledBorder: borda(CoresMarica.areia),
    disabledBorder: borda(CoresMarica.areia),
    focusedBorder: borda(CoresMarica.lagoa, 1.5),
    errorBorder: borda(CoresMarica.marica),
    focusedErrorBorder: borda(CoresMarica.marica, 1.5),
    counterText: '',
  );
}

/// Halo de foco `ring-4 ring-lagoa/15` + `shadow-carta` em volta de um campo.
class HaloFoco extends StatefulWidget {
  const HaloFoco({required this.child, required this.foco, this.sombra = true, super.key});

  final Widget child;
  final FocusNode foco;
  final bool sombra;

  @override
  State<HaloFoco> createState() => _HaloFocoState();
}

class _HaloFocoState extends State<HaloFoco> {
  @override
  void initState() {
    super.initState();
    widget.foco.addListener(_mudou);
  }

  @override
  void dispose() {
    widget.foco.removeListener(_mudou);
    super.dispose();
  }

  void _mudou() => setState(() {});

  @override
  Widget build(BuildContext context) {
    final focado = widget.foco.hasFocus;
    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        boxShadow: [
          if (widget.sombra) ...SombrasMarica.carta,
          if (focado) BoxShadow(color: CoresMarica.lagoa.withValues(alpha: 0.15), spreadRadius: 4),
        ],
      ),
      child: widget.child,
    );
  }
}

/// `Field`: rótulo + campo + dica.
class Campo extends StatefulWidget {
  const Campo({
    required this.rotulo,
    this.controller,
    this.dica,
    this.ajuda,
    this.teclado,
    this.formatadores,
    this.maxLength,
    this.habilitado = true,
    this.autofocus = false,
    this.linhas = 1,
    this.autofill,
    this.aoMudar,
    this.rotuloComplemento,
    super.key,
  });

  final String rotulo;

  /// Parte do rótulo em peso normal (ex.: "(opcional)").
  final String? rotuloComplemento;
  final TextEditingController? controller;
  final String? dica;
  final String? ajuda;
  final TextInputType? teclado;
  final List<TextInputFormatter>? formatadores;
  final int? maxLength;
  final bool habilitado;
  final bool autofocus;
  final int linhas;
  final Iterable<String>? autofill;
  final ValueChanged<String>? aoMudar;

  @override
  State<Campo> createState() => _CampoState();
}

class _CampoState extends State<Campo> {
  final _foco = FocusNode();

  @override
  void dispose() {
    _foco.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(bottom: 6),
          child: Text.rich(
            TextSpan(
              text: widget.rotulo,
              children: [
                if (widget.rotuloComplemento != null)
                  TextSpan(
                    text: ' ${widget.rotuloComplemento}',
                    style: Txt.sans(14, cor: CoresMarica.tintaMute),
                  ),
              ],
            ),
            style: Txt.sans(14, peso: FontWeight.w500),
          ),
        ),
        HaloFoco(
          foco: _foco,
          sombra: false,
          child: TextField(
            controller: widget.controller,
            focusNode: _foco,
            enabled: widget.habilitado,
            autofocus: widget.autofocus,
            keyboardType: widget.teclado,
            inputFormatters: widget.formatadores,
            maxLength: widget.maxLength,
            minLines: widget.linhas,
            maxLines: widget.linhas,
            autofillHints: widget.autofill,
            onChanged: widget.aoMudar,
            style: Txt.sans(16, cor: widget.habilitado ? CoresMarica.tinta : CoresMarica.tintaMute),
            decoration: decoracaoCampo(dica: widget.dica).copyWith(
              fillColor: widget.habilitado ? CoresMarica.branco : CoresMarica.papel,
            ),
          ),
        ),
        if (widget.ajuda != null)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(widget.ajuda!, style: Txt.sans(12, cor: CoresMarica.tintaMute)),
          ),
      ],
    );
  }
}

/// `SectionHeader`: sobretítulo vermelho + título display.
class CabecalhoSecao extends StatelessWidget {
  const CabecalhoSecao({required this.titulo, this.sobretitulo, super.key});

  final String titulo;
  final String? sobretitulo;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (sobretitulo != null) Text(sobretitulo!.toUpperCase(), style: Txt.sobretitulo()),
          Text(titulo, style: Txt.display(20)),
        ],
      ),
    );
  }
}

/// `Skeleton`: bloco areia pulsando.
class Esqueleto extends StatefulWidget {
  const Esqueleto({this.altura = 80, this.largura = double.infinity, super.key});

  final double altura;
  final double largura;

  @override
  State<Esqueleto> createState() => _EsqueletoState();
}

class _EsqueletoState extends State<Esqueleto> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1000),
  )..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: Tween<double>(begin: 1, end: 0.5).animate(CurvedAnimation(parent: _c, curve: Curves.easeInOut)),
      child: Container(
        height: widget.altura,
        width: widget.largura,
        decoration: BoxDecoration(
          color: CoresMarica.areia.withValues(alpha: 0.7),
          borderRadius: BorderRadius.circular(RaiosMarica.xl),
        ),
      ),
    );
  }
}

/// `Loader2 animate-spin` — o ícone girando do PWA.
class Girando extends StatefulWidget {
  const Girando({this.cor = CoresMarica.marica, this.tamanho = 24, super.key});

  final Color cor;
  final double tamanho;

  @override
  State<Girando> createState() => _GirandoState();
}

class _GirandoState extends State<Girando> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1000),
  )..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RotationTransition(
      turns: _c,
      child: Icon(LucideIcons.loaderCircle, color: widget.cor, size: widget.tamanho),
    );
  }
}

/// `EmptyState`: caixa tracejada com ícone na lagoa.
class EstadoVazio extends StatelessWidget {
  const EstadoVazio({
    required this.icone,
    required this.titulo,
    required this.descricao,
    this.acao,
    super.key,
  });

  final IconData icone;
  final String titulo;
  final String descricao;
  final Widget? acao;

  @override
  Widget build(BuildContext context) {
    return CustomPaint(
      painter: const _BordaTracejada(cor: CoresMarica.areia, raio: RaiosMarica.x2l),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 48),
        decoration: BoxDecoration(
          color: CoresMarica.branco.withValues(alpha: 0.6),
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        ),
        child: Column(
          children: [
            Container(
              width: 64,
              height: 64,
              decoration: BoxDecoration(
                color: CoresMarica.lagoaClaro,
                borderRadius: BorderRadius.circular(RaiosMarica.x2l),
              ),
              child: Icon(icone, size: 32, color: CoresMarica.lagoa),
            ),
            const SizedBox(height: 16),
            Text(titulo, textAlign: TextAlign.center, style: Txt.display(18)),
            const SizedBox(height: 4),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 320),
              child: Text(
                descricao,
                textAlign: TextAlign.center,
                style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
              ),
            ),
            if (acao != null) ...[const SizedBox(height: 20), acao!],
          ],
        ),
      ),
    );
  }
}

/// `ErroCard`: cartão avermelhado com "Tentar de novo".
class ErroCard extends StatelessWidget {
  const ErroCard({required this.mensagem, this.aoTentar, super.key});

  final String mensagem;
  final VoidCallback? aoTentar;

  @override
  Widget build(BuildContext context) {
    return Cartao(
      corBorda: CoresMarica.marica.withValues(alpha: 0.2),
      fundo: Color.alphaBlend(CoresMarica.marica.withValues(alpha: 0.03), CoresMarica.branco),
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Text(mensagem, textAlign: TextAlign.center, style: Txt.sans(14, altura: 1.43)),
          if (aoTentar != null) ...[
            const SizedBox(height: 16),
            BotaoFantasma(rotulo: 'Tentar de novo', onPressed: aoTentar, larguraCheia: false),
          ],
        ],
      ),
    );
  }
}

/// Etiqueta de status com tom semântico inferido do texto (`Etiqueta.tsx`).
class Etiqueta extends StatelessWidget {
  const Etiqueta({required this.status, super.key});

  final String status;

  @override
  Widget build(BuildContext context) {
    final s = status.toLowerCase();
    final (Color fundo, Color texto) = RegExp('(pronto|conclu|assinad|dispon|realizad|confirmad)').hasMatch(s)
        ? (CoresMarica.lagoaClaro, CoresMarica.lagoaEscuro)
        : RegExp('(cancel|não pod|nao pod|declin|remarca|não vai|nao vai)').hasMatch(s)
            ? (CoresMarica.vermelho50, CoresMarica.vermelho700)
            : RegExp('(pendente|aguard|process|agendad)').hasMatch(s)
                ? (CoresMarica.ambar50, CoresMarica.ambar700)
                : (CoresMarica.areia.withValues(alpha: 0.6), CoresMarica.tintaMute);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      decoration: BoxDecoration(color: fundo, borderRadius: BorderRadius.circular(999)),
      child: Text(status, style: Txt.sans(12, peso: FontWeight.w600, cor: texto)),
    );
  }
}

/// Caixinha de ícone colorida (`grid h-12 w-12 place-items-center rounded-2xl bg-lagoa-claro`).
class Selo extends StatelessWidget {
  const Selo({
    required this.icone,
    this.tamanho = 48,
    this.tamanhoIcone = 24,
    this.raio = RaiosMarica.x2l,
    this.destaque = false,
    this.fundo,
    this.cor,
    this.girando = false,
    super.key,
  });

  final IconData icone;
  final double tamanho;
  final double tamanhoIcone;
  final double raio;

  /// `true` = tom Maricá (`bg-marica/10 text-marica`); `false` = lagoa.
  final bool destaque;
  final Color? fundo;
  final Color? cor;
  final bool girando;

  @override
  Widget build(BuildContext context) {
    final c = cor ?? (destaque ? CoresMarica.marica : CoresMarica.lagoa);
    return Container(
      width: tamanho,
      height: tamanho,
      decoration: BoxDecoration(
        color: fundo ?? (destaque ? CoresMarica.marica.withValues(alpha: 0.1) : CoresMarica.lagoaClaro),
        borderRadius: BorderRadius.circular(raio),
      ),
      alignment: Alignment.center,
      child: girando ? Girando(cor: c, tamanho: tamanhoIcone) : Icon(icone, size: tamanhoIcone, color: c),
    );
  }
}

/// Aviso âmbar "⚠️ Importante" da guia — igual no Entrar e no modal de agenda confirmada.
class AvisoGuia extends StatelessWidget {
  const AvisoGuia({this.tamanhoTexto = 14, super.key});

  final double tamanhoTexto;

  @override
  Widget build(BuildContext context) {
    final base = Txt.sans(tamanhoTexto, cor: CoresMarica.ambar900, altura: 1.6);
    final negrito = base.copyWith(fontWeight: FontWeight.w700);
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: CoresMarica.ambar50,
        borderRadius: BorderRadius.circular(RaiosMarica.xl),
        border: Border.all(color: CoresMarica.ambar300),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('⚠️ Importante', style: negrito),
          const SizedBox(height: 4),
          Text.rich(
            TextSpan(
              style: base,
              children: [
                const TextSpan(text: 'Antes do dia, passe no '),
                TextSpan(text: 'posto de saúde', style: negrito),
                const TextSpan(text: ' onde você é atendido(a) para retirar a '),
                TextSpan(text: 'guia (ficha de solicitação)', style: negrito),
                const TextSpan(text: '. Sem ela não é possível fazer o atendimento.'),
              ],
            ),
          ),
          const SizedBox(height: 4),
          Text.rich(
            TextSpan(
              style: base,
              children: [
                const TextSpan(text: 'No dia, leve a '),
                TextSpan(text: 'guia', style: negrito),
                const TextSpan(text: ', o '),
                TextSpan(text: 'pedido médico', style: negrito),
                const TextSpan(text: ', o '),
                TextSpan(text: 'cartão do SUS', style: negrito),
                const TextSpan(text: ' e o '),
                TextSpan(text: 'comprovante de residência', style: negrito),
                const TextSpan(text: '.'),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// `animate-rise`: o conteúdo sobe 8px e aparece (350ms, cubic-bezier(.22,.61,.36,1)).
class Subir extends StatelessWidget {
  const Subir({required this.child, this.atraso = Duration.zero, super.key});

  final Widget child;
  final Duration atraso;

  static const Curve curva = Cubic(0.22, 0.61, 0.36, 1);

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: const Duration(milliseconds: 350) + atraso,
      curve: Interval(
        atraso.inMilliseconds / (350 + atraso.inMilliseconds),
        1,
        curve: curva,
      ),
      builder: (_, t, filho) => Opacity(
        opacity: t,
        child: Transform.translate(offset: Offset(0, 8 * (1 - t)), child: filho),
      ),
      child: child,
    );
  }
}

/// Guilloché: textura de linha de segurança do "Cartão do Cidadão" (assinatura visual do PWA).
/// Dois conjuntos de círculos concêntricos finos, brancos e quase transparentes — um saindo do
/// canto inferior esquerdo, outro do superior direito — por cima do gradiente.
class Guilloche extends StatelessWidget {
  const Guilloche({required this.child, this.gradiente, this.cor, this.raio = 0, super.key});

  final Widget child;
  final Gradient? gradiente;
  final Color? cor;
  final double raio;

  @override
  Widget build(BuildContext context) {
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        gradient: gradiente,
        color: cor,
        borderRadius: BorderRadius.circular(raio),
      ),
      child: CustomPaint(painter: const _GuillochePainter(), child: child),
    );
  }
}

class _GuillochePainter extends CustomPainter {
  const _GuillochePainter();

  @override
  void paint(Canvas canvas, Size size) {
    void aneis(Offset centro, double passo, double alfa) {
      final p = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..color = Colors.white.withValues(alpha: alfa);
      final maximo = math.sqrt(size.width * size.width + size.height * size.height) * 1.6;
      for (var r = passo - 0.5; r < maximo; r += passo) {
        canvas.drawCircle(centro, r, p);
      }
    }

    // repeating-radial-gradient(circle at 18% 120%, transparent 0 13px, white/.08 13px 14px)
    aneis(Offset(size.width * 0.18, size.height * 1.2), 14, 0.08);
    // repeating-radial-gradient(circle at 88% -20%, transparent 0 11px, white/.06 11px 12px)
    aneis(Offset(size.width * 0.88, -size.height * 0.2), 12, 0.06);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Borda tracejada (`border border-dashed`).
class BordaTracejada extends StatelessWidget {
  const BordaTracejada({
    required this.child,
    this.cor = CoresMarica.areia,
    this.raio = RaiosMarica.x2l,
    this.largura = 1,
    super.key,
  });

  final Widget child;
  final Color cor;
  final double raio;
  final double largura;

  @override
  Widget build(BuildContext context) =>
      CustomPaint(painter: _BordaTracejada(cor: cor, raio: raio, largura: largura), child: child);
}

class _BordaTracejada extends CustomPainter {
  const _BordaTracejada({required this.cor, required this.raio, this.largura = 1});

  final Color cor;
  final double raio;
  final double largura;

  @override
  void paint(Canvas canvas, Size size) {
    final rr = RRect.fromRectAndRadius(
      Rect.fromLTWH(largura / 2, largura / 2, size.width - largura, size.height - largura),
      Radius.circular(raio),
    );
    final p = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = largura
      ..color = cor;
    final caminho = Path()..addRRect(rr);
    const traco = 5.0;
    const vao = 4.0;
    for (final m in caminho.computeMetrics()) {
      var d = 0.0;
      while (d < m.length) {
        canvas.drawPath(m.extractPath(d, math.min(d + traco, m.length)), p);
        d += traco + vao;
      }
    }
  }

  @override
  bool shouldRepaint(covariant _BordaTracejada old) =>
      old.cor != cor || old.raio != raio || old.largura != largura;
}

/// Linha horizontal tracejada (o "picote" do ticket).
class LinhaTracejada extends StatelessWidget {
  const LinhaTracejada({this.cor = CoresMarica.areia, this.espessura = 2, super.key});

  final Color cor;
  final double espessura;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (_, c) {
        const traco = 6.0;
        const vao = 5.0;
        final n = (c.maxWidth / (traco + vao)).floor();
        return Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: List.generate(
            n,
            (_) => SizedBox(width: traco, height: espessura, child: ColoredBox(color: cor)),
          ),
        );
      },
    );
  }
}

/// Mensagem de erro inline (`text-sm text-marica`).
class TextoErro extends StatelessWidget {
  const TextoErro(this.mensagem, {this.centralizado = false, this.tamanho = 14, super.key});

  final String mensagem;
  final bool centralizado;
  final double tamanho;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      child: Text(
        mensagem,
        textAlign: centralizado ? TextAlign.center : TextAlign.start,
        style: Txt.sans(tamanho, cor: CoresMarica.marica, altura: 1.43),
      ),
    );
  }
}

/// Abre uma folha inferior no estilo do PWA (`rounded-t-3xl bg-papel`, fundo `tinta/40`).
Future<T?> abrirFolha<T>(
  BuildContext context, {
  required WidgetBuilder builder,
  bool dispensavel = true,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    isDismissible: dispensavel,
    enableDrag: dispensavel,
    useSafeArea: true,
    backgroundColor: CoresMarica.papel,
    barrierColor: CoresMarica.tinta.withValues(alpha: 0.4),
    constraints: const BoxConstraints(maxWidth: 460),
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(RaiosMarica.x3l)),
    ),
    builder: (ctx) => Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(ctx).bottom),
      child: builder(ctx),
    ),
  );
}
