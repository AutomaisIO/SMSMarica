import 'dart:convert';

import 'package:agente/features/viagem/domain/viagem.dart';
import 'package:agente/shared/armazenamento/chave_banco.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite_sqlcipher/sqflite.dart';

/// Marcações da viagem esperando rede para chegar à central. A ordem importa
/// (início antes das marcações, conclusão no fim): sai da mais antiga.
abstract interface class FilaViagem {
  /// Uma rota tem poucas dezenas de marcações; o limite só segura um tablet
  /// dias sem rede. Passou, sai a mais antiga.
  static const limite = 2000;

  Future<void> adicionar(List<EventoViagemPendente> eventos);

  /// Da mais antiga para a mais nova.
  Future<List<EventoViagemPendente>> pendentes();

  Future<void> remover(String idLocal);
}

/// Em memória: fallback quando o disco não abre, e base dos testes.
class FilaViagemMemoria implements FilaViagem {
  final List<EventoViagemPendente> _itens = [];

  @override
  Future<void> adicionar(List<EventoViagemPendente> eventos) async {
    for (final e in eventos) {
      _itens
        ..removeWhere((x) => x.idLocal == e.idLocal)
        ..add(e);
    }
    while (_itens.length > FilaViagem.limite) {
      _itens.removeAt(0);
    }
  }

  @override
  Future<List<EventoViagemPendente>> pendentes() async => List.of(_itens);

  @override
  Future<void> remover(String idLocal) async =>
      _itens.removeWhere((x) => x.idLocal == idLocal);
}

/// Em disco cifrado (SQLCipher): leva nome de paciente e posição, e precisa
/// sobreviver a app fechado e tablet reiniciado sem rede. Mesma receita da
/// fila de conferências.
class FilaViagemDisco implements FilaViagem {
  FilaViagemDisco({FlutterSecureStorage? secureStorage})
    : _secure = secureStorage ?? _padrao;

  static const _padrao = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );
  static const _chaveBanco = 'viagem_db_key';
  static const _arquivoBanco = 'viagem.db';
  static const _tabela = 'evento_pendente';

  final FlutterSecureStorage _secure;
  final FilaViagemMemoria _memoria = FilaViagemMemoria();
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
            seq        INTEGER PRIMARY KEY AUTOINCREMENT,
            id_local   TEXT NOT NULL UNIQUE,
            json       TEXT NOT NULL
          )
        '''),
      );
    } on Object catch (_) {
      return null;
    }
  }

  @override
  Future<void> adicionar(List<EventoViagemPendente> eventos) async {
    final db = await _db;
    if (db == null) return _memoria.adicionar(eventos);
    await db.transaction((txn) async {
      for (final e in eventos) {
        await txn.insert(_tabela, {
          'id_local': e.idLocal,
          'json': jsonEncode(e.toJson()),
        }, conflictAlgorithm: ConflictAlgorithm.ignore);
      }
      await txn.rawDelete(
        'DELETE FROM $_tabela WHERE seq NOT IN '
        '(SELECT seq FROM $_tabela ORDER BY seq DESC LIMIT ?)',
        [FilaViagem.limite],
      );
    });
  }

  @override
  Future<List<EventoViagemPendente>> pendentes() async {
    final db = await _db;
    if (db == null) return _memoria.pendentes();
    final linhas = await db.query(_tabela, orderBy: 'seq');
    return [
      for (final l in linhas)
        EventoViagemPendente.fromJson(
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

final filaViagemProvider = Provider<FilaViagem>((_) => FilaViagemDisco());
