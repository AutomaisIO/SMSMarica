import 'dart:ui' show Size;

import 'package:camera/camera.dart';
import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';

/// Wrapper do ML Kit Face Detection.
///
/// Detecta rostos em frames da câmera (liveness/posicionamento) e em arquivos
/// (recorte que alimenta o embedding). Habilita classificação (olhos abertos)
/// e landmarks (posição).
class DetectorRosto {
  DetectorRosto()
      : _detector = FaceDetector(
          options: FaceDetectorOptions(
            enableClassification: true,
            enableLandmarks: true,
            minFaceSize: 0.15,
          ),
        );

  final FaceDetector _detector;

  /// Detecta a partir de um frame da câmera (stream). Retorna `null` quando o
  /// formato do frame não pôde ser convertido nesta plataforma.
  Future<List<Face>> detectarDoFrame(
    CameraImage image,
    CameraDescription camera,
  ) async {
    final input = _inputImageDoFrame(image, camera);
    if (input == null) return const [];
    return _detector.processImage(input);
  }

  /// Detecta a partir de um arquivo (foto tirada / foto de referência baixada).
  Future<List<Face>> detectarDoArquivo(String caminho) {
    return _detector.processImage(InputImage.fromFilePath(caminho));
  }

  void dispose() => _detector.close();

  InputImage? _inputImageDoFrame(
    CameraImage image,
    CameraDescription camera,
  ) {
    final rotation =
        InputImageRotationValue.fromRawValue(camera.sensorOrientation);
    final format =
        InputImageFormatValue.fromRawValue(image.format.raw as int);
    if (rotation == null || format == null) return null;

    // Configuramos a câmera para nv21 (Android) / bgra8888 (outros): plano único.
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
