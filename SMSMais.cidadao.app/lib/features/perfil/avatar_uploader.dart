import 'dart:convert';

import 'package:crop_your_image/crop_your_image.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image/image.dart' as img;
import 'package:image_picker/image_picker.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

const _tamanhoFinal = 512;
const _tamanhoMaximoEntrada = 10 * 1024 * 1024;

/// Avatar grande com botão de câmera (`AvatarUploader.tsx`). Ao escolher a foto, abre o recorte
/// (arrastar/pinça, máscara redonda) e devolve a foto quadrada 512×512 em JPEG como data URL —
/// foto de perfil "estilo WhatsApp": nítida em qualquer avatar e leve para trafegar.
class AvatarUploader extends StatefulWidget {
  const AvatarUploader({
    required this.valor,
    required this.nome,
    required this.aoMudar,
    this.salvando = false,
    super.key,
  });

  final String? valor;
  final String nome;
  final ValueChanged<String> aoMudar;
  final bool salvando;

  @override
  State<AvatarUploader> createState() => _AvatarUploaderState();
}

class _AvatarUploaderState extends State<AvatarUploader> {
  final _ancora = GlobalKey();
  String? _erro;

  Future<void> _abrirMenu() async {
    final caixa = _ancora.currentContext?.findRenderObject() as RenderBox?;
    final overlay = Navigator.of(context).overlay?.context.findRenderObject() as RenderBox?;
    if (caixa == null || overlay == null) return;
    const largura = 224.0;
    final origem = caixa.localToGlobal(Offset.zero, ancestor: overlay);
    final retangulo = Rect.fromLTWH(
      origem.dx + caixa.size.width / 2 - largura / 2,
      origem.dy + caixa.size.height + 12,
      largura,
      0,
    );

    final escolha = await showMenu<ImageSource>(
      context: context,
      position: RelativeRect.fromRect(retangulo, Offset.zero & overlay.size),
      color: CoresMarica.branco,
      elevation: 12,
      shadowColor: Colors.black.withValues(alpha: 0.35),
      constraints: const BoxConstraints(minWidth: largura, maxWidth: largura),
      menuPadding: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        side: const BorderSide(color: CoresMarica.areia),
      ),
      items: [
        _itemMenu(ImageSource.camera, LucideIcons.camera, CoresMarica.marica, 'Tirar foto'),
        const PopupMenuDivider(height: 1, color: CoresMarica.areia),
        _itemMenu(ImageSource.gallery, LucideIcons.images, CoresMarica.lagoa, 'Escolher da galeria'),
      ],
    );
    if (escolha != null) await _escolher(escolha);
  }

  PopupMenuItem<ImageSource> _itemMenu(ImageSource valor, IconData icone, Color cor, String rotulo) =>
      PopupMenuItem<ImageSource>(
        value: valor,
        height: 52,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        child: Row(
          children: [
            Icon(icone, size: 20, color: cor),
            const SizedBox(width: 12),
            Text(rotulo, style: Txt.sans(15, peso: FontWeight.w500)),
          ],
        ),
      );

  Future<void> _escolher(ImageSource origem) async {
    try {
      // Câmera FRONTAL direto (selfie); a galeria abre o seletor de fotos. O teto de 2048px
      // poupa memória no recorte — o resultado final é 512px de qualquer jeito.
      final foto = await ImagePicker().pickImage(
        source: origem,
        preferredCameraDevice: CameraDevice.front,
        maxWidth: 2048,
        maxHeight: 2048,
        imageQuality: 95,
      );
      if (foto == null) return;
      final tipo = foto.mimeType;
      if (tipo != null && !tipo.startsWith('image/')) {
        setState(() => _erro = 'Selecione uma imagem.');
        return;
      }
      if (await foto.length() > _tamanhoMaximoEntrada) {
        setState(() => _erro = 'Imagem muito grande (máx. 10 MB).');
        return;
      }
      setState(() => _erro = null);
      final bytes = await foto.readAsBytes();
      if (!mounted) return;
      final recortada = await Navigator.of(context, rootNavigator: true).push<String>(
        PageRouteBuilder<String>(
          transitionDuration: const Duration(milliseconds: 200),
          pageBuilder: (_, __, ___) => _TelaRecorte(imagem: bytes),
          transitionsBuilder: (_, anim, __, filho) => FadeTransition(opacity: anim, child: filho),
        ),
      );
      if (recortada != null) widget.aoMudar(recortada);
    } on Object {
      if (mounted) setState(() => _erro = 'Não foi possível abrir a imagem.');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Stack(
          key: _ancora,
          clipBehavior: Clip.none,
          children: [
            // ring-4 ring-white + shadow-carta
            Container(
              padding: const EdgeInsets.all(4),
              decoration: const BoxDecoration(
                color: CoresMarica.branco,
                shape: BoxShape.circle,
                boxShadow: SombrasMarica.carta,
              ),
              child: Avatar(nome: widget.nome, foto: widget.valor, tamanho: 104),
            ),
            Positioned(
              right: -4,
              bottom: -4,
              child: Opacity(
                opacity: widget.salvando ? 0.5 : 1,
                child: Pressionavel(
                  onTap: widget.salvando ? null : _abrirMenu,
                  escala: 0.95,
                  semantica: 'Trocar foto',
                  // ring-4 ring-papel em volta do botão vermelho.
                  child: Container(
                    padding: const EdgeInsets.all(4),
                    decoration: const BoxDecoration(color: CoresMarica.papel, shape: BoxShape.circle),
                    child: Container(
                      width: 40,
                      height: 40,
                      decoration: const BoxDecoration(
                        color: CoresMarica.marica,
                        shape: BoxShape.circle,
                        boxShadow: SombrasMarica.carta,
                      ),
                      child: const Icon(LucideIcons.camera, size: 20, color: CoresMarica.branco),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
        if (_erro != null)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: TextoErro(_erro!, centralizado: true),
          ),
      ],
    );
  }
}

/// Recorte em tela cheia sobre fundo `tinta`. Controles no TOPO, como no PWA: em alguns
/// celulares a barra do navegador/sistema cobre o rodapé e o botão some.
class _TelaRecorte extends StatefulWidget {
  const _TelaRecorte({required this.imagem});

  final Uint8List imagem;

  @override
  State<_TelaRecorte> createState() => _TelaRecorteState();
}

class _TelaRecorteState extends State<_TelaRecorte> {
  final _controller = CropController();
  bool _pronto = false;
  bool _processando = false;
  String? _erro;

  void _usar() {
    if (!_pronto || _processando) return;
    setState(() {
      _processando = true;
      _erro = null;
    });
    _controller.crop();
  }

  Future<void> _aoRecortar(CropResult resultado) async {
    switch (resultado) {
      case CropSuccess(:final croppedImage):
        try {
          final jpg = await compute(_paraJpegQuadrado, croppedImage);
          if (mounted) Navigator.of(context).pop('data:image/jpeg;base64,${base64Encode(jpg)}');
        } on Object {
          _falhou();
        }
      case CropFailure():
        _falhou();
    }
  }

  void _falhou() {
    if (mounted) {
      setState(() {
        _processando = false;
        _erro = 'Falha ao recortar a foto.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final topo = MediaQuery.paddingOf(context).top;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.tinta,
        body: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ColoredBox(
              color: CoresMarica.papel,
              child: Padding(
                padding: EdgeInsets.fromLTRB(20, topo + 16, 20, 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: BotaoFantasma(
                            rotulo: 'Cancelar',
                            onPressed: _processando ? null : () => Navigator.of(context).pop(),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: BotaoPrimario(
                            rotulo: 'Usar foto',
                            carregando: _processando,
                            onPressed: _pronto ? _usar : null,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        const Icon(LucideIcons.camera, size: 16, color: CoresMarica.tintaMute),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            'Arraste a foto e use dois dedos para aproximar.',
                            style: Txt.sans(13, cor: CoresMarica.tintaMute),
                          ),
                        ),
                        IconButton(
                          onPressed: _processando ? null : () => _controller.image = widget.imagem,
                          tooltip: 'Resetar',
                          visualDensity: VisualDensity.compact,
                          icon: const Icon(LucideIcons.rotateCcw, size: 16, color: CoresMarica.tintaMute),
                        ),
                      ],
                    ),
                    if (_erro != null) ...[const SizedBox(height: 4), TextoErro(_erro!)],
                  ],
                ),
              ),
            ),
            Expanded(
              child: Crop(
                image: widget.imagem,
                controller: _controller,
                onCropped: _aoRecortar,
                aspectRatio: 1,
                withCircleUi: true,
                interactive: true,
                // Círculo fixo, a foto é que se move (como o react-easy-crop do PWA).
                fixCropRect: true,
                initialRectBuilder: InitialRectBuilder.withSizeAndRatio(size: 0.85, aspectRatio: 1),
                baseColor: CoresMarica.tinta,
                maskColor: Colors.black.withValues(alpha: 0.5),
                cornerDotBuilder: (_, __) => const SizedBox.shrink(),
                progressIndicator: const Center(child: Girando(cor: CoresMarica.branco, tamanho: 32)),
                onStatusChanged: (s) {
                  final pronto = s == CropStatus.ready;
                  if (pronto != _pronto && mounted) setState(() => _pronto = pronto);
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Reduz o recorte a 512×512 e codifica em JPEG 85 (o `recortarParaBase64` do PWA). Roda fora da
/// thread da tela (`compute`). Fundo branco: PNG com transparência viraria preto no JPEG.
Uint8List _paraJpegQuadrado(Uint8List bytes) {
  final origem = img.decodeImage(bytes);
  if (origem == null) throw const FormatException('imagem ilegível');
  final reduzida = img.copyResize(
    img.bakeOrientation(origem),
    width: _tamanhoFinal,
    height: _tamanhoFinal,
    interpolation: img.Interpolation.cubic,
  );
  final fundo = img.Image(width: _tamanhoFinal, height: _tamanhoFinal)..clear(img.ColorRgb8(255, 255, 255));
  img.compositeImage(fundo, reduzida);
  return img.encodeJpg(fundo, quality: 85);
}
