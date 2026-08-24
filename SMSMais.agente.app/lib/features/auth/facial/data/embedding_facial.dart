import 'dart:io';
import 'dart:math';
import 'dart:typed_data';
import 'dart:ui' show Rect;

import 'package:image/image.dart' as img;
import 'package:tflite_flutter/tflite_flutter.dart';

/// Gera o vetor de identidade facial (embedding) com **MobileFaceNet** rodando
/// on-device via TFLite. Entrada 112×112×3 normalizada; saída L2-normalizada.
///
/// O modelo é opcional em build: enquanto `assets/models/mobilefacenet.tflite`
/// não existir, [disponivel] fica `false` e o app degrada (só liveness).
class EmbeddingFacial {
  static const _ativo = 'assets/models/mobilefacenet.tflite';
  static const versaoModelo = 'mobilefacenet-v1';
  static const ladoEntrada = 112;

  Interpreter? _interpreter;
  int _dimensaoSaida = 192;

  bool get disponivel => _interpreter != null;
  String get versao => versaoModelo;

  Future<void> carregar() async {
    if (_interpreter != null) return;
    try {
      final interpreter = await Interpreter.fromAsset(_ativo);
      final saida = interpreter.getOutputTensor(0).shape;
      _dimensaoSaida = saida.last;
      _interpreter = interpreter;
    } on Object {
      _interpreter = null; // modelo ainda não embarcado
    }
  }

  void dispose() {
    _interpreter?.close();
    _interpreter = null;
  }

  /// Gera o embedding a partir de uma foto e do retângulo do rosto detectado.
  /// Retorna `null` se o modelo não estiver disponível ou a imagem inválida.
  Future<Float32List?> gerarEmbedding(String caminhoFoto, Rect rosto) async {
    final interpreter = _interpreter;
    if (interpreter == null) return null;

    final bytes = await File(caminhoFoto).readAsBytes();
    final original = img.decodeImage(bytes);
    if (original == null) return null;

    final recorte = _recortar(original, rosto);
    final redimensionado = img.copyResize(
      recorte,
      width: ladoEntrada,
      height: ladoEntrada,
    );

    final entrada = _normalizar(redimensionado);
    final saida = [List<double>.filled(_dimensaoSaida, 0)];
    interpreter.run(entrada, saida);

    return _l2(Float32List.fromList(saida[0]));
  }

  img.Image _recortar(img.Image origem, Rect rosto) {
    final x = rosto.left.clamp(0, origem.width - 1).toInt();
    final y = rosto.top.clamp(0, origem.height - 1).toInt();
    final w = rosto.width.clamp(1, origem.width - x).toInt();
    final h = rosto.height.clamp(1, origem.height - y).toInt();
    return img.copyCrop(origem, x: x, y: y, width: w, height: h);
  }

  List<List<List<List<double>>>> _normalizar(img.Image imagem) {
    return [
      List.generate(
        ladoEntrada,
        (y) => List.generate(ladoEntrada, (x) {
          final px = imagem.getPixel(x, y);
          return [
            (px.r - 127.5) / 128.0,
            (px.g - 127.5) / 128.0,
            (px.b - 127.5) / 128.0,
          ];
        }),
      ),
    ];
  }

  Float32List _l2(Float32List v) {
    var soma = 0.0;
    for (final x in v) {
      soma += x * x;
    }
    final norma = sqrt(soma);
    if (norma == 0) return v;
    final out = Float32List(v.length);
    for (var i = 0; i < v.length; i++) {
      out[i] = v[i] / norma;
    }
    return out;
  }

  /// Similaridade de cosseno entre dois embeddings já L2-normalizados (0..1).
  static double similaridade(Float32List a, Float32List b) {
    if (a.length != b.length) return 0;
    var produto = 0.0;
    for (var i = 0; i < a.length; i++) {
      produto += a[i] * b[i];
    }
    return produto.clamp(-1.0, 1.0);
  }
}
