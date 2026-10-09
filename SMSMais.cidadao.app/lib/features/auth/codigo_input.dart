import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';

/// Entrada de código OTP em caixinhas segmentadas (uma por dígito), estilo app — `CodigoInput.tsx`.
///
/// Por baixo há UM campo só (invisível, por cima das caixinhas): assim o teclado do celular faz
/// sozinho o que no PWA precisou de código — auto-avanço, Backspace voltando, colar o código
/// inteiro e o preenchimento automático do código recebido (`oneTimeCode`). As caixinhas só
/// desenham o que foi digitado e qual é a próxima posição.
class CodigoInput extends StatefulWidget {
  const CodigoInput({
    required this.valor,
    required this.aoMudar,
    this.tamanho = 6,
    this.autofocus = false,
    this.rotulo = 'Código de acesso',
    super.key,
  });

  /// Dígitos já digitados (sem buracos).
  final String valor;
  final ValueChanged<String> aoMudar;
  final int tamanho;
  final bool autofocus;
  final String rotulo;

  @override
  State<CodigoInput> createState() => _CodigoInputState();
}

class _CodigoInputState extends State<CodigoInput> {
  late final TextEditingController _controle = TextEditingController(text: widget.valor);
  final _foco = FocusNode();

  @override
  void initState() {
    super.initState();
    _foco.addListener(() => setState(() {}));
  }

  @override
  void didUpdateWidget(covariant CodigoInput antigo) {
    super.didUpdateWidget(antigo);
    // Valor trocado por fora (ex.: código do modo de teste) — reflete no campo.
    if (widget.valor != _controle.text) {
      _controle.value = TextEditingValue(
        text: widget.valor,
        selection: TextSelection.collapsed(offset: widget.valor.length),
      );
    }
  }

  @override
  void dispose() {
    _controle.dispose();
    _foco.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final atual = widget.valor.length.clamp(0, widget.tamanho - 1);
    return Semantics(
      label: widget.rotulo,
      textField: true,
      child: Stack(
        alignment: Alignment.center,
        children: [
          // 48px por caixa como no PWA; em celular estreito (360dp: 6×48 + 5×10 = 338 > 312 úteis)
          // as caixas encolhem para caber, em vez de vazar pela direita.
          LayoutBuilder(
            builder: (context, c) {
              const vao = 10.0;
              final largura = ((c.maxWidth - vao * (widget.tamanho - 1)) / widget.tamanho).clamp(36.0, 48.0);
              return Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  for (var i = 0; i < widget.tamanho; i++) ...[
                    if (i > 0) const SizedBox(width: vao),
                    _Caixa(
                      digito: i < widget.valor.length ? widget.valor[i] : '',
                      focada: _foco.hasFocus && i == atual,
                      largura: largura,
                    ),
                  ],
                ],
              );
            },
          ),
          Positioned.fill(
            child: TextField(
              controller: _controle,
              focusNode: _foco,
              autofocus: widget.autofocus,
              keyboardType: TextInputType.number,
              autofillHints: const [AutofillHints.oneTimeCode],
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(widget.tamanho),
              ],
              onChanged: widget.aoMudar,
              showCursor: false,
              enableSuggestions: false,
              autocorrect: false,
              style: const TextStyle(color: Colors.transparent, fontSize: 1),
              decoration: const InputDecoration(
                border: InputBorder.none,
                counterText: '',
                contentPadding: EdgeInsets.zero,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Caixa extends StatelessWidget {
  const _Caixa({required this.digito, required this.focada, required this.largura});

  final String digito;
  final bool focada;
  final double largura;

  @override
  Widget build(BuildContext context) {
    final preenchida = digito.isNotEmpty;
    final Color borda;
    if (focada) {
      borda = CoresMarica.lagoa;
    } else if (preenchida) {
      borda = CoresMarica.marica.withValues(alpha: 0.4);
    } else {
      borda = CoresMarica.areia;
    }
    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      width: largura,
      height: 56,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: CoresMarica.branco,
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        border: Border.all(color: borda, width: focada ? 1.5 : 1),
        boxShadow: [
          if (preenchida) ...SombrasMarica.carta,
          if (focada) BoxShadow(color: CoresMarica.lagoa.withValues(alpha: 0.15), spreadRadius: 4),
        ],
      ),
      child: Text(digito, style: Txt.display(24)),
    );
  }
}
