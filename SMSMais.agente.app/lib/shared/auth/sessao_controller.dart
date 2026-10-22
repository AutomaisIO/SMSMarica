import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Como o motorista entrou no tablet (ADR-0073).
enum MeioEntrada {
  /// Rosto comparado com a foto de referência.
  rosto,

  /// Código de uso único da central.
  codigoCentral,

  /// Acabou de cadastrar o rosto (código no WhatsApp + prova de vida).
  primeiroAcesso,
}

/// Motorista que está dirigindo agora. A sessão é **do tablet**: o servidor
/// só recebe o registro de cada conferência (o tablet fala pelo token do
/// veículo, sem JWT de motorista).
class Sessao {
  const Sessao({
    required this.motoristaId,
    required this.nome,
    required this.meio,
    required this.entrouEm,
  });

  factory Sessao.fromJson(Map<String, dynamic> j) => Sessao(
    motoristaId: j['motoristaId'] as String,
    nome: j['nome'] as String,
    meio: MeioEntrada.values.byName(j['meio'] as String),
    entrouEm: DateTime.parse(j['entrouEm'] as String),
  );

  final String motoristaId;
  final String nome;
  final MeioEntrada meio;
  final DateTime entrouEm;

  Map<String, dynamic> toJson() => {
    'motoristaId': motoristaId,
    'nome': nome,
    'meio': meio.name,
    'entrouEm': entrouEm.toUtc().toIso8601String(),
  };

  /// Vale até a virada do dia em Brasília (o tablet fica em GMT): no dia
  /// seguinte quem dirige pode ser outro, então confere de novo.
  bool valeEm(DateTime agora) =>
      diaEmBrasilia(entrouEm) == diaEmBrasilia(agora);

  /// Data (sem hora) em Brasília, UTC−3 fixo desde 2019 (sem horário de verão).
  static DateTime diaEmBrasilia(DateTime t) {
    final b = t.toUtc().subtract(const Duration(hours: 3));
    return DateTime.utc(b.year, b.month, b.day);
  }
}

/// Guarda a sessão no armazenamento cifrado: o app reinicia no quiosque
/// (atualização, travamento) sem pedir o rosto de novo no mesmo dia.
class SessaoController extends StateNotifier<Sessao?> {
  SessaoController(this._storage, {DateTime Function()? relogio})
    : _relogio = relogio ?? DateTime.now,
      super(null) {
    unawaited(carregar());
  }

  static const _chave = 'sessao_motorista';

  final FlutterSecureStorage _storage;
  final DateTime Function() _relogio;
  bool _carregada = false;

  /// Já leu o que estava salvo (antes disso, null não quer dizer "sem sessão").
  bool get carregada => _carregada;

  Future<void> carregar() async {
    try {
      final s = await _storage.read(key: _chave);
      if (s != null) {
        final sessao = Sessao.fromJson(jsonDecode(s) as Map<String, dynamic>);
        if (sessao.valeEm(_relogio())) {
          if (mounted && state == null) state = sessao;
        } else {
          await _storage.delete(key: _chave);
        }
      }
    } on Object catch (_) {
      // Sessão ilegível: o motorista confere de novo.
    } finally {
      _carregada = true;
    }
  }

  Future<void> entrar(Sessao sessao) async {
    state = sessao;
    try {
      await _storage.write(key: _chave, value: jsonEncode(sessao.toJson()));
    } on Object catch (_) {
      // Sem gravar, vale só até o app fechar.
    }
  }

  /// "Trocar motorista" ou sessão vencida.
  Future<void> sair() async {
    state = null;
    try {
      await _storage.delete(key: _chave);
    } on Object catch (_) {}
  }
}

final sessaoProvider = StateNotifierProvider<SessaoController, Sessao?>(
  (ref) => SessaoController(
    const FlutterSecureStorage(
      aOptions: AndroidOptions(encryptedSharedPreferences: true),
    ),
  ),
);
