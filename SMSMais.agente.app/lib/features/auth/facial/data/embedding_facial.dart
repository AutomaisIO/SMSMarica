import 'dart:io';
import 'dart:isolate';
import 'dart:math';
import 'dart:typed_data';
import 'dart:ui' show Rect;

import 'package:flutter/foundation.dart' show kReleaseMode;
import 'package:image/image.dart' as img;
import 'package:path_provider/path_provider.dart';
import 'package:tflite_flutter/tflite_flutter.dart';

/// Gera o vetor de identidade facial (embedding) com **MobileFaceNet** rodando
/// no tablet via TFLite (o TFLite vai no APK; não usa o do Play Services).
/// Entrada 112×112×3 normalizada; saída L2-normalizada.
///
/// O modelo é opcional em build: enquanto `assets/models/mobilefacenet.tflite`
/// não existir, [disponivel] fica `false` e o app degrada (só liveness).
class EmbeddingFacial {
  static const _ativo = 'assets/models/mobilefacenet.tflite';

  /// Muda quando muda o preparo da imagem: os vetores antigos não servem e
  /// o tablet gera de novo a partir da foto de referência.
  static const versaoModelo = 'mobilefacenet-v2-alinhado';
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

  /// Gera o embedding a partir de uma foto e do rosto detectado nela. Com os
  /// 5 [pontos] do rosto (olhos, nariz, cantos da boca), alinha no gabarito
  /// em que o MobileFaceNet foi treinado; sem eles, recorta a caixa [rosto].
  /// Retorna `null` se o modelo não estiver disponível ou a imagem inválida.
  ///
  /// Nos builds de teste, o recorte 112×112 que entrou no modelo fica em
  /// `app_flutter/diagnostico_rosto/` (rotulado com [rotulo]) para conferir.
  Future<Float32List?> gerarEmbedding(
    String caminhoFoto,
    Rect rosto, {
    List<(double, double)>? pontos,
    String rotulo = 'rosto',
  }) async {
    final interpreter = _interpreter;
    if (interpreter == null) return null;

    final bytes = await File(caminhoFoto).readAsBytes();
    final (x, y, w, h) = (rosto.left, rosto.top, rosto.width, rosto.height);
    const png = !kReleaseMode;
    // Decodificar JPEG em Dart puro leva centenas de ms no tablet: fora do
    // isolate da tela para não congelar a interface.
    final preparada = await Isolate.run(
      () => pontos != null && pontos.length == 5
          ? prepararEntradaAlinhada(bytes, pontos, comPng: png)
          : EntradaPreparada.de(prepararEntrada(bytes, x, y, w, h)),
    );
    final entrada = preparada?.entrada;
    if (entrada == null) return null;
    if (preparada?.png case final p?) await _guardarDiagnostico(p, rotulo);

    // Saída como ByteBuffer: é o formato que o tflite_flutter 0.11 preenche
    // sem passar por listas aninhadas.
    final saida = Float32List(_dimensaoSaida);
    interpreter.run(entrada.buffer.asUint8List(), saida.buffer);
    return normalizarL2(saida);
  }

