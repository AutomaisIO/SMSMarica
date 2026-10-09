import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sms_mais_cidadao/features/auth/auth_shell.dart';
import 'package:sms_mais_cidadao/features/auth/codigo_input.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Passo final do login (`Otp.tsx`): o código que chegou pelo WhatsApp.
class OtpPage extends ConsumerStatefulWidget {
  const OtpPage({required this.dados, super.key});

  /// 'cpf', 'codigoTeste', 'telefoneMascarado', 'semTroca' (bool).
  final Map<String, Object?>? dados;

  @override
  ConsumerState<OtpPage> createState() => _OtpPageState();
}

class _OtpPageState extends ConsumerState<OtpPage> {
  late String _codigo = _codigoTeste ?? '';
  bool _enviando = false;
  String? _erro;

  String? get _cpf => widget.dados?['cpf'] as String?;
  String? get _codigoTeste => widget.dados?['codigoTeste'] as String?;
  String? get _telefoneMascarado => widget.dados?['telefoneMascarado'] as String?;

  /// O código acabou de ser enviado para um número que a própria pessoa digitou: oferecer
  /// "trocar de número" aqui seria um laço sem fim.
  bool get _podeTrocarNumero => _codigoTeste == null && widget.dados?['semTroca'] != true;

  bool get _valido => soDigitos(_codigo).length >= 4;

  @override
  void initState() {
    super.initState();
    if (_cpf == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) context.go('/login');
      });
    }
  }

  Future<void> _validar() async {
    if (!_valido || _enviando) return;
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      final r = await ref.read(apiProvider).validarOtp(_cpf!, soDigitos(_codigo));
      await ref.read(sessaoProvider.notifier).entrar(r.token, r.paciente);
      if (mounted) context.go('/');
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_cpf == null) return const SizedBox.shrink();
    // Últimos dígitos do destino — é como a pessoa reconhece o aparelho que vai tocar.
    final finalDoNumero = soDigitos(_telefoneMascarado);
    final codigoTeste = _codigoTeste;

    return AuthShell(
      titulo: 'Código de acesso',
      subtitulo: 'Digite o código que enviamos pelo WhatsApp.',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (codigoTeste != null) ...[
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: CoresMarica.ambar50,
                borderRadius: BorderRadius.circular(RaiosMarica.x2l),
                border: Border.all(color: CoresMarica.ambar300),
              ),
              child: Text.rich(
                TextSpan(
                  style: Txt.sans(14, cor: CoresMarica.ambar800, altura: 1.43),
                  children: [
                    const TextSpan(text: 'Modo de teste — envio por WhatsApp ainda não ativo.\nSeu código: '),
                    TextSpan(
                      text: codigoTeste,
                      style: const TextStyle(
                        fontFamily: FontesMarica.mono,
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                        letterSpacing: 20 * 0.3,
                        color: CoresMarica.ambar800,
                      ),
                    ),
                  ],
                ),
                textAlign: TextAlign.center,
              ),
            ),
            const SizedBox(height: 24),
          ] else if (finalDoNumero.isNotEmpty) ...[
            Text.rich(
              TextSpan(
                style: Txt.sans(14, cor: CoresMarica.tintaMute),
                children: [
                  const TextSpan(text: 'Enviado para o WhatsApp final '),
                  TextSpan(
                    text: finalDoNumero,
                    style: Txt.display(18, espacamento: 18 * 0.12).copyWith(fontFeatures: Txt.tabular),
                  ),
                  const TextSpan(text: '.'),
                ],
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
          ],
          CodigoInput(
            valor: _codigo,
            autofocus: codigoTeste == null,
            aoMudar: (v) => setState(() => _codigo = v),
          ),
          const SizedBox(height: 24),
          if (_erro != null) ...[
            TextoErro(_erro!, centralizado: true),
            const SizedBox(height: 24),
          ],
          BotaoPrimario(rotulo: 'Entrar', carregando: _enviando, onPressed: _valido ? _validar : null),
          const SizedBox(height: 24),
          // Quem já tem número verificado NÃO troca de número por aqui: seria a porta para alguém
          // com a guia de papel apontar o contato para o próprio celular. Em vez de mandar a
          // pessoa a um formulário que vai recusar, já se diz o caminho.
          if (_podeTrocarNumero) ...[
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              decoration: BoxDecoration(
                color: CoresMarica.branco.withValues(alpha: 0.7),
                borderRadius: BorderRadius.circular(RaiosMarica.x2l),
                border: Border.all(color: CoresMarica.areia),
              ),
              child: Text.rich(
                TextSpan(
                  style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.625),
                  children: [
                    const TextSpan(text: 'Não tem mais esse número? Para trocar, procure o '),
                    TextSpan(text: 'posto de saúde', style: Txt.sans(14, peso: FontWeight.w700, cor: CoresMarica.tintaMute)),
                    const TextSpan(text: ' onde você é atendido(a), com um documento com foto.'),
                  ],
                ),
                textAlign: TextAlign.center,
              ),
            ),
            const SizedBox(height: 24),
          ],
          LinkDiscreto(rotulo: 'Trocar CPF', onTap: () => context.go('/login')),
        ],
      ),
    );
  }
}
