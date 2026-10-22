import 'dart:convert';

import 'package:agente/features/ocorrencia/domain/ocorrencia.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// REPORTs à espera do servidor, guardados cifrados no tablet: sobrevivem a
/// app fechado e tablet reiniciado sem rede. São poucos (botão de exceção),
/// então cabem numa lista JSON no `flutter_secure_storage`.
class FilaOcorrencias {
  FilaOcorrencias({FlutterSecureStorage? storage})
    : _storage = storage ?? _padrao;

  static const _padrao = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );
  static const _chave = 'ocorrencias_pendentes';

  final FlutterSecureStorage _storage;

  /// Serializa as gravações: ler-alterar-gravar não pode se cruzar.
  Future<void> _fila = Future<void>.value();

  Future<List<OcorrenciaPendente>> pendentes() async {
    final s = await _storage.read(key: _chave);
    if (s == null || s.isEmpty) return const [];
    try {
      return [
        for (final item in jsonDecode(s) as List<dynamic>)
          OcorrenciaPendente.fromJson(item as Map<String, dynamic>),
      ];
    } on Object catch (_) {
      return const [];
    }
  }

  Future<void> adicionar(OcorrenciaPendente o) =>
      _alterar((lista) => [...lista, o]);

  Future<void> remover(String idLocal) => _alterar(
    (lista) => [
      for (final o in lista)
        if (o.idLocal != idLocal) o,
    ],
  );

  Future<void> limpar() => _alterar((_) => const []);

  Future<void> _alterar(
    List<OcorrenciaPendente> Function(List<OcorrenciaPendente>) mudar,
  ) {
    final proxima = _fila.then((_) async {
      final nova = mudar(await pendentes());
      await _storage.write(
        key: _chave,
        value: jsonEncode([for (final o in nova) o.toJson()]),
      );
    });
    _fila = proxima.catchError((Object _) {});
    return proxima;
  }
}

final filaOcorrenciasProvider = Provider<FilaOcorrencias>(
  (_) => FilaOcorrencias(),
);
