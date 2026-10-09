import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Desafio de CPF dos links que carregam RESULTADO (exame, laudo) — `ConfirmarCpf.tsx`. Sem ele,
/// possuir o link do WhatsApp era a credencial inteira — quem o recebesse por engano entrava no
/// prontuário alheio.
///
/// Deliberadamente NÃO usa o `AuthShell`: aquela moldura é a do login (hero vinho + logo + "App do
/// Cidadão"), e o paciente que clicou no link do exame não está tentando fazer login — repetir a
/// cara do login faria ele achar que errou o caminho. Aqui a cor é a `lagoa` (afordância clínica)
/// e a fala é curta, grande e sem tecniquês: o público é idoso e de baixa escolaridade.
class ConfirmarCpf extends StatefulWidget {
  const ConfirmarCpf({
    required this.aoConfirmar,
    required this.enviando,
    required this.erro,
    required this.tentativasRestantes,
    super.key,
  });

  final ValueChanged<String> aoConfirmar;
  final bool enviando;
  final String? erro;
  final int? tentativasRestantes;

  @override
  State<ConfirmarCpf> createState() => _ConfirmarCpfState();
}

class _ConfirmarCpfState extends State<ConfirmarCpf> {
  final _cpf = TextEditingController();
  final _foco = FocusNode();

  String get _limpo => soDigitos(_cpf.text);

  @override
  void dispose() {
    _cpf.dispose();
    _foco.dispose();
    super.dispose();
  }

  void _enviar() {
    if (_limpo.length == 11 && !widget.enviando) widget.aoConfirmar(_limpo);
  }

  @override
  Widget build(BuildContext context) {
    final tentativas = widget.tentativasRestantes;
    final padding = MediaQuery.paddingOf(context);
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.papel,
        body: LayoutBuilder(
          builder: (context, c) => SingleChildScrollView(
            padding: EdgeInsets.fromLTRB(24, padding.top + 56, 24, padding.bottom + 32),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: c.maxHeight - padding.top - padding.bottom - 88),
              child: Center(
                child: Subir(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Center(
                        child: Selo(
                          icone: LucideIcons.shieldCheck,
                          tamanho: 80,
                          tamanhoIcone: 40,
                          raio: 999,
                        ),
                      ),
                      const SizedBox(height: 28),
                      Text('Digite seu CPF', textAlign: TextAlign.center, style: Txt.display(30, altura: 1.25)),
                      const SizedBox(height: 12),
                      Center(
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 304),
                          child: Text(
                            'É para ter certeza de que só você vê o seu exame.',
                            textAlign: TextAlign.center,
                            style: Txt.sans(17, cor: CoresMarica.tintaMute, altura: 1.625),
                          ),
                        ),
                      ),
                      const SizedBox(height: 32),
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
                          onSubmitted: (_) => _enviar(),
                          style: Txt.display(28, espacamento: 28 * 0.06).copyWith(fontFeatures: Txt.tabular),
                          decoration: decoracaoCampo(
                            dica: '000.000.000-00',
                            grande: true,
                            preenchimento: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
                          ).copyWith(
                            hintStyle: Txt.display(
                              28,
                              peso: FontWeight.w400,
                              cor: CoresMarica.tintaMute.withValues(alpha: 0.35),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 20),
                      if (widget.erro != null) ...[
                        Semantics(
                          liveRegion: true,
                          child: Text.rich(
                            TextSpan(
                              style: Txt.sans(15, peso: FontWeight.w500, cor: CoresMarica.marica, altura: 1.625),
                              children: [
                                TextSpan(text: widget.erro),
                                if (tentativas != null && tentativas > 0)
                                  TextSpan(
                                    text: tentativas == 1 ? '\nResta 1 tentativa.' : '\nRestam $tentativas tentativas.',
                                    style: Txt.sans(15, cor: CoresMarica.tintaMute, altura: 1.625),
                                  ),
                              ],
                            ),
                            textAlign: TextAlign.center,
                          ),
                        ),
                        const SizedBox(height: 20),
                      ],
                      BotaoPrimario(
                        rotulo: 'Ver meu exame',
                        carregando: widget.enviando,
                        onPressed: _limpo.length == 11 ? _enviar : null,
                      ),
                      const SizedBox(height: 28),
                      Center(
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 288),
                          child: Text(
                            'Não conseguiu? Procure a unidade de saúde onde fez o exame.',
                            textAlign: TextAlign.center,
                            style: Txt.sans(13, cor: CoresMarica.tintaMute, altura: 1.625),
                          ),
                        ),
                      ),
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
}
