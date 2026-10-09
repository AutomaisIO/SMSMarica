import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:pdfrx/pdfrx.dart';
import 'package:share_plus/share_plus.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/cache/arquivos_cache.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Abre um documento protegido por JWT DENTRO do app (equivalente a `abrirDocumento` de
/// `lib/pdf.ts` do PWA) — sem depender de leitor de PDF instalado no celular (muitos idosos não
/// têm). A 1ª abertura baixa e guarda no aparelho; as próximas abrem direto, sem rede.
///
/// O cache guarda só os bytes, então o tipo vem de quem chama (a listagem já informa o
/// mimeType); na 1ª abertura vale o Content-Type que o servidor mandou.
///
/// Termina quando o documento ABRE (não quando fecha): quem chama usa isso para tirar o
/// "Abrindo…"/ícone girando da linha.
Future<void> abrirDocumento(
  BuildContext context,
  WidgetRef ref,
  String url, {
  required String nomePadrao,
  String mimeType = 'application/pdf',
}) async {
  final navegador = Navigator.of(context, rootNavigator: true);
  final cache = await ArquivosCache.obter(url);
  if (cache != null) {
    navegador.push(_rota(cache, nomePadrao, mimeType)).ignore();
    return;
  }
  final r = await ref.read(apiProvider).baixar(url);
  // Salva uma cópia — best-effort, não segura a abertura.
  ArquivosCache.salvar(url, r.bytes).ignore();
  navegador.push(_rota(r.bytes, r.nome ?? nomePadrao, r.tipo ?? mimeType)).ignore();
}

/// Abre bytes que já estão na mão (ex.: download público).
void abrirBytes(BuildContext context, Uint8List bytes, String nome, String mimeType) =>
    Navigator.of(context, rootNavigator: true).push(_rota(bytes, nome, mimeType)).ignore();

PageRoute<void> _rota(Uint8List dados, String nome, String mimeType) => PageRouteBuilder<void>(
      opaque: false,
      transitionDuration: const Duration(milliseconds: 200),
      reverseTransitionDuration: const Duration(milliseconds: 150),
      pageBuilder: (_, __, ___) => VisualizadorDocumento(dados: dados, nome: nome, mimeType: mimeType),
      transitionsBuilder: (_, anim, __, filho) => FadeTransition(opacity: anim, child: filho),
    );

const _escalaMin = 1.0;
const _escalaMax = 4.0;

/// Visualizador em tela cheia (`VisualizadorPdf.tsx`): fundo `tinta/95`, cabeçalho com nome,
/// Compartilhar, Baixar e fechar; controle de zoom flutuante embaixo. PDF pelo pdfium (pdfrx);
/// FOTO com o mesmo zoom e os mesmos botões, para o cidadão não ter de aprender duas telas.
class VisualizadorDocumento extends StatefulWidget {
  const VisualizadorDocumento({required this.dados, required this.nome, required this.mimeType, super.key});

  final Uint8List dados;
  final String nome;
  final String mimeType;

  @override
  State<VisualizadorDocumento> createState() => _VisualizadorDocumentoState();
}

class _VisualizadorDocumentoState extends State<VisualizadorDocumento> {
  final _pdf = PdfViewerController();
  final _imagem = TransformationController();
  double _escala = 1;
  bool _pronto = false;
  String? _erro;

  bool get _ehImagem => widget.mimeType.startsWith('image/');

  @override
  void initState() {
    super.initState();
    _imagem.addListener(() => _atualizarEscala(_imagem.value.getMaxScaleOnAxis()));
    if (_ehImagem) _pronto = true;
  }

  @override
  void dispose() {
    _imagem.dispose();
    super.dispose();
  }

  void _atualizarEscala(double e) {
    if ((e - _escala).abs() > 0.01 && mounted) setState(() => _escala = e);
  }

  void _aoMudarPdf() {
    if (!_pdf.isReady) return;
    final base = _pdf.minScale;
    if (base > 0) _atualizarEscala(_pdf.currentZoom / base);
  }

