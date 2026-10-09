import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/auth/confirmar_cpf.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/visualizador/visualizador.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

enum _Estado { carregando, cpf, valido, expirado }

/// Página pública (sem login) do link de download enviado ao paciente (ex.: WhatsApp) —
/// `Documento.tsx`.
///
/// O token é de uso único, mas possuí-lo não basta: antes de qualquer coisa o portador confirma o
/// CPF do titular. Sem isso, este era o caminho de menor resistência — entregava o PDF completo
/// do exame de forma anônima, sem nem abrir sessão.
class DocumentoPublicoPage extends ConsumerStatefulWidget {
  const DocumentoPublicoPage({required this.token, super.key});

  final String token;

  @override
  ConsumerState<DocumentoPublicoPage> createState() => _DocumentoPublicoPageState();
}

class _DocumentoPublicoPageState extends ConsumerState<DocumentoPublicoPage> {
  _Estado _estado = _Estado.carregando;
  String? _descricao;
  String? _liberacao;
  bool _enviando = false;
  String? _erroCpf;
  int? _tentativas;
  bool _baixando = false;
  String? _erroDownload;

  @override
  void initState() {
    super.initState();
    _status();
  }

  Future<void> _status() async {
    try {
      final r = await ref.read(apiProvider).downloadStatus(widget.token);
      if (!mounted) return;
      if (r['estado'] != 'valido') {
        setState(() => _estado = _Estado.expirado);
        return;
      }
      setState(() {
        _descricao = r['descricao'] as String?;
        _tentativas = (r['tentativasRestantes'] as num?)?.toInt();
        _estado = r['requerCpf'] == true ? _Estado.cpf : _Estado.valido;
      });
    } on Object {
      if (mounted) setState(() => _estado = _Estado.expirado);
    }
  }

  Future<void> _confirmarCpf(String cpf) async {
    setState(() {
      _enviando = true;
      _erroCpf = null;
    });
    try {
      final r = await ref.read(apiProvider).downloadConfirmar(widget.token, cpf);
      if (!mounted) return;
      final liberacao = r['liberacao'] as String?;
      if (liberacao != null) {
        setState(() {
          _liberacao = liberacao;
          _descricao = r['descricao'] as String?;
          _estado = _Estado.valido;
        });
        return;
      }
      final restantes = (r['tentativasRestantes'] as num?)?.toInt() ?? 0;
      setState(() {
        _tentativas = restantes;
        if (restantes <= 0) {
          _estado = _Estado.expirado;
        } else {
          _erroCpf = 'Esse CPF não confere.';
        }
      });
    } on Object {
      if (mounted) setState(() => _estado = _Estado.expirado);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  /// No navegador o PWA só aponta para a URL; aqui baixamos os bytes e abrimos no visualizador
  /// embutido — de lá a pessoa salva ou compartilha.
  Future<void> _baixar() async {
    setState(() {
      _baixando = true;
      _erroDownload = null;
    });
    try {
      final api = ref.read(apiProvider);
      final r = await api.baixar(api.urlDownloadPublico(widget.token, _liberacao));
      if (!mounted) return;
      abrirBytes(context, r.bytes, r.nome ?? 'exame.pdf', r.tipo ?? 'application/pdf');
    } on Object catch (e) {
      if (mounted) setState(() => _erroDownload = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _baixando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_estado == _Estado.cpf) {
      return ConfirmarCpf(
        aoConfirmar: _confirmarCpf,
        enviando: _enviando,
        erro: _erroCpf,
        tentativasRestantes: _tentativas,
      );
    }

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.areia,
        body: SafeArea(
          child: LayoutBuilder(
            builder: (context, c) => SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 40),
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: c.maxHeight - 80),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Container(
                      width: double.infinity,
                      constraints: const BoxConstraints(maxWidth: 384),
                      padding: const EdgeInsets.all(28),
                      decoration: BoxDecoration(
                        color: CoresMarica.branco,
                        borderRadius: BorderRadius.circular(RaiosMarica.x3l),
                        boxShadow: const [
                          BoxShadow(color: Color(0x1A000000), offset: Offset(0, 10), blurRadius: 15, spreadRadius: -3),
                          BoxShadow(color: Color(0x1A000000), offset: Offset(0, 4), blurRadius: 6, spreadRadius: -4),
                        ],
                      ),
                      child: switch (_estado) {
                        _Estado.valido => _valido(),
                        _Estado.expirado => _expirado(),
                        _ => _carregando(),
                      },
                    ),
                    const SizedBox(height: 24),
                    Text(
                      'Secretaria Municipal de Saúde de Maricá',
                      textAlign: TextAlign.center,
                      style: Txt.sans(12, cor: CoresMarica.tintaMute),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _carregando() => Padding(
        padding: const EdgeInsets.symmetric(vertical: 24),
        child: Column(
          children: [
            const Girando(tamanho: 28),
            const SizedBox(height: 12),
            Text('Carregando…', style: Txt.sans(14, cor: CoresMarica.tintaMute)),
          ],
        ),
      );

  Widget _icone(IconData icone, Color fundo, Color cor) => Container(
        width: 56,
        height: 56,
        decoration: BoxDecoration(color: fundo, shape: BoxShape.circle),
        child: Icon(icone, size: 28, color: cor),
      );

  Widget _valido() {
    final base = Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43);
    final negrito = base.copyWith(fontWeight: FontWeight.w700);
    return Column(
      children: [
        _icone(LucideIcons.fileCheck2, CoresMarica.marica.withValues(alpha: 0.1), CoresMarica.marica),
        const SizedBox(height: 16),
        Text('Seu exame está pronto', textAlign: TextAlign.center, style: Txt.display(20)),
        const SizedBox(height: 8),
        Text.rich(
          TextSpan(
            style: base,
            children: [
              if (_descricao != null) ...[
                const TextSpan(text: 'Exame: '),
                TextSpan(text: _descricao, style: negrito),
                const TextSpan(text: '.\n'),
              ],
              const TextSpan(text: 'O download fica disponível '),
              TextSpan(text: 'uma única vez', style: negrito),
              const TextSpan(text: ' por este link. Depois, acesse pelo app.'),
            ],
          ),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 24),
        BotaoPrimario(
          rotulo: 'Baixar meu exame',
          icone: LucideIcons.download,
          carregando: _baixando,
          onPressed: _baixar,
        ),
        if (_erroDownload != null) ...[
          const SizedBox(height: 12),
          TextoErro(_erroDownload!, centralizado: true),
        ],
      ],
    );
  }

  Widget _expirado() {
    return Column(
      children: [
        _icone(LucideIcons.clock, CoresMarica.tinta.withValues(alpha: 0.05), CoresMarica.tintaMute),
        const SizedBox(height: 16),
        Text('Este link expirou', textAlign: TextAlign.center, style: Txt.display(20)),
        const SizedBox(height: 8),
        Text(
          'O link de download já foi utilizado ou passou da validade. Para acessar seus exames e '
          'laudos quando quiser, use o aplicativo Saúde Maricá.',
          textAlign: TextAlign.center,
          style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
        ),
        const SizedBox(height: 24),
        BotaoPrimario(rotulo: 'Abrir o app', onPressed: () => context.go('/')),
      ],
    );
  }
}
