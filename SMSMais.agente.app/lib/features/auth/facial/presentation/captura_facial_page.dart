import 'dart:async';
import 'dart:io';
import 'dart:math';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/data/detector_rosto.dart';
import 'package:agente/features/auth/facial/domain/desafio_liveness.dart';
import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:permission_handler/permission_handler.dart';

/// Resultado da captura facial: a foto nítida tirada depois da prova de vida.
class CapturaFacialResultado {
  const CapturaFacialResultado({
    required this.caminhoFoto,
    required this.duracaoProva,
  });

  final String caminhoFoto;

  /// Da abertura da câmera até a foto (mede a experiência do motorista).
  final Duration duracaoProva;
}

/// Motivo do título/instrução exibido — distingue cadastro, login e re-check.
enum FinalidadeCaptura { login, cadastro, inicioTranslado }

/// Tela de captura facial com prova de vida (piscar + virar para o lado
/// sorteado + voltar de frente). Devolve um [CapturaFacialResultado] via
/// `Navigator.pop` quando aprovada, ou `null` se o motorista voltar.
///
/// [diagnostico] mostra, por cima da câmera, o que o detector está vendo
/// (rotação, ângulos, olhos, tempo por quadro) — para calibrar no tablet.
class CapturaFacialPage extends StatefulWidget {
  const CapturaFacialPage({
    required this.finalidade,
    this.nomeMotorista,
    this.diagnostico = false,
    super.key,
  });

  final FinalidadeCaptura finalidade;
  final String? nomeMotorista;
  final bool diagnostico;

  @override
  State<CapturaFacialPage> createState() => _CapturaFacialPageState();
}

class _CapturaFacialPageState extends State<CapturaFacialPage> {
  CameraController? _controller;
  final DetectorRosto _detector = DetectorRosto();
  final DesafioLiveness _liveness = DesafioLiveness();
  final Stopwatch _desdeAbertura = Stopwatch();

  bool _processando = false;
  bool _capturando = false;
  EtapaLiveness _etapa = EtapaLiveness.procurandoRosto;
  int _rostos = 0;
  MedidaRosto? _ultimaMedida;
  double? _roll;
  int _msQuadro = 0;
  String? _erro;

  @override
  void initState() {
    super.initState();
    unawaited(_iniciar());
  }

  Future<void> _iniciar() async {
    final perm = await Permission.camera.request();
    if (!perm.isGranted) {
      setState(() => _erro = 'Permissão de câmera negada.');
      return;
    }

    final cameras = await availableCameras();
    if (cameras.isEmpty) {
      setState(() => _erro = 'Este tablet não tem câmera.');
      return;
    }
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
      _desdeAbertura.start();
      await controller.startImageStream(_aoReceberFrame);
      setState(() {});
    } on CameraException catch (e) {
      setState(() => _erro = 'Falha ao abrir a câmera: ${e.description}');
    }
  }

  Future<void> _aoReceberFrame(CameraImage image) async {
    final controller = _controller;
    if (_processando || _capturando || controller == null) return;
    _processando = true;
    final relogio = Stopwatch()..start();
    try {
      final rostos = await _detector.detectarDoFrame(image, controller);
      final tamanho = Size(image.width.toDouble(), image.height.toDouble());
      final medida = medirRostos(rostos, tamanho);
      final etapa = _liveness.avaliar(medida);

      if (mounted) {
        setState(() {
          _etapa = etapa;
          _rostos = rostos.length;
          _ultimaMedida = medida;
          _roll = rostos.length == 1 ? rostos.first.headEulerAngleZ : null;
          _msQuadro = relogio.elapsedMilliseconds;
        });
      }

      if (etapa == EtapaLiveness.aprovado) await _capturarFrame();
    } on Object catch (_) {
      // Um quadro ruim não derruba a captura; o próximo tenta de novo.
    } finally {
      _processando = false;
    }
  }

  Future<void> _capturarFrame() async {
    final controller = _controller;
    if (_capturando || controller == null) return;
    _capturando = true;
    try {
      await controller.stopImageStream();
      final foto = await controller.takePicture();
      if (!mounted) return;
      Navigator.of(context).pop(
        CapturaFacialResultado(
          caminhoFoto: foto.path,
          duracaoProva: _desdeAbertura.elapsed,
        ),
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
      if (c.value.isStreamingImages) unawaited(c.stopImageStream());
      unawaited(c.dispose());
    }
    _detector.dispose();
    super.dispose();
  }

  String get _titulo => switch (widget.finalidade) {
    FinalidadeCaptura.login => 'Identificação do motorista',
    FinalidadeCaptura.cadastro => 'Cadastro do rosto',
    FinalidadeCaptura.inicioTranslado => 'Confirmar motorista',
  };

  String get _instrucao => switch (_etapa) {
    EtapaLiveness.procurandoRosto when _rostos > 1 =>
      'Só um rosto na câmera, por favor',
    EtapaLiveness.procurandoRosto when _liveness.expirou =>
      'Vamos de novo: olhe para a câmera',
    EtapaLiveness.procurandoRosto => 'Olhe para a câmera, dentro do círculo',
    EtapaLiveness.aguardandoPiscada => 'Pisque os olhos',
    EtapaLiveness.aguardandoVirar => switch (_liveness.lado) {
      LadoVirar.esquerda => 'Vire o rosto devagar para a sua ESQUERDA',
      LadoVirar.direita => 'Vire o rosto devagar para a sua DIREITA',
    },
    EtapaLiveness.aguardandoFrente => 'Agora olhe de frente para a câmera',
    EtapaLiveness.aprovado => 'Pronto! Tirando a foto…',
  };

  @override
  Widget build(BuildContext context) {
    final controller = _controller;
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(title: Text(_titulo)),
      body: _erro != null
          ? _ErroView(mensagem: _erro!)
          : controller == null || !controller.value.isInitialized
          ? const Center(child: CircularProgressIndicator(color: Colors.white))
          : Stack(
              fit: StackFit.expand,
              children: [
                _Preview(controller: controller),
                const _MascaraOval(),
                if (_etapa == EtapaLiveness.aguardandoVirar)
                  _Seta(lado: _liveness.lado),
                Positioned(
                  left: 0,
                  right: 0,
                  bottom: 32,
                  child: Center(child: _Instrucao(texto: _instrucao)),
                ),
                if (widget.diagnostico)
                  Positioned(
                    left: 8,
                    top: 8,
                    child: _Diagnostico(linhas: _linhasDiagnostico(controller)),
                  ),
              ],
            ),
    );
  }

  List<String> _linhasDiagnostico(CameraController c) {
    final m = _ultimaMedida;
    String g(double? v) => v == null ? '—' : v.toStringAsFixed(0);
    final olhos = m?.olhosAbertos?.toStringAsFixed(2) ?? '—';
    final rot = _detector.rotacao;
    final estado = rot.travada ? 'travada' : 'buscando';
    final camera =
        'sensor ${c.description.sensorOrientation}°'
        ' · tela ${c.value.deviceOrientation.name}';
    final rosto = 'yaw ${g(m?.yaw)} · roll ${g(_roll)} · olhos $olhos';
    return [
      'etapa ${_etapa.name}',
      'rotação ${rot.ultima ?? '—'}° ($estado) · $camera',
      'rostos $_rostos · frontal ${m?.frontal ?? false} · $rosto',
      'quadro $_msQuadro ms · prévia ${c.value.previewSize}',
    ];
  }
}

