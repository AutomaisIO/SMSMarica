import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sms_mais_cidadao/features/auth/auth_shell.dart';
import 'package:sms_mais_cidadao/features/auth/entrada_erros.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Passo 1 do login (`Login.tsx`, ADR-0078): CPF e data de nascimento, e nada mais. O servidor
/// confere o par (na ficha ou, sem cadastro, na Receita) e devolve os números que já conhece, só
/// com o final — a pessoa escolhe o seu no passo seguinte. Errou, a resposta é sempre a mesma ("não
/// conseguimos confirmar os seus dados"), para não ajudar quem tenta adivinhar.
class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _cpf = TextEditingController();
  final _nascimento = TextEditingController();
  final _focoCpf = FocusNode();
  final _focoNascimento = FocusNode();
  bool _enviando = false;
  String? _erro;

  String get _cpfLimpo => soDigitos(_cpf.text);
  bool get _cpfErrado => _cpfLimpo.length == 11 && !cpfValido(_cpfLimpo);
  String? get _iso => dataParaIso(_nascimento.text);
  bool get _dataErrada => _nascimento.text.length == 10 && _iso == null;
  bool get _valido => _cpfLimpo.length == 11 && !_cpfErrado && _iso != null;

  @override
  void dispose() {
    _cpf.dispose();
    _nascimento.dispose();
    _focoCpf.dispose();
    _focoNascimento.dispose();
    super.dispose();
  }

  Future<void> _continuar() async {
    final iso = _iso;
    if (!_valido || iso == null || _enviando) return;
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      final entrada = await ref.read(apiProvider).entrada(_cpfLimpo, iso);
      if (!mounted) return;
      // Sem `await`: o botão sai do "carregando" já na ida (como o PWA), não só na volta. A volta
      // pode trazer um aviso (o tempo para entrar acabou) — os campos continuam preenchidos.
      context
          .push<String>('/login/telefone', extra: <String, Object?>{'cpf': _cpfLimpo, 'entrada': entrada})
          .then((aviso) {
            if (aviso != null && mounted) setState(() => _erro = aviso);
          })
          .ignore();
    } on Object catch (e) {
      if (mounted) setState(() => _erro = mensagemDaEntrada(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  /// Rótulo + campo grande centralizado (o do CPF e o da data são iguais, como no PWA).
  Widget _campo({
    required String rotulo,
    required TextEditingController controller,
    required FocusNode foco,
    required String dica,
    required String Function(String) mascara,
    required String? erro,
    bool autofocus = false,
    Iterable<String>? autofill,
    TextInputAction? acao,
    VoidCallback? aoConfirmar,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          rotulo.toUpperCase(),
          textAlign: TextAlign.center,
          style: Txt.sobretitulo(cor: CoresMarica.tintaMute, espacamentoEm: 0.22),
        ),
        const SizedBox(height: 8),
        HaloFoco(
          foco: foco,
          child: TextField(
            controller: controller,
            focusNode: foco,
            autofocus: autofocus,
            keyboardType: TextInputType.number,
            textInputAction: acao,
            textAlign: TextAlign.center,
            autofillHints: autofill,
            inputFormatters: [MascaraFormatter(mascara)],
            onChanged: (_) => setState(() {}),
            onSubmitted: (_) => aoConfirmar?.call(),
            style: Txt.display(26, espacamento: 26 * 0.06).copyWith(fontFeatures: Txt.tabular),
            decoration: decoracaoCampo(
              dica: dica,
              grande: true,
              preenchimento: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
            ),
          ),
        ),
        if (erro != null) ...[
          const SizedBox(height: 8),
          TextoErro(erro, centralizado: true, tamanho: 12),
        ],
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return AuthShell(
      titulo: 'Entrar',
      subtitulo:
          'Informe seu CPF e sua data de nascimento. Enviaremos um código de acesso pelo WhatsApp do número cadastrado na Saúde.',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _campo(
            rotulo: 'Seu CPF',
            controller: _cpf,
            foco: _focoCpf,
            dica: '000.000.000-00',
            mascara: mascararCpf,
            erro: _cpfErrado ? 'CPF inválido. Confira os números.' : null,
            autofocus: true,
            acao: TextInputAction.next,
            aoConfirmar: _focoNascimento.requestFocus,
          ),
          const SizedBox(height: 20),
          _campo(
            rotulo: 'Data de nascimento',
            controller: _nascimento,
            foco: _focoNascimento,
            dica: 'dd/mm/aaaa',
            mascara: mascararData,
            erro: _dataErrada ? 'Data inválida. Confira o dia, o mês e o ano.' : null,
            autofill: const [AutofillHints.birthday],
            acao: TextInputAction.done,
            aoConfirmar: _continuar,
          ),
          const SizedBox(height: 20),
          if (_erro != null) ...[
            TextoErro(_erro!, centralizado: true),
            const SizedBox(height: 20),
          ],
          BotaoPrimario(
            rotulo: 'Continuar',
            carregando: _enviando,
            onPressed: _valido ? _continuar : null,
          ),
          const SizedBox(height: 20),
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 272),
              child: Text(
                'É a sua primeira vez? Entre do mesmo jeito — procuramos o seu WhatsApp no cadastro da Saúde.',
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
