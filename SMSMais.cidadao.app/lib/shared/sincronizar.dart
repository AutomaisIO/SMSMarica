import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/cache/arquivos_cache.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// Sincronização OFFLINE-FIRST (`lib/sincronizar.ts` do PWA): ao entrar no app, baixa em segundo
/// plano tudo do paciente — listas (o próprio [ApiCidadao] já grava o cache) e os DOCUMENTOS
/// (laudos assinados, anexos e PDFs de imagens). Com sinal fraco ou sem plano de dados, o cidadão
/// continua vendo tudo o que já foi sincronizado.
///
/// Regras: roda 1x por entrada (por paciente), sequencial (não disputa banda com a tela), pula o
/// que já está no aparelho, para se ficar offline e NUNCA quebra nada (best-effort).
class Sincronizador {
  Sincronizador(this._ref);

  final Ref _ref;
  String? _sincronizadoPara;

  void sincronizarTudo() {
    final pacienteId = _ref.read(sessaoProvider)?.paciente.id;
    if (pacienteId == null || _sincronizadoPara == pacienteId) return;
    _sincronizadoPara = pacienteId;
    _executar(pacienteId).ignore();
  }

  /// Permite re-sincronizar após novo login (logout limpa a marca).
  void resetar() => _sincronizadoPara = null;

  bool _mesmaSessao(String pacienteId) => _ref.read(sessaoProvider)?.paciente.id == pacienteId;

  Future<void> _executar(String pacienteId) async {
    try {
      final api = _ref.read(apiProvider);
      Future<T?> talvez<T>(Future<T> f) => f.then<T?>((v) => v).catchError((Object _) => null);

      // 1. Listas (cada chamada grava o cache local de dados).
      final exames = await talvez(api.exames()) ?? const <Exame>[];
      final laudos = await talvez(api.laudos()) ?? const <Laudo>[];
      final resto = await Future.wait<Object?>([
        talvez(api.documentos()),
        talvez(api.agendamentos('exame')),
        talvez(api.agendamentos('consulta')),
        talvez(api.atendimentos()),
        talvez(api.perfil()),
      ]);
      final documentos = (resto.first as List<ItemAcervo>?) ?? const <ItemAcervo>[];

      // 2. Documentos, do mais leve/valioso ao mais pesado: laudos → anexos → imagens.
      final fila = <String>[
        ...laudos.map((l) => UrlsConteudo.laudo(l.id)),
        ...exames.expand((e) => e.documentos.map((d) => UrlsConteudo.anexo(d.id))),
        ...exames.where((e) => e.temImagens).map((e) => UrlsConteudo.exameImagens(e.id)),
      ];

      for (final url in fila) {
        if (!_mesmaSessao(pacienteId)) return; // saiu ou trocou de conta no meio
        final rede = await Connectivity().checkConnectivity();
        if (rede.contains(ConnectivityResult.none)) return; // sem rede → tenta na próxima entrada
        try {
          if (await ArquivosCache.obter(url) != null) continue; // já está no aparelho
          final r = await api.baixar(url);
          await ArquivosCache.salvar(url, r.bytes);
        } on Object {
          /* um documento falhou (ex.: PACS fora) — segue para o próximo */
        }
      }

      // 3. FAXINA: o que não está mais na lista do paciente sai do aparelho. O que o cidadão
      //    abriu em Documentos continua enquanto estiver no acervo.
      if (_mesmaSessao(pacienteId)) {
        await ArquivosCache.manterApenas([...fila, ...documentos.map(UrlsConteudo.acervo)]);
      }
    } on Object {
      /* sincronização é best-effort — nunca afeta a navegação */
    }
  }
}

final sincronizadorProvider = Provider<Sincronizador>(Sincronizador.new);
