import 'dart:io';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/data/detector_rosto.dart';
import 'package:agente/features/auth/facial/domain/desafio_liveness.dart';
import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:permission_handler/permission_handler.dart';

/// Resultado da captura facial: caminho do arquivo do frame nítido aprovado.
class CapturaFacialResultado {
  const CapturaFacialResultado({required this.caminhoFoto});
  final String caminhoFoto;
}

/// Motivo do título/instrução exibido — distingue cadastro, login e re-check.
enum FinalidadeCaptura { login, cadastro, inicioTranslado }

/// Tela de captura facial com liveness ativo (piscada). Retorna um
/// [CapturaFacialResultado] via `Navigator.pop` quando aprovado, ou `null` se
/// o usuário cancelar.
class CapturaFacialPage extends StatefulWidget {
  const CapturaFacialPage({
    required this.finalidade,
    this.nomeMotorista,
    super.key,
  });

  final FinalidadeCaptura finalidade;
  final String? nomeMotorista;

  @override
  State<CapturaFacialPage> createState() => _CapturaFacialPageState();
}

class _CapturaFacialPageState extends State<CapturaFacialPage> {
  CameraController? _controller;
  final DetectorRosto _detector = DetectorRosto();
  final DesafioLiveness _liveness = DesafioLiveness();

  bool _processando = false;
  bool _capturando = false;
  String _instrucao = 'Posicione o rosto no círculo';
  String? _erro;

  @override
  void initState() {
    super.initState();
    _iniciar();
  }

  Future<void> _iniciar() async {
    final perm = await Permission.camera.request();
    if (!perm.isGranted) {
      setState(() => _erro = 'Permissão de câmera negada.');
      return;
    }

    final cameras = await availableCameras();
    final frontal = cameras.firstWhere(
      (c) => c.lensDirection == CameraLensDirection.front,
      orElse: () => cameras.first,
    );

    final controller = CameraController(
      frontal,
      ResolutionPreset.medium,
      enableAudio: false,
      imageFormatGroup: Platform.isAndroid
          ? ImageFormatGroup.nv21
          : ImageFormatGroup.bgra8888,
    );

    try {
      await controller.initialize();
      if (!mounted) {
        await controller.dispose();
        return;
      }
      _controller = controller;
      await controller.startImageStream(_aoReceberFrame);
      setState(() {});
    } on CameraException catch (e) {
      setState(() => _erro = 'Falha ao abrir a câmera: ${e.description}');
    }
  }

  Future<void> _aoReceberFrame(CameraImage image) async {
    if (_processando || _capturando || _controller == null) return;
    _processando = true;
    try {
      final rostos =
          await _detector.detectarDoFrame(image, _controller!.description);
      final tamanho = Size(image.width.toDouble(), image.height.toDouble());
      final frontalOk = rostoFrontalOk(rostos, tamanho);
      final prob = rostos.length == 1 ? probOlhosAbertos(rostos.first) : null;

      final etapa = _liveness.avaliar(
        rostoFrontalOk: frontalOk,
        probOlhosAbertos: prob,
      );

      _atualizarInstrucao(etapa, rostos.length);

      if (etapa == EtapaLiveness.aprovado) {
        await _capturarFrame();
      }
    } finally {
      _processando = false;
    }
  }

  void _atualizarInstrucao(EtapaLiveness etapa, int qtdRostos) {
    final novo = switch (etapa) {
      EtapaLiveness.procurandoRosto => qtdRostos > 1
          ? 'Apenas um rosto no quadro'
          : 'Posicione o rosto no círculo',
      EtapaLiveness.aguardandoPiscada => 'Pisque os olhos',
      EtapaLiveness.aprovado => 'Pronto! Capturando…',
    };
    if (novo != _instrucao && mounted) setState(() => _instrucao = novo);
  }

  Future<void> _capturarFrame() async {
    if (_capturando) return;
    _capturando = true;
    final controller = _controller;
    if (controller == null) return;
    try {
      await controller.stopImageStream();
      final foto = await controller.takePicture();
      if (!mounted) return;
      Navigator.of(context).pop(
        CapturaFacialResultado(caminhoFoto: foto.path),
      );
    } on CameraException catch (_) {
      _capturando = false;
      _liveness.reiniciar();
      if (controller.value.isInitialized) {
        await controller.startImageStream(_aoReceberFrame);
      }
    }
  }

  @override
  void dispose() {
    final c = _controller;
    _controller = null;
    if (c != null) {
      if (c.value.isStreamingImages) {
        c.stopImageStream();
      }
      c.dispose();
    }
    _detector.dispose();
    super.dispose();
  }

  String get _titulo => switch (widget.finalidade) {
        FinalidadeCaptura.login => 'Identificação do motorista',
        FinalidadeCaptura.cadastro => 'Cadastro facial',
        FinalidadeCaptura.inicioTranslado => 'Confirmar motorista',
      };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(title: Text(_titulo)),
      body: _erro != null
          ? _ErroView(mensagem: _erro!)
          : _controller == null || !_controller!.value.isInitialized
              ? const Center(
                  child: CircularProgressIndicator(color: Colors.white),
                )
              : _Camera(controller: _controller!, instrucao: _instrucao),
    );
  }
}

class _Camera extends StatelessWidget {
  const _Camera({required this.controller, required this.instrucao});
  final CameraController controller;
  final String instrucao;

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: [
        FittedBox(
          fit: BoxFit.cover,
          child: SizedBox(
            width: controller.value.previewSize?.height ?? 1,
            height: controller.value.previewSize?.width ?? 1,
            child: CameraPreview(controller),
          ),
        ),
        const _MascaraOval(),
        Positioned(
          left: 0,
          right: 0,
          bottom: 48,
          child: Center(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              decoration: BoxDecoration(
                color: Colors.black.withValues(alpha: 0.6),
                borderRadius: BorderRadius.circular(24),
              ),
              child: Text(
                instrucao,
                style: const TextStyle(color: Colors.white, fontSize: 16),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _MascaraOval extends StatelessWidget {
  const _MascaraOval();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: 240,
        height: 320,
        decoration: BoxDecoration(
          borderRadius: const BorderRadius.all(Radius.elliptical(120, 160)),
          border: Border.all(color: MaricaTheme.branco, width: 3),
        ),
      ),
    );
  }
}

class _ErroView extends StatelessWidget {
  const _ErroView({required this.mensagem});
  final String mensagem;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.videocam_off, color: Colors.white70, size: 56),
            const SizedBox(height: 16),
            Text(
              mensagem,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('Voltar'),
            ),
          ],
        ),
      ),
    );
  }
}
