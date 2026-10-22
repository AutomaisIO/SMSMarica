import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:agente/features/auth/data/fila_conferencias.dart';
import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/features/auth/facial/data/servico_facial.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/imagem/jpeg_reduzido.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Quem pode entrar hoje neste tablet.
class EscalaDoDia {
  const EscalaDoDia({required this.motoristas, required this.atualizada});

  final List<MotoristaEscalado> motoristas;

  /// false = sem rede: veio a cópia guardada hoje (ou nada).
  final bool atualizada;

  bool get algumComRosto => motoristas.any((m) => m.temReferencia);
  bool get algumSemRosto => motoristas.any((m) => !m.temReferencia);

  MotoristaEscalado? porId(String? id) {
    for (final m in motoristas) {
      if (m.motoristaId == id) return m;
    }
    return null;
  }
}

/// Cópia da escala no tablet, para entrar pelo rosto sem rede. Só vale no
/// dia (em Brasília) em que foi baixada.
class EscalaLocal {
  EscalaLocal(this._storage, {DateTime Function()? relogio})
    : _relogio = relogio ?? DateTime.now;

  static const _chave = 'escala_do_dia';

  final FlutterSecureStorage _storage;
  final DateTime Function() _relogio;

  Future<void> salvar(List<MotoristaEscalado> lista) => _storage.write(
    key: _chave,
    value: jsonEncode({
      'dia': Sessao.diaEmBrasilia(_relogio()).toIso8601String(),
      'motoristas': [for (final m in lista) m.toJson()],
    }),
  );

  Future<List<MotoristaEscalado>?> doDia() async {
    try {
      final s = await _storage.read(key: _chave);
      if (s == null) return null;
      final j = jsonDecode(s) as Map<String, dynamic>;
      final hoje = Sessao.diaEmBrasilia(_relogio()).toIso8601String();
      if (j['dia'] != hoje) return null;
      return [
        for (final m in j['motoristas'] as List<dynamic>)
          MotoristaEscalado.fromJson(m as Map<String, dynamic>),
      ];
    } on Object catch (_) {
      return null;
    }
  }
}

/// A foto não tinha um rosto só (ou o modelo não carregou).
class SemRostoNaFoto implements Exception {
  const SemRostoNaFoto();
}

/// Posição do GPS no momento da conferência (opcional).
typedef Posicao = ({double? latitude, double? longitude, double? precisaoM});

/// Entrada do motorista no tablet (ADR-0073): baixa a escala e as fotos de
/// referência, confere o rosto contra os escalados, faz o primeiro acesso e
/// a entrada pelo código da central. Cada conferência vai para a fila.
class EntradaMotoristaService {
  EntradaMotoristaService({
    required MotoristaRepository repo,
    required ReconhecedorFacial facial,
    required EscalaLocal escala,
    required FilaConferencias fila,
  }) : _repo = repo,
       _facial = facial,
       _escala = escala,
       _fila = fila;

  final MotoristaRepository _repo;
  final ReconhecedorFacial _facial;
  final EscalaLocal _escala;
  final FilaConferencias _fila;

  /// Lado maior das fotos que saem do tablet: a de referência e a selfie.
  static const larguraReferencia = 640;
  static const larguraSelfie = 320;

  /// Pergunta ao servidor; sem rede, usa a cópia do dia. 401 sobe como
  /// [TokenDispositivoInvalido] (tablet desvinculado).
  Future<EscalaDoDia> carregarEscala(String token) async {
    List<MotoristaEscalado> lista;
    try {
      lista = await _repo.escaladosDoDia(token);
    } on TokenDispositivoInvalido {
      rethrow;
    } on Object catch (_) {
      return EscalaDoDia(
        motoristas: await _escala.doDia() ?? const [],
        atualizada: false,
      );
    }
    try {
      await _escala.salvar(lista);
    } on Object catch (_) {}
    await _sincronizarReferencias(token, lista);
    return EscalaDoDia(motoristas: lista, atualizada: true);
  }

