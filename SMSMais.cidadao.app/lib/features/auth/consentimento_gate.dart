import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/sessao/sair.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Situação do termo LGPD para a sessão atual. Consultado UMA vez por sessão: o `ShellRoute`
/// reconstrói o portão a cada troca de tela, e o provider (que só se refaz quando o token muda)
/// evita perguntar de novo ao servidor a cada toque no menu.
class ConsentimentoController extends AsyncNotifier<ConsentimentoStatus> {
  @override
  Future<ConsentimentoStatus> build() {
    final token = ref.watch(sessaoProvider.select((s) => s?.token));
    // Sem sessão o portão não aparece (o roteador já foi para o login): não pergunta nada.
    if (token == null) return Completer<ConsentimentoStatus>().future;
    return ref.read(apiProvider).consentimento();
  }

  Future<void> aceitar() async {
    await ref.read(apiProvider).aceitarConsentimento();
    final atual = state.valueOrNull;
    state = AsyncData(
      ConsentimentoStatus(versao: atual?.versao ?? '', texto: atual?.texto ?? '', aceito: true),
    );
  }
}

final consentimentoProvider =
    AsyncNotifierProvider<ConsentimentoController, ConsentimentoStatus>(ConsentimentoController.new);

/// 401 = sessão expirada/revogada: o interceptor já desloga e o roteador leva ao login.
bool _sessaoInvalida(Object e) => e is DioException && e.response?.statusCode == 401;

/// Portão de consentimento LGPD (`ConsentGate.tsx`): antes de liberar o app, confere se o cidadão
/// já aceitou o termo vigente. Se não, mostra o termo em tela cheia (bloqueante) — só libera após
/// o aceite. O backend também barra cada requisição sem consentimento (defesa em profundidade).
class ConsentimentoGate extends ConsumerStatefulWidget {
  const ConsentimentoGate({required this.child, super.key});

  final Widget child;

  @override
  ConsumerState<ConsentimentoGate> createState() => _ConsentimentoGateState();
}

class _ConsentimentoGateState extends ConsumerState<ConsentimentoGate> {
  bool _enviando = false;
  String? _erro;

  Future<void> _aceitar() async {
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      await ref.read(consentimentoProvider.notifier).aceitar();
    } on Object catch (e) {
      if (_sessaoInvalida(e)) return; // interceptor leva ao login
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(consentimentoProvider);
    final termo = estado.valueOrNull;
    if (termo != null && termo.aceito) return widget.child;

    final erroCarga = estado.hasError && !estado.isLoading ? estado.error : null;
    // Carregando — ou sessão inválida: segura na tela vazia, o interceptor redireciona ao login
    // (não deixa a pessoa presa no "Li e concordo").
    if (termo == null && (erroCarga == null || _sessaoInvalida(erroCarga))) {
      return const ColoredBox(color: CoresMarica.areia, child: SizedBox.expand());
    }

    // Na dúvida (erro ao consultar), NÃO libera o app: mostra o termo pendente com o erro.
    final erro = _erro ?? (erroCarga != null ? extrairMensagemDeErro(erroCarga) : null);

    // Título = 1ª linha; resto = parágrafos separados por linha em branco.
    final linhas = (termo?.texto ?? '').split('\n\n');
    final titulo = linhas.first.isNotEmpty ? linhas.first : 'Termo de Consentimento';
    final paragrafos = linhas.skip(1).toList();
    final padding = MediaQuery.paddingOf(context);

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.areia,
        body: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ColoredBox(
              color: CoresMarica.marica,
              child: Padding(
                padding: EdgeInsets.fromLTRB(20, padding.top + 16, 20, 16),
                child: Row(
                  children: [
                    const Icon(LucideIcons.shieldCheck, size: 24, color: CoresMarica.branco),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Privacidade e seus dados', style: Txt.display(15, cor: CoresMarica.branco, altura: 1.25)),
                          Text(
                            'Leia e confirme para continuar',
                            style: Txt.sans(11, cor: CoresMarica.branco.withValues(alpha: 0.8), altura: 1.25),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  Text(titulo, style: Txt.display(18)),
                  const SizedBox(height: 12),
                  for (final p in paragrafos) ...[
                    Text(p, style: Txt.sans(14, altura: 1.625)),
                    const SizedBox(height: 12),
                  ],
                ],
              ),
            ),
            DecoratedBox(
              decoration: const BoxDecoration(
                color: CoresMarica.branco,
                border: Border(top: BorderSide(color: CoresMarica.areia)),
              ),
              child: Padding(
                padding: EdgeInsets.fromLTRB(20, 16, 20, 16 + padding.bottom),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (erro != null) ...[TextoErro(erro, centralizado: true), const SizedBox(height: 12)],
                    BotaoPrimario(rotulo: 'Li e concordo', carregando: _enviando, onPressed: _aceitar),
                    const SizedBox(height: 12),
                    LinkDiscreto(rotulo: 'Não concordo e quero sair', onTap: () => sairDoApp(ref)),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
