import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:path_provider/path_provider.dart';

// Implementação em disco do ArquivosCache: um arquivo por URL na pasta de suporte do app (não
// aparece na galeria nem em "Arquivos"); a data de modificação serve de "último uso" para o LRU.

Directory? _pasta;

Future<Directory> _diretorio() async {
  if (_pasta != null) return _pasta!;
  final base = await getApplicationSupportDirectory();
  final d = Directory('${base.path}${Platform.pathSeparator}documentos');
  if (!d.existsSync()) await d.create(recursive: true);
  return _pasta = d;
}

/// Nome de arquivo estável e seguro a partir da URL.
String _nome(String url) => base64Url.encode(utf8.encode(url)).replaceAll('=', '');

Future<File> _arquivo(String url) async => File('${(await _diretorio()).path}${Platform.pathSeparator}${_nome(url)}');

Future<Uint8List?> obter(String url) async {
  try {
    final f = await _arquivo(url);
    if (!f.existsSync()) return null;
    final dados = await f.readAsBytes();
    await f.setLastModified(DateTime.now()); // marca o uso (LRU)
    return dados;
  } on Object {
    return null;
  }
}

Future<void> salvar(String url, Uint8List dados, int limite) async {
  try {
    await (await _arquivo(url)).writeAsBytes(dados, flush: true);
    final arquivos = (await _diretorio()).listSync().whereType<File>().toList()
      ..sort((a, b) => a.lastModifiedSync().compareTo(b.lastModifiedSync()));
    for (var i = 0; i < arquivos.length - limite; i++) {
      await arquivos[i].delete();
    }
  } on Object {
    /* best-effort: sem espaço, segue sem cache */
  }
}

Future<void> manterApenas(Set<String> urls) async {
  try {
    final manter = urls.map(_nome).toSet();
    for (final f in (await _diretorio()).listSync().whereType<File>()) {
      if (!manter.contains(f.uri.pathSegments.last)) await f.delete();
    }
  } on Object {
    /* best-effort */
  }
}

Future<void> limpar() async {
  try {
    final d = await _diretorio();
    if (d.existsSync()) await d.delete(recursive: true);
    _pasta = null;
  } on Object {
    /* best-effort */
  }
}
