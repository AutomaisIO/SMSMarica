import 'dart:typed_data';

import 'package:agente/features/auth/facial/data/armazenamento_facial.dart';
import 'package:agente/features/auth/facial/data/detector_rosto.dart';
import 'package:agente/features/auth/facial/data/embedding_facial.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';

/// Resultado de uma verificação (1:1, ou o melhor de vários candidatos).
class ResultadoVerificacao {
  const ResultadoVerificacao({
    required this.aprovado,
    required this.score,
    required this.motivo,
    this.motoristaId,
  });

  final bool aprovado;
  final double score;
  final String motivo;

  /// Candidato mais parecido (null quando não havia com quem comparar). Só
  /// é "quem entrou" se [aprovado].
  final String? motoristaId;
}

/// O que o login do motorista precisa do reconhecimento (permite trocar por
/// um falso nos testes, sem câmera nem ML Kit).
abstract interface class ReconhecedorFacial {
  bool get matchDisponivel;
  String get versaoModelo;
  Future<void> carregar();
  Future<MotoristaFacial?> registro(String motoristaId);
  Future<List<String>> cadastrados();
  Future<void> esquecer(String motoristaId);
  Future<void> trocarHash(String motoristaId, String fotoHash);
  Future<bool> cadastrar({
    required String motoristaId,
    required String cpf,
    required String nome,
    required String caminhoFoto,
    required String fotoHash,
  });
  Future<ResultadoVerificacao> verificar({
    required String caminhoFoto,
    String? motoristaId,
    Iterable<String>? candidatos,
  });
}

/// Orquestra captura → embedding → cadastro/match, usando o store cifrado.
///
/// O vetor de cada motorista vem da foto de referência baixada do servidor
/// (ADR-0073); a tela de teste ainda cadastra localmente para calibrar.
class ServicoFacial implements ReconhecedorFacial {
  ServicoFacial({
    EmbeddingFacial? embedding,
    ArmazenamentoFacial? armazenamento,
    DetectorRosto? detector,
  }) : _embedding = embedding ?? EmbeddingFacial(),
       _armazenamento = armazenamento ?? ArmazenamentoFacial(),
       _detector = detector ?? DetectorRosto();

  /// Limiar de cosseno para aceitar como a mesma pessoa. MobileFaceNet:
  /// mesma pessoa costuma ficar > 0.7; pessoas diferentes < 0.5. 0.62 é um
  /// ponto de partida conservador (ajustável em campo).
  static const double limiar = 0.62;

  final EmbeddingFacial _embedding;
  final ArmazenamentoFacial _armazenamento;
  final DetectorRosto _detector;

  @override
  bool get matchDisponivel => _embedding.disponivel;

  @override
  String get versaoModelo => _embedding.versao;

  @override
  Future<void> carregar() => _embedding.carregar();

  /// Indica se já existe template facial deste motorista neste tablet.
  Future<bool> estaCadastrado(String motoristaId) async {
    return await _armazenamento.obter(motoristaId) != null;
  }

  @override
  Future<MotoristaFacial?> registro(String motoristaId) =>
      _armazenamento.obter(motoristaId);

  @override
  Future<List<String>> cadastrados() async => [
    for (final m in await _armazenamento.todos()) m.motoristaId,
  ];

  @override
  Future<void> esquecer(String motoristaId) =>
      _armazenamento.apagar(motoristaId);

  @override
  Future<void> trocarHash(String motoristaId, String fotoHash) async {
    final m = await _armazenamento.obter(motoristaId);
    if (m == null) return;
    await _armazenamento.salvar(
      MotoristaFacial(
        motoristaId: m.motoristaId,
        cpf: m.cpf,
        nome: m.nome,
        embedding: m.embedding,
        fotoHash: fotoHash,
        modeloVersao: m.modeloVersao,
        atualizadoEm: DateTime.now(),
      ),
    );
  }

