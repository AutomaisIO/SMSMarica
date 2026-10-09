import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/auth/auth_shell.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Passo 2 do login de quem NÃO tem WhatsApp verificado no cadastro (`Verificacao.tsx`). Prova de
/// identidade + o telefone que vai receber o código. Quem já tem cadastro informa o nº da
/// solicitação; quem ainda não tem cai na conferência do CPF na Receita (o backend decide).
///
/// Quem JÁ tem um número verificado não passa por aqui: trocar o número exige ir ao posto (LGPD —
/// ADR-0057). O backend recusa com "telefone.troca_no_posto" e a mensagem aparece no lugar do erro.
class VerificacaoPage extends ConsumerStatefulWidget {
  const VerificacaoPage({required this.dados, super.key});

  /// 'cpf', 'situacao', 'telefoneMascarado' — vindos do Login.
  final Map<String, String?>? dados;

  @override
  ConsumerState<VerificacaoPage> createState() => _VerificacaoPageState();
}

class _VerificacaoPageState extends ConsumerState<VerificacaoPage> {
  final _nascimento = TextEditingController();
  final _solicitacao = TextEditingController();
  final _telefone = TextEditingController();
  bool _enviando = false;
  String? _erro;

  String? get _cpf => widget.dados?['cpf'];
  bool get _cadastroNovo => widget.dados?['situacao'] == 'cadastro';
  String? get _telefoneDoCadastro => widget.dados?['telefoneMascarado'];

  @override
  void initState() {
    super.initState();
    if (_cpf == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) context.go('/login');
      });
    }
  }

  @override
  void dispose() {
    _nascimento.dispose();
    _solicitacao.dispose();
    _telefone.dispose();
    super.dispose();
  }

  String? get _iso => dataParaIso(_nascimento.text);
  bool get _telefoneOk => soDigitos(_telefone.text).length >= 10;
  bool get _valido => _iso != null && _telefoneOk && (_cadastroNovo || soDigitos(_solicitacao.text).isNotEmpty);

  Future<void> _enviar() async {
    final iso = _iso;
    if (iso == null || !_valido || _enviando) return;
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      final r = await ref.read(apiProvider).solicitarOtpVerificacao(
            cpf: _cpf!,
            dataNascimento: iso,
            codigoSolicitacao: _cadastroNovo ? null : soDigitos(_solicitacao.text),
            telefone: normalizarCelularBr(_telefone.text),
          );
      if (!mounted) return;
      // O código acabou de ir para um número que a própria pessoa digitou: na tela do código não
      // se oferece "trocar de número" (seria um laço sem fim).
      context.pushReplacement('/login/codigo', extra: <String, Object?>{
        'cpf': _cpf,
        'codigoTeste': r.codigoTeste,
        'telefoneMascarado': r.telefoneMascarado,
        'semTroca': true,
      });
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_cpf == null) return const SizedBox.shrink();
    final telefoneDoCadastro = _telefoneDoCadastro;
    return AuthShell(
      titulo: 'Confirme seus dados',
      subtitulo: _cadastroNovo
          ? 'Não encontramos seu CPF na Saúde de Maricá. Confirme seus dados para receber o código no WhatsApp.'
          : 'Seu WhatsApp ainda não foi confirmado. Informe os dados abaixo para receber o código com segurança.',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Campo(
            rotulo: 'Data de nascimento',
            controller: _nascimento,
            dica: 'dd/mm/aaaa',
            teclado: TextInputType.number,
            autofocus: true,
            autofill: const [AutofillHints.birthday],
            formatadores: [MascaraFormatter(mascararData)],
            aoMudar: (_) => setState(() {}),
          ),
          if (!_cadastroNovo) ...[
            const SizedBox(height: 16),
            Campo(
              rotulo: 'Nº da solicitação',
              controller: _solicitacao,
              dica: 'Somente números',
              teclado: TextInputType.number,
              formatadores: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(20)],
              ajuda: 'Está no papel do seu exame ou consulta marcada. Não tem em mãos? Procure a sua unidade.',
              aoMudar: (_) => setState(() {}),
            ),
          ],
          const SizedBox(height: 16),
          Campo(
            rotulo: 'Seu WhatsApp',
            controller: _telefone,
            dica: '(21) 99999-0000',
            teclado: TextInputType.phone,
            autofill: const [AutofillHints.telephoneNumber],
            formatadores: [MascaraFormatter(mascararTelefone)],
            ajuda: telefoneDoCadastro != null
                ? 'O número do seu cadastro termina em ${soDigitos(telefoneDoCadastro)}. Confirme-o ou informe o número atual.'
                : 'É neste número que você vai receber o código.',
            aoMudar: (_) => setState(() {}),
          ),
          const SizedBox(height: 16),
          if (_erro != null) ...[
            TextoErro(_erro!, centralizado: true),
            const SizedBox(height: 16),
          ],
          BotaoPrimario(
            rotulo: 'Receber código',
            icone: LucideIcons.messageCircle,
            carregando: _enviando,
            onPressed: _valido ? _enviar : null,
          ),
          const SizedBox(height: 12),
          LinkDiscreto(rotulo: 'Voltar', onTap: () => context.go('/login')),
        ],
      ),
    );
  }
}
