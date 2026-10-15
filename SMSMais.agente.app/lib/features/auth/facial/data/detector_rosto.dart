import 'dart:ui' show Size;

import 'package:agente/features/auth/facial/domain/desafio_liveness.dart';
import 'package:camera/camera.dart';
import 'package:flutter/services.dart' show DeviceOrientation;
import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';

/// Wrapper do ML Kit Face Detection. O modelo vai dentro do APK
/// (`com.google.mlkit:face-detection`), sem baixar nada do Play Services —
/// os tablets da frota não têm Play Services funcionando.
///
/// Detecta rostos em quadros da câmera (prova de vida e posicionamento) e em
/// arquivos (recorte que alimenta o embedding).
class DetectorRosto {
  DetectorRosto()
    : _detector = FaceDetector(
        options: FaceDetectorOptions(
          enableClassification: true,
          enableLandmarks: true,
          enableTracking: true,
          minFaceSize: 0.15,
        ),
      );

  final FaceDetector _detector;

  /// Rotação dos quadros, descoberta no aparelho (ver [RotacaoCamera]).
  final RotacaoCamera rotacao = RotacaoCamera();

  /// Detecta num quadro do stream da câmera.
  Future<List<Face>> detectarDoFrame(
    CameraImage image,
    CameraController controller,
  ) async {
    final camera = controller.description;
    final graus = rotacao.graus(
      sensor: camera.sensorOrientation,
      tela: _grausTela[controller.value.deviceOrientation] ?? 0,
      frontal: camera.lensDirection == CameraLensDirection.front,
    );
    final input = _inputImageDoFrame(image, graus);
    if (input == null) return const [];
    final rostos = await _detector.processImage(input);
    rotacao.registrar(
      achouRosto: rostos.any((r) => (r.headEulerAngleZ ?? 90).abs() < 30),
    );
    return rostos;
  }

  /// Detecta num arquivo (foto tirada ou foto de referência baixada).
  Future<List<Face>> detectarDoArquivo(String caminho) =>
      _detector.processImage(InputImage.fromFilePath(caminho));

  void dispose() => _detector.close();

  static const _grausTela = {
    DeviceOrientation.portraitUp: 0,
    DeviceOrientation.landscapeLeft: 90,
    DeviceOrientation.portraitDown: 180,
    DeviceOrientation.landscapeRight: 270,
  };

  InputImage? _inputImageDoFrame(CameraImage image, int graus) {
    final rotation = InputImageRotationValue.fromRawValue(graus);
    final format = InputImageFormatValue.fromRawValue(image.format.raw as int);
    if (rotation == null || format == null) return null;

    // A câmera está em NV21 (Android): um plano só.
    if (image.planes.isEmpty) return null;
    final plane = image.planes.first;

    return InputImage.fromBytes(
      bytes: plane.bytes,
      metadata: InputImageMetadata(
        size: Size(image.width.toDouble(), image.height.toDouble()),
        rotation: rotation,
        format: format,
        bytesPerRow: plane.bytesPerRow,
      ),
    );
  }
}

/// Rotação do quadro para o ML Kit. Começa pela conta oficial (sensor +
/// orientação da tela); se passar [quadrosParaTrocar] quadros sem rosto,
/// tenta a próxima (a conta oficial erra em tablet cuja "posição natural" é
/// paisagem). Achou rosto em pé: trava, e vale para o app inteiro enquanto
/// ele estiver aberto (o tablet fica sempre na mesma posição). Travada e
/// [quadrosParaDestravar] quadros sem ninguém: volta a procurar.
class RotacaoCamera {
  RotacaoCamera({this.quadrosParaTrocar = 12, this.quadrosParaDestravar = 90});

  final int quadrosParaTrocar;
  final int quadrosParaDestravar;

  static int? _aprendida;
  int _deslocamento = 0;
  int _semRosto = 0;
  int? _ultima;

  /// Graus usados no último quadro (diagnóstico).
  int? get ultima => _ultima;
  bool get travada => _aprendida != null;

  /// Esquece a rotação aprendida (testes).
  static void esquecer() => _aprendida = null;

  int graus({required int sensor, required int tela, required bool frontal}) {
    final aprendida = _aprendida;
    if (aprendida != null) return _ultima = aprendida;
    final base = frontal ? (sensor + tela) % 360 : (sensor - tela + 360) % 360;
    return _ultima = (base + _deslocamento) % 360;
  }

  void registrar({required bool achouRosto}) {
    if (achouRosto) {
      _semRosto = 0;
      _aprendida ??= _ultima;
      return;
    }
    _semRosto++;
    if (_aprendida != null) {
      if (_semRosto >= quadrosParaDestravar) {
        _aprendida = null;
        _semRosto = 0;
      }
    } else if (_semRosto >= quadrosParaTrocar) {
      _semRosto = 0;
      _deslocamento = (_deslocamento + 90) % 360;
    }
  }
}

/// Heurística de "rosto frontal e centralizado": um único rosto, ângulos de
/// cabeça pequenos (encarando a câmera) e ocupando boa parte do quadro.
bool rostoFrontalOk(List<Face> rostos, Size tamanhoImagem) {
  if (rostos.length != 1) return false;
  final r = rostos.first;
  final yaw = r.headEulerAngleY ?? 90;
  final roll = r.headEulerAngleZ ?? 90;
  if (yaw.abs() > 15 || roll.abs() > 15) return false;

  final areaRosto = r.boundingBox.width * r.boundingBox.height;
  final areaImagem = tamanhoImagem.width * tamanhoImagem.height;
  if (areaImagem <= 0) return false;
  return areaRosto / areaImagem >= 0.06;
}

/// Média das probabilidades de olhos abertos (ou `null` se indisponível).
double? probOlhosAbertos(Face rosto) {
  final e = rosto.leftEyeOpenProbability;
  final d = rosto.rightEyeOpenProbability;
  if (e == null && d == null) return null;
  if (e == null) return d;
  if (d == null) return e;
  return (e + d) / 2;
}

/// Resume os rostos de um quadro para o [DesafioLiveness].
MedidaRosto medirRostos(List<Face> rostos, Size tamanhoImagem) {
  final um = rostos.length == 1 ? rostos.first : null;
  return MedidaRosto(
    quantidade: rostos.length,
    frontal: rostoFrontalOk(rostos, tamanhoImagem),
    olhosAbertos: um == null ? null : probOlhosAbertos(um),
    yaw: um?.headEulerAngleY,
    trackingId: um?.trackingId,
  );
}
