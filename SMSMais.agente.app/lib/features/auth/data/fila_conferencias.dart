import 'dart:convert';

import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/shared/armazenamento/chave_banco.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite_sqlcipher/sqflite.dart';

/// Conferências do motorista esperando rede para chegar ao servidor (ADR-0073).
abstract interface class FilaConferencias {
  /// Guarda no máximo [limite]; passou, sai a mais antiga.
  static const limite = 100;

  Future<void> adicionar(ConferenciaPendente c);

  /// Da mais antiga para a mais nova.
  Future<List<ConferenciaPendente>> pendentes();

  Future<void> remover(String idLocal);
}

/// Em memória: fallback quando o disco não abre, e base dos testes.
class FilaConferenciasMemoria implements FilaConferencias {
  final List<ConferenciaPendente> _itens = [];

  @override
  Future<void> adicionar(ConferenciaPendente c) async {
    _itens
      ..removeWhere((x) => x.idLocal == c.idLocal)
      ..add(c);
    if (_itens.length > FilaConferencias.limite) _itens.removeAt(0);
  }

  @override
  Future<List<ConferenciaPendente>> pendentes() async => List.of(_itens);

  @override
  Future<void> remover(String idLocal) async =>
      _itens.removeWhere((x) => x.idLocal == idLocal);
}

/// Em disco cifrado (SQLCipher), porque leva a selfie: sobrevive a app
/// fechado e tablet reiniciado sem rede. Mesma receita da fila de posições.
class FilaConferenciasDisco implements FilaConferencias {
  FilaConferenciasDisco({FlutterSecureStorage? secureStorage})
    : _secure = secureStorage ?? _padrao;

  static const _padrao = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );
  static const _chaveBanco = 'conferencias_db_key';
  static const _arquivoBanco = 'conferencias.db';
  static const _tabela = 'conferencia_pendente';

  final FlutterSecureStorage _secure;
  final FilaConferenciasMemoria _memoria = FilaConferenciasMemoria();
  Future<Database?>? _abertura;

  Future<Database?> get _db => _abertura ??= _abrir();

  Future<Database?> _abrir() async {
    try {
      final dir = await getApplicationDocumentsDirectory();
      return await openDatabase(
        p.join(dir.path, _arquivoBanco),
        password: await obterOuCriarChaveBanco(_secure, _chaveBanco),
        version: 1,
        onCreate: (db, _) => db.execute('''
          CREATE TABLE $_tabela (
            id_local   TEXT PRIMARY KEY,
            json       TEXT NOT NULL,
            criada_em  INTEGER NOT NULL
          )
        '''),
      );
    } on Object catch (_) {
      return null;
    }
  }

  @override
  Future<void> adicionar(ConferenciaPendente c) async {
    final db = await _db;
    if (db == null) return _memoria.adicionar(c);
    await db.transaction((txn) async {
      await txn.insert(_tabela, {
        'id_local': c.idLocal,
        'json': jsonEncode(c.toJson()),
        'criada_em': DateTime.now().millisecondsSinceEpoch,
      }, conflictAlgorithm: ConflictAlgorithm.replace);
      await txn.rawDelete(
        'DELETE FROM $_tabela WHERE id_local NOT IN '
        '(SELECT id_local FROM $_tabela ORDER BY criada_em DESC LIMIT ?)',
        [FilaConferencias.limite],
      );
    });
  }

  @override
  Future<List<ConferenciaPendente>> pendentes() async {
    final db = await _db;
    if (db == null) return _memoria.pendentes();
    final linhas = await db.query(_tabela, orderBy: 'criada_em');
    return [
      for (final l in linhas)
        ConferenciaPendente.fromJson(
          jsonDecode(l['json']! as String) as Map<String, dynamic>,
        ),
    ];
  }

  @override
  Future<void> remover(String idLocal) async {
    final db = await _db;
    if (db == null) return _memoria.remover(idLocal);
    await db.delete(_tabela, where: 'id_local = ?', whereArgs: [idLocal]);
  }
}

final filaConferenciasProvider = Provider<FilaConferencias>(
  (_) => FilaConferenciasDisco(),
);