  Future<void> _mudarZoom(double alvo) async {
    final e = alvo.clamp(_escalaMin, _escalaMax);
    if (_ehImagem) {
      final tamanho = context.size ?? Size.zero;
      final centro = Offset(tamanho.width / 2, tamanho.height / 2);
      _imagem.value = Matrix4.identity()
        ..translateByDouble(centro.dx * (1 - e), centro.dy * (1 - e), 0, 1)
        ..scaleByDouble(e, e, 1, 1);
      return;
    }
    if (_pdf.isReady) await _pdf.setZoom(_pdf.centerPosition, _pdf.minScale * e);
  }

  Future<void> _baixar() async {
    try {
      final destino = await FilePicker.platform.saveFile(
        dialogTitle: 'Salvar documento',
        fileName: widget.nome,
        bytes: widget.dados,
      );
      if (destino != null && mounted && !kIsWeb) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Documento salvo.')));
      }
    } on Object {
      // Sem seletor de pasta (ou cancelado de um jeito estranho): o Compartilhar cobre.
      await _compartilhar();
    }
  }

  Future<void> _compartilhar() async {
    try {
      await SharePlus.instance.share(
        ShareParams(
          files: [XFile.fromData(widget.dados, name: widget.nome, mimeType: widget.mimeType)],
          fileNameOverrides: [widget.nome],
          title: widget.nome,
        ),
      );
    } on Object {
      /* usuário cancelou ou não suportado — silencioso */
    }
  }

  @override
  Widget build(BuildContext context) {
    final topo = MediaQuery.paddingOf(context).top;
    final base = MediaQuery.paddingOf(context).bottom;
    return Scaffold(
      backgroundColor: CoresMarica.tinta.withValues(alpha: 0.95),
      body: Stack(
        children: [
          Column(
            children: [
              Padding(
                padding: EdgeInsets.fromLTRB(16, topo + 12, 12, 12),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        widget.nome,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: Txt.sans(14, peso: FontWeight.w500, cor: CoresMarica.branco),
                      ),
                    ),
                    const SizedBox(width: 8),
                    _BotaoTopo(icone: LucideIcons.share2, rotulo: 'Compartilhar', onTap: _compartilhar),
                    const SizedBox(width: 8),
                    _BotaoTopo(icone: LucideIcons.download, rotulo: 'Baixar', onTap: _baixar),
                    const SizedBox(width: 4),
                    IconButton(
                      onPressed: () => Navigator.of(context).pop(),
                      tooltip: 'Fechar',
                      icon: const Icon(LucideIcons.x, color: CoresMarica.branco, size: 20),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: Padding(
                  padding: EdgeInsets.fromLTRB(12, 0, 12, base + 16),
                  child: _ehImagem ? _conteudoImagem() : _conteudoPdf(),
                ),
              ),
            ],
          ),
          if (_pronto && _erro == null)
            Positioned(
              left: 0,
              right: 0,
              bottom: base + 16,
              child: Center(child: _controleZoom()),
            ),
          if (!_pronto && _erro == null) const Center(child: Girando(cor: CoresMarica.branco, tamanho: 32)),
          if (_erro != null)
            Positioned(
              left: 24,
              right: 24,
              bottom: 96,
              child: Center(
                child: Container(
                  constraints: const BoxConstraints(maxWidth: 320),
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  decoration: BoxDecoration(
                    color: CoresMarica.branco,
                    borderRadius: BorderRadius.circular(RaiosMarica.xl),
                  ),
                  child: Text.rich(
                    TextSpan(
                      style: Txt.sans(14, altura: 1.43),
                      children: [
                        const TextSpan(text: 'Não foi possível exibir aqui. Toque em '),
                        TextSpan(text: 'Baixar', style: Txt.sans(14, peso: FontWeight.w700)),
                        const TextSpan(text: ' para salvar o arquivo.'),
                      ],
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _conteudoImagem() {
    return GestureDetector(
      // Duplo-toque: alterna entre 100% e 2,5x (como no PWA).
      onDoubleTap: () => _mudarZoom(_escala > 1.05 ? 1 : 2.5),
      child: InteractiveViewer(
        transformationController: _imagem,
        minScale: _escalaMin,
        maxScale: _escalaMax,
        child: Center(
          child: ClipRRect(
            borderRadius: BorderRadius.circular(RaiosMarica.lg),
            child: Image.memory(
              widget.dados,
              fit: BoxFit.contain,
              errorBuilder: (_, __, ___) {
                WidgetsBinding.instance.addPostFrameCallback((_) {
                  if (mounted && _erro == null) setState(() => _erro = 'imagem');
                });
                return const SizedBox.shrink();
              },
            ),
          ),
        ),
      ),
    );
  }

  Widget _conteudoPdf() {
    return PdfViewer.data(
      widget.dados,
      sourceName: widget.nome,
      controller: _pdf,
      params: PdfViewerParams(
        backgroundColor: Colors.transparent,
        margin: 0,
        pageDropShadow: const BoxShadow(color: Color(0x33000000), blurRadius: 6, offset: Offset(0, 2)),
        onViewerReady: (_, controller) {
          controller.addListener(_aoMudarPdf);
          if (mounted) setState(() => _pronto = true);
        },
        loadingBannerBuilder: (_, __, ___) => const SizedBox.shrink(),
        errorBannerBuilder: (_, erro, __, ___) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted && _erro == null) setState(() => _erro = '$erro');
          });
          return const SizedBox.shrink();
        },
      ),
    );
  }

  Widget _controleZoom() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
      decoration: BoxDecoration(
        color: CoresMarica.tinta.withValues(alpha: 0.8),
        borderRadius: BorderRadius.circular(999),
        boxShadow: const [BoxShadow(color: Color(0x40000000), blurRadius: 12, offset: Offset(0, 4))],
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _BotaoZoom(
            icone: LucideIcons.minus,
            rotulo: 'Reduzir',
            onTap: _escala <= _escalaMin + 0.01 ? null : () => _mudarZoom(_escala - 0.5),
          ),
          GestureDetector(
            onTap: () => _mudarZoom(1),
            child: SizedBox(
              width: 52,
              child: Text(
                '${(_escala * 100).round()}%',
                textAlign: TextAlign.center,
                style: Txt.sans(14, peso: FontWeight.w500, cor: CoresMarica.branco, recursos: Txt.tabular),
              ),
            ),
          ),
          _BotaoZoom(
            icone: LucideIcons.plus,
            rotulo: 'Ampliar',
            onTap: _escala >= _escalaMax - 0.01 ? null : () => _mudarZoom(_escala + 0.5),
          ),
        ],
      ),
    );
  }
}

class _BotaoTopo extends StatelessWidget {
  const _BotaoTopo({required this.icone, required this.rotulo, required this.onTap});

  final IconData icone;
  final String rotulo;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Pressionavel(
      onTap: onTap,
      escala: 0.95,
      semantica: rotulo,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: CoresMarica.branco.withValues(alpha: 0.15),
          borderRadius: BorderRadius.circular(RaiosMarica.lg),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icone, size: 16, color: CoresMarica.branco),
            const SizedBox(width: 6),
            Text(rotulo, style: Txt.sans(14, peso: FontWeight.w500, cor: CoresMarica.branco)),
          ],
        ),
      ),
    );
  }
}

class _BotaoZoom extends StatelessWidget {
  const _BotaoZoom({required this.icone, required this.rotulo, required this.onTap});

  final IconData icone;
  final String rotulo;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: onTap == null ? 0.4 : 1,
      child: IconButton(
        onPressed: onTap,
        tooltip: rotulo,
        visualDensity: VisualDensity.compact,
        icon: Icon(icone, size: 20, color: CoresMarica.branco),
      ),
    );
  }
}
