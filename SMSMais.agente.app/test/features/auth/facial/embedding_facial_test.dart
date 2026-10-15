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

  group('alinhamento no gabarito 112×112', () {
    (double, double) aplicar(
      ({double a, double b, double tx, double ty}) t,
      (double, double) p,
    ) => (t.a * p.$1 - t.b * p.$2 + t.tx, t.b * p.$1 + t.a * p.$2 + t.ty);

    test('desfaz escala e deslocamento', () {
      final foto = [for (final (u, v) in gabarito112) (u * 2 + 10, v * 2 + 20)];
      final t = semelhanca(foto, gabarito112);

      expect(t.a, closeTo(0.5, 1e-9));
      expect(t.b, closeTo(0, 1e-9));
      for (var i = 0; i < 5; i++) {
        final (u, v) = aplicar(t, foto[i]);
        expect(u, closeTo(gabarito112[i].$1, 1e-6));
        expect(v, closeTo(gabarito112[i].$2, 1e-6));
      }
    });

    test('desfaz cabeça inclinada (rotação)', () {
      const ang = 0.3;
      final foto = [
        for (final (u, v) in gabarito112)
          (u * cos(ang) - v * sin(ang), u * sin(ang) + v * cos(ang)),
      ];
      final t = semelhanca(foto, gabarito112);
      for (var i = 0; i < 5; i++) {
        final (u, v) = aplicar(t, foto[i]);
        expect(u, closeTo(gabarito112[i].$1, 1e-6));
        expect(v, closeTo(gabarito112[i].$2, 1e-6));
      }
    });

    test('ordena olhos e boca pela posição na imagem', () {
      final pontos = ordenarPontos(
        olhoA: (80, 50),
        olhoB: (40, 52),
        nariz: (60, 70),
        bocaA: (75, 90),
        bocaB: (45, 91),
      );
      expect(pontos, [(40, 52), (80, 50), (60, 70), (45, 91), (75, 90)]);
    });

    test('não espelha: o que está à esquerda na foto fica à esquerda', () {
      final foto = img.Image(width: 224, height: 224);
      for (var y = 0; y < 224; y++) {
        for (var x = 0; x < 224; x++) {
          foto.setPixelRgb(x, y, x < 112 ? 255 : 0, 0, x < 112 ? 0 : 255);
        }
      }
      final jpeg = Uint8List.fromList(img.encodeJpg(foto, quality: 95));
      final pontos = [for (final (u, v) in gabarito112) (u * 2, v * 2)];

      final r = prepararEntradaAlinhada(jpeg, pontos, comPng: true)!;

      expect(r.entrada, hasLength(112 * 112 * 3));
      double canal(int x, int y, int c) => r.entrada[(y * 112 + x) * 3 + c];
      expect(canal(10, 56, 0), greaterThan(0.8)); // vermelho à esquerda
      expect(canal(100, 56, 2), greaterThan(0.8)); // azul à direita
      expect(r.png, isNotNull);
    });
  });
}
