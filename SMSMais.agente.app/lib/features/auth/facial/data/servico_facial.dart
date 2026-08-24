import 'dart:typed_data';

import 'package:agente/features/auth/facial/data/armazenamento_facial.dart';
import 'package:agente/features/auth/facial/data/detector_rosto.dart';
import 'package:agente/features/auth/facial/data/embedding_facial.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Resultado de uma verificação 1:1.
class ResultadoVerificacao {
  const ResultadoVerificacao({
    required this.aprovado,
    required this.score,
    required this.motivo,
  });

  final bool aprovado;
  final double score;
  final String motivo;
}

/// Orquestra captura → embedding → cadastro/match, usando o store cifrado.
///
/// Enquanto o backend (BE-1) não entrega a foto de referência, o cadastro pode
/// ser feito localmente (modo teste) capturando o rosto no próprio device.
class ServicoFacial {
  ServicoFacial({
    EmbeddingFacial? embedding,
    ArmazenamentoFacial? armazenamento,
    DetectorRosto? detector,
  })  : _embedding = embedding ?? EmbeddingFacial(),
        _armazenamento = armazenamento ?? ArmazenamentoFacial(),
        _detector = detector ?? DetectorRosto();

  /// Limiar de cosseno para aceitar como a mesma pessoa. MobileFaceNet:
  /// mesma pessoa costuma ficar > 0.7; pessoas diferentes < 0.5. 0.62 é um
  /// ponto de partida conservador (ajustável em campo).
  static const double limiar = 0.62;

  final EmbeddingFacial _embedding;
  final ArmazenamentoFacial _armazenamento;
  final DetectorRosto _detector;

  bool get matchDisponivel => _embedding.disponivel;

  Future<void> carregar() => _embedding.carregar();

  /// Indica se já existe template facial deste motorista neste tablet.
  Future<bool> estaCadastrado(String motoristaId) async {
    return await _armazenamento.obter(motoristaId) != null;
  }

  /// Gera o embedding de uma foto capturada (detecta o rosto no arquivo e
  /// recorta). Retorna `null` se o modelo indisponível ou nenhum rosto.
  Future<Float32List?> _embeddingDaFoto(String caminhoFoto) async {
    final rostos = await _detector.detectarDoArquivo(caminhoFoto);
    if (rostos.length != 1) return null;
    return _embedding.gerarEmbedding(caminhoFoto, rostos.first.boundingBox);
  }

  /// Cadastra (enrollment) o rosto de um motorista a partir de uma foto.
  Future<bool> cadastrar({
    required String motoristaId,
    required String cpf,
    required String nome,
    required String caminhoFoto,
    required String fotoHash,
  }) async {
    final vetor = await _embeddingDaFoto(caminhoFoto);
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

  /// Verifica 1:1 contra um motorista específico, ou 1:N (melhor match) quando
  /// [motoristaId] é nulo. Útil no login quando o device tem um único cadastro.
  Future<ResultadoVerificacao> verificar({
    required String caminhoFoto,
    String? motoristaId,
  }) async {
    if (!_embedding.disponivel) {
      return const ResultadoVerificacao(
        aprovado: false,
        score: 0,
        motivo: 'Modelo facial indisponível',
      );
    }

    final atual = await _embeddingDaFoto(caminhoFoto);
    if (atual == null) {
      return const ResultadoVerificacao(
        aprovado: false,
        score: 0,
        motivo: 'Nenhum rosto reconhecido na captura',
      );
    }

    final candidatos = motoristaId != null
        ? [await _armazenamento.obter(motoristaId)].whereType<MotoristaFacial>()
        : await _armazenamento.todos();

    if (candidatos.isEmpty) {
      return const ResultadoVerificacao(
        aprovado: false,
        score: 0,
        motivo: 'Nenhum motorista cadastrado neste tablet',
      );
    }

    var melhor = -1.0;
    for (final c in candidatos) {
      final s = EmbeddingFacial.similaridade(atual, c.embedding);
      if (s > melhor) melhor = s;
    }

    return ResultadoVerificacao(
      aprovado: melhor >= limiar,
      score: melhor,
      motivo: melhor >= limiar ? 'Identidade confirmada' : 'Rosto não confere',
    );
  }
}

final servicoFacialProvider = Provider<ServicoFacial>((ref) {
  final s = ServicoFacial();
  ref.onDispose(() {});
  return s;
});
