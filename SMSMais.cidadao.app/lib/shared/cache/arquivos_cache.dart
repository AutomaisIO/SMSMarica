import 'dart:typed_data';

import 'package:sms_mais_cidadao/shared/cache/arquivos_cache_memoria.dart'
    if (dart.library.io) 'package:sms_mais_cidadao/shared/cache/arquivos_cache_disco.dart' as impl;

/// Cache dos DOCUMENTOS (PDF de laudo, anexos, PDF das imagens, fotos do acervo) no aparelho —
/// equivalente ao `lib/pdfCache.ts` do PWA (IndexedDB, LRU de 40). A 1ª abertura baixa e salva;
/// as próximas abrem direto do aparelho, sem rede. Chave = URL do conteúdo na API.
///
/// No celular fica em arquivo (pasta de suporte do app, fora da galeria); na web (só usada para
/// conferir telas) fica em memória.
abstract final class ArquivosCache {
  static const limite = 40;

  static Future<Uint8List?> obter(String url) => impl.obter(url);

  static Future<void> salvar(String url, Uint8List dados) => impl.salvar(url, dados, limite);

  /// FAXINA: tira do aparelho o que não está mais na lista do paciente (exame que deixou de ser
  /// dele por correção de identidade, exclusão, cancelamento…).
  static Future<void> manterApenas(Iterable<String> urls) => impl.manterApenas(urls.toSet());

  /// Logout (LGPD): o aparelho pode ser compartilhado.
  static Future<void> limpar() => impl.limpar();
}
