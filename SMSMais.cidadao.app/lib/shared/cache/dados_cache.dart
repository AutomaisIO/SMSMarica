import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

/// Cache local dos DADOS do cidadão (listas/JSON) para acesso OFFLINE — sinal fraco e falta de
/// plano de dados são realidade do público (espelho de `lib/dadosCache.ts` do PWA).
///
/// REGRA: a rede vem SEMPRE primeiro (nunca servimos cache estando online); o cache só responde
/// quando a rede falha, e o app mostra a faixa "Sem conexão". Chaves incluem o id do paciente
/// (aparelho compartilhado não vaza dados entre contas) e tudo é limpo no logout (LGPD).
class DadosCache {
  const DadosCache(this.pacienteId);

  final String? pacienteId;

  static const _prefixo = 'sms.dados.';

  String? _chave(String chave) => pacienteId == null ? null : '$_prefixo$pacienteId.$chave';

  Future<void> salvar(String chave, Object? dados) async {
    final k = _chave(chave);
    if (k == null) return;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(k, jsonEncode({'em': DateTime.now().millisecondsSinceEpoch, 'dados': dados}));
    } on Object {
      /* cache é best-effort */
    }
  }

  Future<Object?> ler(String chave) async {
    final k = _chave(chave);
    if (k == null) return null;
    try {
      final prefs = await SharedPreferences.getInstance();
      final bruto = prefs.getString(k);
      if (bruto == null) return null;
      return (jsonDecode(bruto) as Map<String, dynamic>)['dados'];
    } on Object {
      return null;
    }
  }

  /// Rede-primeiro com fallback offline: tenta a rede (e atualiza o cache); se falhar E houver
  /// cache local, devolve o cache em vez de quebrar a tela. Sem cache, o erro segue normal.
  ///
  /// [buscar] devolve o JSON cru (o que vai para o cache); [converter] monta o modelo.
  Future<T> comCache<T>(String chave, Future<Object?> Function() buscar, T Function(Object?) converter) async {
    try {
      final dados = await buscar();
      await salvar(chave, dados);
      return converter(dados);
    } on Object {
      final cache = await ler(chave);
      if (cache != null) return converter(cache);
      rethrow;
    }
  }

  /// Remove TODOS os dados locais (todas as contas) — chamado no logout (LGPD).
  static Future<void> limparTudo() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      for (final k in prefs.getKeys().where((k) => k.startsWith(_prefixo)).toList()) {
        await prefs.remove(k);
      }
    } on Object {
      /* best-effort */
    }
  }
}