  /// A cópia de hoje, sem ir ao servidor (conferência antes do translado).
  Future<EscalaDoDia> escalaGuardada() async => EscalaDoDia(
    motoristas: await _escala.doDia() ?? const [],
    atualizada: false,
  );

  /// Baixa a foto de quem mudou (hash diferente) e gera o vetor aqui; tira
  /// do tablet quem não está mais escalado (só o necessário fica no aparelho).
  Future<void> _sincronizarReferencias(
    String token,
    List<MotoristaEscalado> lista,
  ) async {
    await _facial.carregar();
    if (!_facial.matchDisponivel) return;
    final escalados = {for (final m in lista) m.motoristaId};
    for (final id in await _facial.cadastrados()) {
      if (!escalados.contains(id)) await _facial.esquecer(id);
    }
    for (final m in lista.where((m) => m.temReferencia)) {
      try {
        final local = await _facial.registro(m.motoristaId);
        if (local != null &&
            local.fotoHash == m.fotoHash &&
            local.modeloVersao == _facial.versaoModelo) {
          continue;
        }
        final ref = await _repo.referencia(token, m.motoristaId);
        await _comArquivo(base64Decode(ref.fotoBase64), (caminho) async {
          await _facial.cadastrar(
            motoristaId: m.motoristaId,
            cpf: '',
            nome: m.nome,
            caminhoFoto: caminho,
            fotoHash: ref.fotoHash,
          );
        });
      } on TokenDispositivoInvalido {
        rethrow;
      } on Object catch (_) {
        // Uma foto que não baixou não impede as outras; tenta na próxima.
      }
    }
  }

  /// Confere a foto contra os escalados com rosto (ou só [motoristaId], no
  /// início do translado) e põe a conferência na fila, com a selfie.
  Future<ResultadoVerificacao> conferirRosto({
    required EscalaDoDia escala,
    required String caminhoFoto,
    required MomentoConferencia momento,
    String? motoristaId,
    Posicao? posicao,
  }) async {
    await _facial.carregar();
    final candidatos = motoristaId != null
        ? [motoristaId]
        : [
            for (final m in escala.motoristas)
              if (m.temReferencia) m.motoristaId,
          ];
    final r = await _facial.verificar(
      caminhoFoto: caminhoFoto,
      candidatos: candidatos,
    );
    String? selfie;
    try {
      selfie = await jpegReduzidoBase64(caminhoFoto, largura: larguraSelfie);
    } on Object catch (_) {}
    await _fila.adicionar(
      ConferenciaPendente(
        idLocal: novoIdConferencia(),
        motoristaId: r.motoristaId,
        momento: momento,
        aprovada: r.aprovado,
        nota: r.motoristaId == null ? null : r.score,
        limiar: ServicoFacial.limiar,
        provaDeVida: true,
        modeloVersao: _facial.versaoModelo,
        latitude: posicao?.latitude,
        longitude: posicao?.longitude,
        precisaoM: posicao?.precisaoM,
        rotaId: escala.porId(r.motoristaId)?.rotaId,
        capturadaEm: DateTime.now().toUtc(),
        selfieBase64: selfie,
      ),
    );
    return r;
  }

  Future<CodigoEmitido> pedirCodigo(String token, String cpf) =>
      _repo.solicitarCodigo(token, cpf);

  Future<PrimeiroAcessoLiberado> confirmarCodigo(
    String token,
    String cpf,
    String codigo,
  ) => _repo.confirmarCodigo(token, cpf, codigo);

