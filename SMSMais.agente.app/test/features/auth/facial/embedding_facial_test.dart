import 'dart:math';
import 'dart:typed_data';

import 'package:agente/features/auth/facial/data/embedding_facial.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image/image.dart' as img;

void main() {
  test('recorte ganha margem e fica dentro da imagem', () {
    expect(areaRecorte(640, 480, 100, 100, 200, 200), (80, 80, 240, 240));
    // Rosto encostado no canto: a margem não sai da foto.
    expect(areaRecorte(640, 480, 0, 0, 100, 100), (0, 0, 110, 110));
    expect(areaRecorte(640, 480, 560, 400, 100, 100), (550, 390, 90, 90));
  });

  test('normalização L2 deixa o vetor com norma 1', () {
    final v = normalizarL2(Float32List.fromList([3, 4]));
    expect(v[0], closeTo(0.6, 1e-6));
    expect(v[1], closeTo(0.8, 1e-6));
    final zero = Float32List(3);
    expect(normalizarL2(zero), same(zero));
  });

  test('similaridade: igual dá 1, perpendicular dá 0', () {
    final a = Float32List.fromList([1, 0]);
    final b = Float32List.fromList([0, 1]);
    expect(EmbeddingFacial.similaridade(a, a), closeTo(1, 1e-6));
    expect(EmbeddingFacial.similaridade(a, b), closeTo(0, 1e-6));
    expect(EmbeddingFacial.similaridade(a, Float32List(3)), 0);
  });

  test('entrada do modelo: 112×112×3 normalizada a partir do JPEG', () {
    final foto = img.Image(width: 200, height: 160);
    img.fill(foto, color: img.ColorRgb8(255, 0, 0));
    final jpeg = Uint8List.fromList(img.encodeJpg(foto));

    final entrada = prepararEntrada(jpeg, 40, 30, 100, 100)!;

    expect(entrada, hasLength(112 * 112 * 3));
    final vermelho = entrada[0];
    final verde = entrada[1];
    expect(vermelho, closeTo((255 - 127.5) / 128, 0.05));
    expect(verde, closeTo((0 - 127.5) / 128, 0.05));
    expect(entrada.reduce(max), lessThanOrEqualTo(1));
  });

  test('JPEG inválido não gera entrada', () {
    expect(prepararEntrada(Uint8List.fromList([1, 2, 3]), 0, 0, 1, 1), isNull);
  });
}