  /// Últimos recortes que entraram no modelo (só builds de teste).
  static Future<void> _guardarDiagnostico(Uint8List png, String rotulo) async {
    try {
      final base = await getApplicationDocumentsDirectory();
      final dir = Directory('${base.path}/diagnostico_rosto');
      await dir.create(recursive: true);
      final nome = '${DateTime.now().millisecondsSinceEpoch}_$rotulo.png';
      await File('${dir.path}/$nome').writeAsBytes(png);
      final arquivos = dir.listSync().whereType<File>().toList()
        ..sort((a, b) => a.path.compareTo(b.path));
      for (final f in arquivos.take(max(0, arquivos.length - 30))) {
        await f.delete();
      }
    } on Object catch (_) {}
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

/// Entrada pronta para o modelo e, nos builds de teste, o recorte em PNG.
class EntradaPreparada {
  const EntradaPreparada(this.entrada, [this.png]);

  final Float32List entrada;
  final Uint8List? png;

  static EntradaPreparada? de(Float32List? entrada) =>
      entrada == null ? null : EntradaPreparada(entrada);
}

/// Gabarito 112×112 dos rostos alinhados em que o MobileFaceNet (como o
/// ArcFace/InsightFace) é treinado, na ordem da imagem: olho da esquerda,
/// olho da direita, nariz, canto da boca da esquerda, da direita.
const gabarito112 = <(double, double)>[
  (38.2946, 51.6963),
  (73.5318, 51.5014),
  (56.0252, 71.7366),
  (41.5493, 92.3655),
  (70.7299, 92.2041),
];

/// Põe os 5 pontos na ordem do [gabarito112] pela posição NA IMAGEM (menor x
/// primeiro), sem depender de "esquerda/direita da pessoa" — que muda se a
/// foto da câmera frontal vier espelhada.
List<(double, double)> ordenarPontos({
  required (double, double) olhoA,
  required (double, double) olhoB,
  required (double, double) nariz,
  required (double, double) bocaA,
  required (double, double) bocaB,
}) {
  final olhos = olhoA.$1 <= olhoB.$1 ? [olhoA, olhoB] : [olhoB, olhoA];
  final boca = bocaA.$1 <= bocaB.$1 ? [bocaA, bocaB] : [bocaB, bocaA];
  return [...olhos, nariz, ...boca];
}

/// Semelhança (escala, rotação e deslocamento, sem espelhar) que leva
/// [origem] o mais perto possível de [destino], por mínimos quadrados:
/// u = a·x − b·y + tx ; v = b·x + a·y + ty.
({double a, double b, double tx, double ty}) semelhanca(
  List<(double, double)> origem,
  List<(double, double)> destino,
) {
  final n = origem.length;
  var mx = 0.0;
  var my = 0.0;
  var mu = 0.0;
  var mv = 0.0;
  for (var i = 0; i < n; i++) {
    mx += origem[i].$1;
    my += origem[i].$2;
    mu += destino[i].$1;
    mv += destino[i].$2;
  }
  mx /= n;
  my /= n;
  mu /= n;
  mv /= n;
  var num1 = 0.0;
  var num2 = 0.0;
  var den = 0.0;
  for (var i = 0; i < n; i++) {
    final x = origem[i].$1 - mx;
    final y = origem[i].$2 - my;
    final u = destino[i].$1 - mu;
    final v = destino[i].$2 - mv;
    num1 += x * u + y * v;
    num2 += x * v - y * u;
    den += x * x + y * y;
  }
  final a = den == 0 ? 1.0 : num1 / den;
  final b = den == 0 ? 0.0 : num2 / den;
  return (a: a, b: b, tx: mu - (a * mx - b * my), ty: mv - (b * mx + a * my));
}

/// Decodifica o JPEG e leva o rosto para o [gabarito112] pelos 5 [pontos]
/// (na ordem de [ordenarPontos]), com amostragem bilinear; entrada em float
/// normalizada para (x − 127,5) / 128.
EntradaPreparada? prepararEntradaAlinhada(
  Uint8List jpeg,
  List<(double, double)> pontos, {
  bool comPng = false,
  int lado = EmbeddingFacial.ladoEntrada,
}) {
  img.Image? original;
  try {
    original = img.decodeImage(jpeg);
  } on Object {
    return null;
  }
  if (original == null) return null;
  final escala = lado / EmbeddingFacial.ladoEntrada;
  final destino = [for (final (u, v) in gabarito112) (u * escala, v * escala)];
  final t = semelhanca(pontos, destino);
  final det = t.a * t.a + t.b * t.b;
  if (det == 0) return null;

  final out = Float32List(lado * lado * 3);
  final quadro = comPng ? img.Image(width: lado, height: lado) : null;
  final w = original.width;
  final h = original.height;
  var i = 0;
  for (var v = 0; v < lado; v++) {
    for (var u = 0; u < lado; u++) {
      // Volta do pixel de saída para a foto (inversa da semelhança).
      final du = u - t.tx;
      final dv = v - t.ty;
      final x = (t.a * du + t.b * dv) / det;
      final y = (-t.b * du + t.a * dv) / det;
      final (r, g, b) = _bilinear(original, x, y, w, h);
      out[i++] = (r - 127.5) / 128;
      out[i++] = (g - 127.5) / 128;
      out[i++] = (b - 127.5) / 128;
      quadro?.setPixelRgb(u, v, r.round(), g.round(), b.round());
    }
  }
  return EntradaPreparada(
    out,
    quadro == null ? null : Uint8List.fromList(img.encodePng(quadro)),
  );
}

(double, double, double) _bilinear(
  img.Image im,
  double x,
  double y,
  int w,
  int h,
) {
  final x0 = x.floor().clamp(0, w - 1);
  final y0 = y.floor().clamp(0, h - 1);
  final x1 = (x0 + 1).clamp(0, w - 1);
  final y1 = (y0 + 1).clamp(0, h - 1);
  final fx = (x - x0).clamp(0.0, 1.0);
  final fy = (y - y0).clamp(0.0, 1.0);
  final p00 = im.getPixel(x0, y0);
  final p10 = im.getPixel(x1, y0);
  final p01 = im.getPixel(x0, y1);
  final p11 = im.getPixel(x1, y1);
  double mistura(num c00, num c10, num c01, num c11) =>
      (c00 * (1 - fx) + c10 * fx) * (1 - fy) + (c01 * (1 - fx) + c11 * fx) * fy;
  return (
    mistura(p00.r, p10.r, p01.r, p11.r),
    mistura(p00.g, p10.g, p01.g, p11.g),
    mistura(p00.b, p10.b, p01.b, p11.b),
  );
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