/// Câmera cobrindo a tela, com a proporção certa em paisagem ou retrato.
class _Preview extends StatelessWidget {
  const _Preview({required this.controller});

  final CameraController controller;

  @override
  Widget build(BuildContext context) {
    final s = controller.value.previewSize ?? const Size(1, 1);
    final maior = max(s.width, s.height);
    final menor = min(s.width, s.height);
    final paisagem = MediaQuery.orientationOf(context) == Orientation.landscape;
    return ClipRect(
      child: FittedBox(
        fit: BoxFit.cover,
        child: SizedBox(
          width: paisagem ? maior : menor,
          height: paisagem ? menor : maior,
          child: CameraPreview(controller),
        ),
      ),
    );
  }
}

class _Instrucao extends StatelessWidget {
  const _Instrucao({required this.texto});

  final String texto;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
    decoration: BoxDecoration(
      color: Colors.black.withValues(alpha: 0.65),
      borderRadius: BorderRadius.circular(28),
    ),
    child: Text(
      texto,
      textAlign: TextAlign.center,
      style: const TextStyle(
        color: Colors.white,
        fontSize: 22,
        fontWeight: FontWeight.w700,
      ),
    ),
  );
}

/// Seta grande do lado para onde virar. A prévia da câmera frontal é
/// espelhada, então a esquerda da pessoa fica à esquerda da tela.
class _Seta extends StatelessWidget {
  const _Seta({required this.lado});

  final LadoVirar lado;

  @override
  Widget build(BuildContext context) => Align(
    alignment: lado == LadoVirar.esquerda
        ? const Alignment(-0.8, 0)
        : const Alignment(0.8, 0),
    child: Icon(
      lado == LadoVirar.esquerda ? Icons.arrow_back : Icons.arrow_forward,
      color: Colors.white,
      size: 120,
      shadows: const [Shadow(blurRadius: 12)],
    ),
  );
}

class _Diagnostico extends StatelessWidget {
  const _Diagnostico({required this.linhas});

  final List<String> linhas;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(8),
    color: Colors.black.withValues(alpha: 0.6),
    child: Text(
      linhas.join('\n'),
      style: const TextStyle(
        color: Colors.greenAccent,
        fontSize: 12,
        fontFamily: 'monospace',
      ),
    ),
  );
}

class _MascaraOval extends StatelessWidget {
  const _MascaraOval();

  @override
  Widget build(BuildContext context) => Center(
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

class _ErroView extends StatelessWidget {
  const _ErroView({required this.mensagem});

  final String mensagem;

  @override
  Widget build(BuildContext context) => Center(
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
