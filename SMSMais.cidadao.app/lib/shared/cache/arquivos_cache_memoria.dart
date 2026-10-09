import 'dart:typed_data';

// Implementação em memória (web) do ArquivosCache — some ao recarregar a página.

final _mapa = <String, Uint8List>{}; // LinkedHashMap: mantém a ordem de inserção

Future<Uint8List?> obter(String url) async {
  final v = _mapa.remove(url);
  if (v != null) _mapa[url] = v; // mais recente no fim (LRU)
  return v;
}

Future<void> salvar(String url, Uint8List dados, int limite) async {
  _mapa
    ..remove(url)
    ..[url] = dados;
  while (_mapa.length > limite) {
    _mapa.remove(_mapa.keys.first);
  }
}

Future<void> manterApenas(Set<String> urls) async => _mapa.removeWhere((k, _) => !urls.contains(k));

Future<void> limpar() async => _mapa.clear();
