import 'dart:convert';
import 'dart:io';
import 'dart:isolate';
import 'dart:typed_data';

import 'package:image/image.dart' as img;

/// Lê o JPEG da câmera e devolve outro com no máximo [largura] px de lado
/// maior, em [qualidade], já com a rotação do EXIF aplicada. Fora do isolate
/// da tela (JPEG em Dart puro é lento no tablet). Null se não for imagem.
Future<Uint8List?> jpegReduzido(
  String caminho, {
  required int largura,
  int qualidade = 80,
}) async {
  final bytes = await File(caminho).readAsBytes();
  return Isolate.run(() => reduzirJpeg(bytes, largura, qualidade));
}

Future<String?> jpegReduzidoBase64(
  String caminho, {
  required int largura,
  int qualidade = 80,
}) async {
  final r = await jpegReduzido(caminho, largura: largura, qualidade: qualidade);
  return r == null ? null : base64Encode(r);
}

Uint8List? reduzirJpeg(Uint8List bytes, int largura, int qualidade) {
  img.Image? original;
  try {
    original = img.decodeImage(bytes);
  } on Object {
    return null;
  }
  if (original == null) return null;
  final maior = original.width >= original.height
      ? original.width
      : original.height;
  final menor = maior <= largura
      ? original
      : original.width >= original.height
      ? img.copyResize(original, width: largura)
      : img.copyResize(original, height: largura);
  return img.encodeJpg(menor, quality: qualidade);
}
