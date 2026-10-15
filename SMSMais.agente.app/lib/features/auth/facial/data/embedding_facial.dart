import 'dart:io';
import 'dart:isolate';
import 'dart:math';
import 'dart:typed_data';
import 'dart:ui' show Rect;

import 'package:image/image.dart' as img;
import 'package:tflite_flutter/tflite_flutter.dart';

/// Gera o vetor de identidade facial (embedding) com **MobileFaceNet** rodando
/// no tablet via TFLite (o TFLite vai no APK; não usa o do Play Services).
/// Entrada 112×112×3 normalizada; saída L2-normalizada.
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
      // O tablet tem 8 núcleos fracos (Snapdragon 625): 4 threads em CPU.
      final interpreter = await Interpreter.fromAsset(
        _ativo,
        options: InterpreterOptions()..threads = 4,
      );
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
    final (x, y, w, h) = (rosto.left, rosto.top, rosto.width, rosto.height);
    // Decodificar JPEG em Dart puro leva centenas de ms no tablet: fora do
    // isolate da tela para não congelar a interface.
    final entrada = await Isolate.run(() => prepararEntrada(bytes, x, y, w, h));
    if (entrada == null) return null;

    // Saída como ByteBuffer: é o formato que o tflite_flutter 0.11 preenche
    // sem passar por listas aninhadas.
    final saida = Float32List(_dimensaoSaida);
    interpreter.run(entrada.buffer.asUint8List(), saida.buffer);
    return normalizarL2(saida);
  }

  /// Similaridade de cosseno entre dois embeddings já L2-normalizados.
  static double similaridade(Float32List a, Float32List b) {
    if (a.length != b.length) return 0;
    var produto = 0.0;
    for (var i = 0; i < a.length; i++) {
      produto += a[i] * b[i];
    }
    return produto.clamp(-1.0, 1.0);
  }
}

/// Quanto o recorte cresce além da caixa do ML Kit, em cada lado. A caixa é
/// justa no rosto; o MobileFaceNet foi treinado com um pouco de margem.
const margemRecorte = 0.1;

/// Decodifica o JPEG, recorta o rosto (com [margemRecorte]) e monta a
/// entrada 112×112×3 em float, normalizada para (x − 127,5) / 128.
Float32List? prepararEntrada(
  Uint8List jpeg,
  double x,
  double y,
  double largura,
  double altura, {
  int lado = EmbeddingFacial.ladoEntrada,
}) {
  img.Image? original;
  try {
    original = img.decodeImage(jpeg);
  } on Object {
    return null; // arquivo corrompido: o chamador trata como "sem rosto"
  }
  if (original == null) return null;
  final (cx, cy, cw, ch) = areaRecorte(
    original.width,
    original.height,
    x,
    y,
    largura,
    altura,
  );
  final recorte = img.copyCrop(original, x: cx, y: cy, width: cw, height: ch);
  final quadro = img.copyResize(
    recorte,
    width: lado,
    height: lado,
    interpolation: img.Interpolation.linear,
  );

  final out = Float32List(lado * lado * 3);
  var i = 0;
  for (var py = 0; py < lado; py++) {
    for (var px = 0; px < lado; px++) {
      final p = quadro.getPixel(px, py);
      out[i++] = (p.r - 127.5) / 128;
      out[i++] = (p.g - 127.5) / 128;
      out[i++] = (p.b - 127.5) / 128;
    }
  }
  return out;
}

/// Caixa do rosto + margem, presa dentro da imagem: (x, y, largura, altura).
(int, int, int, int) areaRecorte(
  int larguraImagem,
  int alturaImagem,
  double x,
  double y,
  double largura,
  double altura,
) {
  final mx = largura * margemRecorte;
  final my = altura * margemRecorte;
  final esq = (x - mx).floor().clamp(0, larguraImagem - 1);
  final topo = (y - my).floor().clamp(0, alturaImagem - 1);
  final dir = (x + largura + mx).ceil().clamp(esq + 1, larguraImagem);
  final base = (y + altura + my).ceil().clamp(topo + 1, alturaImagem);
  return (esq, topo, dir - esq, base - topo);
}

/// Divide o vetor pela norma (vetor zero volta como está).
Float32List normalizarL2(Float32List v) {
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