  /// Gera o embedding de uma foto capturada (detecta o rosto no arquivo e
  /// recorta). Retorna `null` se o modelo indisponível ou nenhum rosto.
  Future<Float32List?> _embeddingDaFoto(
    String caminhoFoto, {
    String rotulo = 'rosto',
  }) async {
    final rostos = await _detector.detectarDoArquivo(caminhoFoto);
    if (rostos.length != 1) return null;
    final rosto = rostos.first;
    return _embedding.gerarEmbedding(
      caminhoFoto,
      rosto.boundingBox,
      pontos: _pontos(rosto),
      rotulo: rotulo,
    );
  }

  /// Olhos, nariz e cantos da boca na ordem do gabarito; null se o ML Kit
  /// não devolveu algum (aí o embedding usa só a caixa do rosto).
  static List<(double, double)>? _pontos(Face rosto) {
    (double, double)? p(FaceLandmarkType t) {
      final pos = rosto.landmarks[t]?.position;
      return pos == null ? null : (pos.x.toDouble(), pos.y.toDouble());
    }

    final olhoA = p(FaceLandmarkType.leftEye);
    final olhoB = p(FaceLandmarkType.rightEye);
    final nariz = p(FaceLandmarkType.noseBase);
    final bocaA = p(FaceLandmarkType.leftMouth);
    final bocaB = p(FaceLandmarkType.rightMouth);
    if (olhoA == null ||
        olhoB == null ||
        nariz == null ||
        bocaA == null ||
        bocaB == null) {
      return null;
    }
    return ordenarPontos(
      olhoA: olhoA,
      olhoB: olhoB,
      nariz: nariz,
      bocaA: bocaA,
      bocaB: bocaB,
    );
  }

  /// Cadastra (enrollment) o rosto de um motorista a partir de uma foto.
  @override
  Future<bool> cadastrar({
    required String motoristaId,
    required String cpf,
    required String nome,
    required String caminhoFoto,
    required String fotoHash,
  }) async {
    final vetor = await _embeddingDaFoto(caminhoFoto, rotulo: 'cadastro');
    if (vetor == null) return false;
    await _armazenamento.salvar(
      MotoristaFacial(
        motoristaId: motoristaId,
        cpf: cpf,
        nome: nome,
        embedding: vetor,
        fotoHash: fotoHash,
        modeloVersao: _embedding.versao,
        atualizadoEm: DateTime.now(),
      ),
    );
    return true;
  }

  /// Verifica 1:1 contra [motoristaId], contra os [candidatos] (os escalados
  /// do dia) ou, sem nenhum dos dois, contra todos os cadastrados no tablet.
  @override
  Future<ResultadoVerificacao> verificar({
    required String caminhoFoto,
    String? motoristaId,
    Iterable<String>? candidatos,
  }) async {
    if (!_embedding.disponivel) {
      return const ResultadoVerificacao(
        aprovado: false,
        score: 0,
        motivo: 'Modelo facial indisponível',
      );
    }

    final atual = await _embeddingDaFoto(caminhoFoto, rotulo: 'conferencia');
    if (atual == null) {
      return const ResultadoVerificacao(
        aprovado: false,
        score: 0,
        motivo: 'Nenhum rosto reconhecido na captura',
      );
    }

    final ids = motoristaId != null ? [motoristaId] : candidatos?.toList();
    final registros = ids != null
        ? [
            for (final id in ids) await _armazenamento.obter(id),
          ].whereType<MotoristaFacial>()
        : await _armazenamento.todos();

    if (registros.isEmpty) {
      return const ResultadoVerificacao(
        aprovado: false,
        score: 0,
        motivo: 'Nenhum motorista cadastrado neste tablet',
      );
    }

    var melhor = -1.0;
    String? quem;
    for (final c in registros) {
      final s = EmbeddingFacial.similaridade(atual, c.embedding);
      if (s > melhor) {
        melhor = s;
        quem = c.motoristaId;
      }
    }

    return ResultadoVerificacao(
      aprovado: melhor >= limiar,
      score: melhor,
      motivo: melhor >= limiar ? 'Identidade confirmada' : 'Rosto não confere',
      motoristaId: quem,
    );
  }

  void dispose() {
    _embedding.dispose();
    _detector.dispose();
  }
}

final servicoFacialProvider = Provider<ServicoFacial>((ref) {
  final s = ServicoFacial();
  ref.onDispose(s.dispose);
  return s;
});
