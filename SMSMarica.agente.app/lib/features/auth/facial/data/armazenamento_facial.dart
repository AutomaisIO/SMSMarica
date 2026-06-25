import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite_sqlcipher/sqflite.dart';

/// Registro biométrico local de um motorista (template + metadados).
class MotoristaFacial {
  const MotoristaFacial({
    required this.motoristaId,
    required this.cpf,
    required this.nome,
    required this.embedding,
    required this.fotoHash,
    required this.modeloVersao,
    required this.atualizadoEm,
  });

  final String motoristaId;
  final String cpf;
  final String nome;
  final Float32List embedding;
  final String fotoHash;
  final String modeloVersao;
  final DateTime atualizadoEm;
}

/// Armazenamento local **cifrado** (SQLCipher) do template facial.
///
/// A chave do banco é gerada aleatoriamente uma única vez e selada no
/// `flutter_secure_storage` (Android Keystore, hardware-backed). O template
/// nunca sai do device — o servidor guarda só a foto de referência + auditoria.
class ArmazenamentoFacial {
  ArmazenamentoFacial({FlutterSecureStorage? secureStorage})
      : _secure = secureStorage ?? _padrao;

  static const _padrao = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );

  static const _chaveBanco = 'facial_db_key';
  static const _arquivoBanco = 'facial.db';

  final FlutterSecureStorage _secure;
  Database? _db;

  Future<Database> get _database async => _db ??= await _abrir();

  Future<Database> _abrir() async {
    final dir = await getApplicationDocumentsDirectory();
    final caminho = p.join(dir.path, _arquivoBanco);
    final senha = await _obterOuCriarChave();
    return openDatabase(
      caminho,
      password: senha,
      version: 1,
      onCreate: (db, _) async {
        await db.execute('''
          CREATE TABLE motorista_facial (
            motorista_id   TEXT PRIMARY KEY,
            cpf            TEXT NOT NULL,
            nome           TEXT NOT NULL,
            embedding      BLOB NOT NULL,
            foto_hash      TEXT NOT NULL,
            modelo_versao  TEXT NOT NULL,
            atualizado_em  TEXT NOT NULL
          )
        ''');
      },
    );
  }

  Future<String> _obterOuCriarChave() async {
    final existente = await _secure.read(key: _chaveBanco);
    if (existente != null) return existente;
    final rng = Random.secure();
    final bytes = Uint8List.fromList(
      List<int>.generate(32, (_) => rng.nextInt(256)),
    );
    final chave = base64UrlEncode(bytes);
    await _secure.write(key: _chaveBanco, value: chave);
    return chave;
  }

  Future<void> salvar(MotoristaFacial m) async {
    final db = await _database;
    await db.insert(
      'motorista_facial',
      {
        'motorista_id': m.motoristaId,
        'cpf': m.cpf,
        'nome': m.nome,
        'embedding': _floatsParaBytes(m.embedding),
        'foto_hash': m.fotoHash,
        'modelo_versao': m.modeloVersao,
        'atualizado_em': m.atualizadoEm.toIso8601String(),
      },
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<MotoristaFacial?> obter(String motoristaId) async {
    final db = await _database;
    final linhas = await db.query(
      'motorista_facial',
      where: 'motorista_id = ?',
      whereArgs: [motoristaId],
      limit: 1,
    );
    if (linhas.isEmpty) return null;
    return _mapear(linhas.first);
  }

  Future<List<MotoristaFacial>> todos() async {
    final db = await _database;
    final linhas = await db.query('motorista_facial');
    return linhas.map(_mapear).toList();
  }

  Future<void> apagar(String motoristaId) async {
    final db = await _database;
    await db.delete(
      'motorista_facial',
      where: 'motorista_id = ?',
      whereArgs: [motoristaId],
    );
  }

  MotoristaFacial _mapear(Map<String, Object?> linha) => MotoristaFacial(
        motoristaId: linha['motorista_id']! as String,
        cpf: linha['cpf']! as String,
        nome: linha['nome']! as String,
        embedding: _bytesParaFloats(linha['embedding']! as Uint8List),
        fotoHash: linha['foto_hash']! as String,
        modeloVersao: linha['modelo_versao']! as String,
        atualizadoEm: DateTime.parse(linha['atualizado_em']! as String),
      );

  Uint8List _floatsParaBytes(Float32List floats) =>
      floats.buffer.asUint8List();

  Float32List _bytesParaFloats(Uint8List bytes) =>
      Float32List.view(Uint8List.fromList(bytes).buffer);
}
