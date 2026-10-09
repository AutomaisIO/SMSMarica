import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/auth/auth_shell.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Passo 1 do login (`Login.tsx`): o CPF. O código só sai quando o WhatsApp do cadastro está
/// VERIFICADO (situação "otp"). Sem verificação ("verificacao") ou sem cadastro ("cadastro"), o
/// backend não envia nada e a pessoa passa pela tela de confirmação de dados. `codigoTeste` só
/// vem como reserva se o servidor não estiver com o WhatsApp configurado (aí aparece na tela).
class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _cpf = TextEditingController();
  final _foco = FocusNode();
  bool _enviando = false;
  String? _erro;

  String get _cpfLimpo => soDigitos(_cpf.text);
  bool get _valido => _cpfLimpo.length == 11;

  @override
  void dispose() {
    _cpf.dispose();
    _foco.dispose();
    super.dispose();
  }

  Future<void> _solicitar() async {
    if (!_valido || _enviando) return;
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      final r = await ref.read(apiProvider).solicitarOtp(_cpfLimpo);
      if (!mounted) return;
      if (r.situacao == 'otp') {
        // Sem `await`: o botão sai do "carregando" já na ida (como o PWA), não só na volta.
        context.push('/login/codigo', extra: <String, Object?>{
          'cpf': _cpfLimpo,
          'codigoTeste': r.codigoTeste,
          'telefoneMascarado': r.telefoneMascarado,
        }).ignore();
      } else {
        context.push('/login/verificacao', extra: <String, String?>{
          'cpf': _cpfLimpo,
          'situacao': r.situacao,
          'telefoneMascarado': r.telefoneMascarado,
        }).ignore();
      }
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AuthShell(
      titulo: 'Entrar',
      subtitulo: 'Informe seu CPF. Enviaremos um código de acesso pelo WhatsApp do número cadastrado na Saúde.',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'SEU CPF',
            textAlign: TextAlign.center,
            style: Txt.sobretitulo(cor: CoresMarica.tintaMute, espacamentoEm: 0.22),
          ),
          const SizedBox(height: 8),
          HaloFoco(
            foco: _foco,
            child: TextField(
              controller: _cpf,
              focusNode: _foco,
              autofocus: true,
              keyboardType: TextInputType.number,
              textAlign: TextAlign.center,
              inputFormatters: [MascaraFormatter(mascararCpf)],
              onChanged: (_) => setState(() {}),
              onSubmitted: (_) => _solicitar(),
              style: Txt.display(26, espacamento: 26 * 0.06).copyWith(fontFeatures: Txt.tabular),
              decoration: decoracaoCampo(
                dica: '000.000.000-00',
                grande: true,
                preenchimento: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
              ),
            ),
          ),
          const SizedBox(height: 20),
          if (_erro != null) ...[
            TextoErro(_erro!, centralizado: true),
            const SizedBox(height: 20),
          ],
          BotaoPrimario(
            rotulo: 'Receber código',
            icone: LucideIcons.messageCircle,
            carregando: _enviando,
            onPressed: _valido ? _solicitar : null,
          ),
          const SizedBox(height: 20),
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 272),
              child: Text(
                'É a sua primeira vez? Informe o CPF do mesmo jeito — confirmamos seus dados no passo seguinte.',
                textAlign: TextAlign.center,
                style: Txt.sans(12, cor: CoresMarica.tintaMute, altura: 1.625),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