  /// Reduz a foto, confere que tem um rosto, gera o vetor DESSA MESMA foto
  /// (a que os outros tablets vão baixar) e só então manda ao servidor. Se
  /// o servidor recusar, o vetor sai do tablet.
  Future<void> concluirPrimeiroAcesso(
    String token,
    PrimeiroAcessoLiberado liberado,
    String caminhoFoto, {
    Posicao? posicao,
  }) async {
    await _facial.carregar();
    final reduzida = await jpegReduzido(
      caminhoFoto,
      largura: larguraReferencia,
      qualidade: 85,
    );
    if (reduzida == null) throw const SemRostoNaFoto();

    await _comArquivo(reduzida, (caminho) async {
      final ok = await _facial.cadastrar(
        motoristaId: liberado.motoristaId,
        cpf: '',
        nome: liberado.nome,
        caminhoFoto: caminho,
        fotoHash: 'enviando',
      );
      if (!ok) throw const SemRostoNaFoto();
    });

    try {
      final hash = await _repo.cadastrarRosto(
        token,
        ticket: liberado.ticket,
        fotoBase64: base64Encode(reduzida),
        termoVersao: liberado.termoVersao,
        latitude: posicao?.latitude,
        longitude: posicao?.longitude,
      );
      // Grava o hash do servidor: a próxima escala não baixa a foto de novo.
      await _facial.trocarHash(liberado.motoristaId, hash);
    } on Object {
      await _facial.esquecer(liberado.motoristaId);
      rethrow;
    }
  }

  Future<({String motoristaId, String nome})> entrarComCodigoDaCentral(
    String token,
    String codigo, {
    Posicao? posicao,
  }) => _repo.usarLiberacao(
    token,
    idLocal: novoIdConferencia(),
    codigo: codigo,
    latitude: posicao?.latitude,
    longitude: posicao?.longitude,
  );

  static Future<void> _comArquivo(
    List<int> bytes,
    Future<void> Function(String caminho) uso,
  ) async {
    final dir = await Directory.systemTemp.createTemp('rosto');
    try {
      final f = File('${dir.path}/rosto.jpg');
      await f.writeAsBytes(bytes, flush: true);
      await uso(f.path);
    } finally {
      try {
        await dir.delete(recursive: true);
      } on Object catch (_) {}
    }
  }
}

/// Manda as conferências da fila ao servidor: na hora e a cada
/// [intervalo]. Recusa (4xx) sai da fila para não travar as outras; sem
/// rede, para e tenta no próximo ciclo.
class ConferenciasController extends StateNotifier<int> {
  ConferenciasController(this._ref, this._fila, this._repo) : super(0) {
    _timer = Timer.periodic(intervalo, (_) => unawaited(enviarPendentes()));
    unawaited(enviarPendentes());
  }

  static const intervalo = Duration(seconds: 30);

  final Ref _ref;
  final FilaConferencias _fila;
  final MotoristaRepository _repo;
  Timer? _timer;
  bool _enviando = false;

  Future<void> enviarPendentes() async {
    if (_enviando) return;
    _enviando = true;
    try {
      final token = _ref.read(dispositivoProvider).token;
      if (token == null) return;
      for (final c in await _fila.pendentes()) {
        try {
          await _repo.registrarConferencia(token, c);
          await _fila.remover(c.idLocal);
        } on RecusaDaCentral catch (_) {
          await _fila.remover(c.idLocal);
        } on Object catch (_) {
          break;
        }
      }
    } on Object catch (_) {
    } finally {
      _enviando = false;
      try {
        final n = (await _fila.pendentes()).length;
        if (mounted) state = n;
      } on Object catch (_) {}
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

final reconhecedorFacialProvider = Provider<ReconhecedorFacial>(
  (ref) => ref.watch(servicoFacialProvider),
);

final entradaMotoristaServiceProvider = Provider<EntradaMotoristaService>(
  (ref) => EntradaMotoristaService(
    repo: ref.watch(motoristaRepositoryProvider),
    facial: ref.watch(reconhecedorFacialProvider),
    escala: EscalaLocal(
      const FlutterSecureStorage(
        aOptions: AndroidOptions(encryptedSharedPreferences: true),
      ),
    ),
    fila: ref.watch(filaConferenciasProvider),
  ),
);

/// Criado na raiz do app: a fila anda mesmo sem abrir a tela de entrada.
final conferenciasProvider = StateNotifierProvider<ConferenciasController, int>(
  (ref) => ConferenciasController(
    ref,
    ref.watch(filaConferenciasProvider),
    ref.watch(motoristaRepositoryProvider),
  ),
);
