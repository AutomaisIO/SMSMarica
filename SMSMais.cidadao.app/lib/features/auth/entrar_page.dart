import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/auth/confirmar_cpf.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/sessao/sair.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

enum _Fase { trocando, cpf, expirado, confirmado }

/// Magic-link (`Entrar.tsx`): troca o token do link do WhatsApp por uma sessão.
///
/// Links de agendamento seguem em 1 toque. Links de RESULTADO (exame liberado, laudo pronto)
/// passam pelo desafio de CPF: o backend não devolve JWT nem destino enquanto o titular não se
/// identificar, e não consome o token. Uso único — se já usado/expirado, cai no Início (se já
/// logado) ou no login.
class EntrarPage extends ConsumerStatefulWidget {
  const EntrarPage({required this.token, super.key});

  final String token;

  @override
  ConsumerState<EntrarPage> createState() => _EntrarPageState();
}

class _EntrarPageState extends ConsumerState<EntrarPage> {
  _Fase _fase = _Fase.trocando;
  bool _enviando = false;
  String? _erroCpf;
  int? _tentativas;
  late ConfirmacaoAgendamento _confirmacao;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _trocar());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _trocar() async {
    try {
      final r = await ref.read(apiProvider).magic(widget.token);
      if (!mounted) return;
      if (r.requerConfirmacaoCpf) {
        setState(() {
          _tentativas = r.tentativasRestantes;
          _fase = _Fase.cpf;
        });
        return;
      }
      if (r.token != null && r.paciente != null) {
        await _concluir(r);
        return;
      }
      _semSessao(r.destino, r.confirmacaoAgendamento);
    } on Object {
      // Token inexistente/queimado (410) ou erro de rede.
      if (mounted) _semSessao();
    }
  }

  /// Conclui a troca bem-sucedida: autentica e vai ao destino. O destino é um caminho do PWA
  /// (ex.: "/exames?exame=…") — as rotas do app são as mesmas.
  Future<void> _concluir(RespostaMagic r) async {
    if (r.confirmacaoAgendamento != null) {
      ref.read(confirmacaoPendenteProvider.notifier).state = r.confirmacaoAgendamento;
    }
    await ref.read(sessaoProvider.notifier).entrar(r.token!, r.paciente!);
    if (mounted) context.go(r.destino.isEmpty ? '/' : r.destino);
  }

  /// Token gasto/inexistente: nunca autentica. Aparelho com sessão abre o app; senão, login.
  /// EXCEÇÃO: se o backend devolveu a confirmação do agendamento (2º toque no botão "Sim!
  /// Confirmo"), mostramos "presença confirmada" e paramos aí — mandar essa pessoa para o
  /// login/código seria uma barreira sem sentido: ela já provou quem é para receber a mensagem.
  void _semSessao([String? destino, ConfirmacaoAgendamento? confirmada]) {
    if (confirmada != null) {
      setState(() {
        _confirmacao = confirmada;
        _fase = _Fase.confirmado;
      });
      return;
    }
    if (ref.read(sessaoProvider) != null) {
      context.go((destino?.isEmpty ?? true) ? '/' : destino!);
      return;
    }
    setState(() => _fase = _Fase.expirado);
    _timer = Timer(const Duration(milliseconds: 3200), () {
      if (mounted) context.go('/login');
    });
  }

  Future<void> _confirmarCpf(String cpf) async {
    setState(() {
      _enviando = true;
      _erroCpf = null;
    });
    try {
      final r = await ref.read(apiProvider).magicConfirmar(widget.token, cpf);
      if (!mounted) return;
      if (r.token != null && r.paciente != null) {
        await _concluir(r);
        return;
      }
      // Ainda no desafio = CPF não conferiu. A mensagem nunca revela de quem é o exame.
      setState(() {
        _tentativas = r.tentativasRestantes;
        _erroCpf = 'Esse CPF não confere.';
      });
    } on Object {
      // 410 — o link foi queimado na última tentativa.
      if (!mounted) return;
      setState(() => _fase = _Fase.expirado);
      _timer = Timer(const Duration(seconds: 4), () {
        if (mounted) context.go('/login');
      });
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    switch (_fase) {
      case _Fase.cpf:
        return ConfirmarCpf(
          aoConfirmar: _confirmarCpf,
          enviando: _enviando,
          erro: _erroCpf,
          tentativasRestantes: _tentativas,
        );
      case _Fase.confirmado:
        final c = _confirmacao;
        final base = Txt.sans(16, cor: CoresMarica.tintaMute, altura: 1.625);
        return _TelaCentral(
          children: [
            const Icon(LucideIcons.circleCheck, size: 56, color: CoresMarica.verde600),
            const SizedBox(height: 12),
            Text('Presença confirmada!', textAlign: TextAlign.center, style: Txt.display(22, altura: 1.25)),
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 320),
              child: Text.rich(
                TextSpan(
                  style: base,
                  children: [
                    const TextSpan(text: 'Sua presença em '),
                    TextSpan(text: c.titulo, style: base.copyWith(fontWeight: FontWeight.w700)),
                    TextSpan(
                      text: '${c.inicioEm != null ? ' em ${formatarDataHora(c.inicioEm!)}' : ''}'
                          '${c.unidade != null ? ', ${c.unidade},' : ''} está confirmada.',
                    ),
                  ],
                ),
                textAlign: TextAlign.center,
              ),
            ),
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 352),
              child: const AvisoGuia(tamanhoTexto: 15),
            ),
          ],
        );
      case _Fase.expirado:
        return _TelaCentral(
          children: [
            Text('Este link não vale mais', textAlign: TextAlign.center, style: Txt.display(22, altura: 1.25)),
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 288),
              child: Text(
                'Procure a unidade de saúde onde você fez o exame para receber um link novo.',
                textAlign: TextAlign.center,
                style: Txt.sans(16, cor: CoresMarica.tintaMute, altura: 1.625),
              ),
            ),
          ],
        );
      case _Fase.trocando:
        return AnnotatedRegion<SystemUiOverlayStyle>(
          value: SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent),
          child: Scaffold(
            backgroundColor: CoresMarica.areia,
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Girando(tamanho: 32),
                  const SizedBox(height: 16),
                  Text('Entrando…', style: Txt.sans(14, cor: CoresMarica.tintaMute)),
                ],
              ),
            ),
          ),
        );
    }
  }
}

class _TelaCentral extends StatelessWidget {
  const _TelaCentral({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.papel,
        body: SafeArea(
          child: LayoutBuilder(
            builder: (context, c) => SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: c.maxHeight - 48),
                child: Column(mainAxisAlignment: MainAxisAlignment.center, children: children),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
