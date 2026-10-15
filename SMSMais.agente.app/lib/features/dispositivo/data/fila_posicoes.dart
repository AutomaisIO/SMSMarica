import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:agente/shared/armazenamento/chave_banco.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite_sqlcipher/sqflite.dart';

/// Ponto aguardando envio, com a posição dele na fila.
typedef PontoPendente = ({int id, PontoDispositivo ponto});

/// Posições ainda não aceitas pelo servidor, da mais antiga à mais nova.
abstract interface class FilaPosicoes {
  /// ~14 h de deslocamento ativo (1 ponto a cada 5 s).
  static const int limitePadrao = 10000;

  /// Acrescenta no fim; passando do limite, descarta as mais antigas.
  Future<void> adicionar(PontoDispositivo ponto);

  /// Até [maximo] pontos, do mais antigo ao mais novo.
  Future<List<PontoPendente>> lote(int maximo);

  /// Tira da fila tudo até o [id], inclusive (o lote foi aceito).
  Future<void> removerAte(int id);

  Future<void> limpar();
}

/// Fila só em memória: some se o app fechar. Reserva da [FilaPosicoesDisco]
/// e base dos testes.
class FilaPosicoesMemoria implements FilaPosicoes {
  FilaPosicoesMemoria({this.limite = FilaPosicoes.limitePadrao});

  final int limite;
  final List<PontoPendente> _itens = <PontoPendente>[];
  int _proximoId = 1;

  @override
  Future<void> adicionar(PontoDispositivo ponto) async {
    _itens.add((id: _proximoId++, ponto: ponto));
    if (_itens.length > limite) _itens.removeRange(0, _itens.length - limite);
  }

  @override
  Future<List<PontoPendente>> lote(int maximo) async =>
      _itens.take(maximo).toList();

  @override
  Future<void> removerAte(int id) async =>
      _itens.removeWhere((item) => item.id <= id);

  @override
  Future<void> limpar() async => _itens.clear();
}

/// Fila em disco (SQLCipher): sobrevive a app fechado e tablet reiniciado
/// sem rede. Mesma receita do `ArmazenamentoFacial` para a chave. Se o banco
/// não abrir, segue em memória em vez de parar o envio ao Mapa da frota.
class FilaPosicoesDisco implements FilaPosicoes {
  FilaPosicoesDisco({
    this.limite = FilaPosicoes.limitePadrao,
    FlutterSecureStorage? secureStorage,
  }) : _secure = secureStorage ?? _padrao;

  static const _padrao = FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
  );

  static const _chaveBanco = 'posicoes_db_key';
  static const _arquivoBanco = 'posicoes.db';
  static const _tabela = 'ponto_pendente';

  final int limite;
  final FlutterSecureStorage _secure;
  late final FilaPosicoesMemoria _memoria = FilaPosicoesMemoria(
    limite: limite,
  );
  Future<Database?>? _abertura;

  /// Abre uma vez só, mesmo com chamadas simultâneas; `null` = sem disco.
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
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            latitude        REAL NOT NULL,
            longitude       REAL NOT NULL,
            velocidade_kmh  REAL,
            rumo            REAL,
            precisao_m      REAL,
            capturado_em    TEXT NOT NULL
          )
        '''),
      );
    } on Object catch (_) {
      return null;
    }
  }

  @override
  Future<void> adicionar(PontoDispositivo ponto) async {
    final db = await _db;
    if (db == null) return _memoria.adicionar(ponto);
    await db.transaction((txn) async {
      final id = await txn.insert(_tabela, _paraLinha(ponto));
      // Ids crescem 1 a 1: fica só o último [limite] de pontos.
      await txn.delete(_tabela, where: 'id <= ?', whereArgs: [id - limite]);
    });
  }

  @override
  Future<List<PontoPendente>> lote(int maximo) async {
    final db = await _db;
    if (db == null) return _memoria.lote(maximo);
    final linhas = await db.query(_tabela, orderBy: 'id', limit: maximo);
    return linhas.map(_deLinha).toList();
  }

  @override
  Future<void> removerAte(int id) async {
    final db = await _db;
    if (db == null) return _memoria.removerAte(id);
    await db.delete(_tabela, where: 'id <= ?', whereArgs: [id]);
  }

  @override
  Future<void> limpar() async {
    final db = await _db;
    if (db == null) return _memoria.limpar();
    await db.delete(_tabela);
  }

  static Map<String, Object?> _paraLinha(PontoDispositivo ponto) =>
      <String, Object?>{
        'latitude': ponto.latitude,
        'longitude': ponto.longitude,
        'velocidade_kmh': ponto.velocidadeKmh,
        'rumo': ponto.rumo,
        'precisao_m': ponto.precisaoM,
        'capturado_em': ponto.capturadoEm.toUtc().toIso8601String(),
      };

  static PontoPendente _deLinha(Map<String, Object?> linha) => (
    id: linha['id']! as int,
    ponto: PontoDispositivo(
      latitude: (linha['latitude']! as num).toDouble(),
      longitude: (linha['longitude']! as num).toDouble(),
      velocidadeKmh: (linha['velocidade_kmh'] as num?)?.toDouble(),
      rumo: (linha['rumo'] as num?)?.toDouble(),
      precisaoM: (linha['precisao_m'] as num?)?.toDouble(),
      capturadoEm: DateTime.parse(linha['capturado_em']! as String),
    ),
  );
}

final filaPosicoesProvider = Provider<FilaPosicoes>(
  (_) => FilaPosicoesDisco(),
);
