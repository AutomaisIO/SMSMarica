import 'package:agente/features/auth/facial/data/detector_rosto.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  setUp(RotacaoCamera.esquecer);

  int graus(RotacaoCamera r) => r.graus(sensor: 270, tela: 90, frontal: true);

  test('começa pela conta oficial da câmera frontal', () {
    expect(graus(RotacaoCamera()), 0); // (270 + 90) % 360
    expect(
      RotacaoCamera().graus(sensor: 90, tela: 90, frontal: false),
      0, // (90 − 90) % 360
    );
  });

  test('sem rosto por um tempo, tenta a próxima rotação', () {
    final r = RotacaoCamera(quadrosParaTrocar: 3);
    for (var i = 0; i < 3; i++) {
      graus(r);
      r.registrar(achouRosto: false);
    }
    expect(graus(r), 90);
  });

  test('achou rosto: trava, e vale para a próxima captura', () {
    final r = RotacaoCamera(quadrosParaTrocar: 1);
    graus(r);
    r.registrar(achouRosto: false);
    expect(graus(r), 90);
    r.registrar(achouRosto: true);
    expect(r.travada, isTrue);

    final outra = RotacaoCamera();
    expect(graus(outra), 90);
  });

  test('travada e muito tempo sem ninguém: volta a procurar', () {
    final r = RotacaoCamera(quadrosParaTrocar: 1, quadrosParaDestravar: 2);
    graus(r);
    r.registrar(achouRosto: true);
    expect(r.travada, isTrue);

    r
      ..registrar(achouRosto: false)
      ..registrar(achouRosto: false);
    expect(r.travada, isFalse);
  });
}
