import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Chave de um banco SQLCipher local, guardada sob [nome].
///
/// Gerada aleatoriamente (32 bytes) uma única vez e selada no
/// `flutter_secure_storage` (Android Keystore, hardware-backed). Sem ela o
/// arquivo do banco é ilegível.
Future<String> obterOuCriarChaveBanco(
  FlutterSecureStorage secure,
  String nome,
) async {
  final existente = await secure.read(key: nome);
  if (existente != null) return existente;
  final rng = Random.secure();
  final bytes = Uint8List.fromList(
    List<int>.generate(32, (_) => rng.nextInt(256)),
  );
  final chave = base64UrlEncode(bytes);
  await secure.write(key: nome, value: chave);
  return chave;
}
