import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/viagem/domain/viagem.dart';

/// Uma rota de hoje do veículo do tablet, como o servidor manda em
/// `GET /rastreamento/dispositivos/rotas-do-dia` — já convertida para a
/// navegação. Junta o que a central cadastrou em Translados, Atendimentos,
/// Unidades de Atendimento e Tipos de tratamento.
class RotaDoTablet {
  const RotaDoTablet({
    required this.rotaId,
    required this.data,
    required this.status,
    required this.motoristaId,
    required this.motoristaNome,
    required this.translado,
    required this.andamento,
  });

  factory RotaDoTablet.fromJson(Map<String, dynamic> j, {String veiculo = ''}) {
    final data = DateTime.parse(j['data'] as String);
    final rotaId = j['rotaId'] as String;
    final status = j['status'] as String? ?? 'Planejada';
    final paradasJson = [
      for (final p in j['paradas'] as List<dynamic>? ?? const [])
        p as Map<String, dynamic>,
    ];
    final paradas = [for (final p in paradasJson) _parada(p, data)];
    return RotaDoTablet(
      rotaId: rotaId,
      data: data,
      status: status,
      motoristaId: j['motoristaId'] as String? ?? '',
      motoristaNome: j['motoristaNome'] as String? ?? '',
      translado: TransladoNavegacao(
        id: rotaId,
        veiculo: veiculo,
        paradas: paradas,
      ),
      andamento: _andamento(rotaId, status, paradasJson, paradas),
    );
  }

  final String rotaId;
  final DateTime data;

  /// `Planejada`, `EmAndamento` ou `Concluida` (canceladas não vêm).
  final String status;
  final String motoristaId;
  final String motoristaNome;
  final TransladoNavegacao translado;

  /// O que a central já recebeu desta viagem (paradas resolvidas e quem
  /// faltou), para o tablet retomar depois de reiniciar.
  final AndamentoViagem andamento;

  bool get emAndamento => status == 'EmAndamento';
  bool get concluida => status == 'Concluida';

  static ParadaNavegacao _parada(Map<String, dynamic> p, DateTime dia) {
    final tipo = switch (p['tipo']) {
      'Destino' => TipoParada.destino,
      _ => TipoParada.coleta,
    };
    return ParadaNavegacao(
      id: p['id'] as String,
      ordem: (p['ordem'] as num).toInt(),
      tipo: tipo,
      rotulo: p['rotulo'] as String? ?? '',
      endereco: p['endereco'] as String? ?? 'Endereço não informado',
      latitude: (p['latitude'] as num?)?.toDouble(),
      longitude: (p['longitude'] as num?)?.toDouble(),
      horarioPrevisto: _hora(p['horarioPrevisto'] as String?, dia),
      observacoes: p['observacoes'] as String?,
      passageiros: [
        for (final x in p['passageiros'] as List<dynamic>? ?? const [])
          _passageiro(x as Map<String, dynamic>),
      ],
    );
  }

  static PassageiroTranslado _passageiro(Map<String, dynamic> p) {
    final condicao = p['condicao'] as Map<String, dynamic>? ?? const {};
    return PassageiroTranslado(
      id: p['sessaoId'] as String,
      nome: p['nome'] as String? ?? 'Paciente',
      telefone: p['telefone'] as String?,
      confirmacao: confirmacao(p['confirmacao'] as String?),
      assento: assento(p['assento'] as Map<String, dynamic>?),
      observacao: observacao(
        condicao,
        tipoTratamento: p['tipoTratamento'] as String?,
        tempoMedioMinutos: (p['tempoMedioMinutos'] as num?)?.toInt(),
      ),
      acompanhantes: [
        for (final (i, a)
            in (p['acompanhantes'] as List<dynamic>? ?? const []).indexed)
          _acompanhante(a as Map<String, dynamic>, ordem: i + 1),
      ],
    );
  }

  static Acompanhante _acompanhante(
    Map<String, dynamic> a, {
    required int ordem,
  }) {
    final parentesco = rotuloParentesco(a['parentesco'] as String?);
    final nome = a['nome'] as String? ?? 'Acompanhante';
    return Acompanhante(
      nome: parentesco == null ? nome : '$nome ($parentesco)',
      confirmacao: confirmacao(a['confirmacao'] as String?),
      assento: assento(a['assento'] as Map<String, dynamic>?),
      id: a['id'] as String?,
      ordem: ordem,
    );
  }

  /// Paradas que o tablet já resolveu e quem faltou, pelo que a central
  /// recebeu (`concluida` da parada, `situacao` de cada pessoa).
  static AndamentoViagem _andamento(
    String rotaId,
    String status,
    List<Map<String, dynamic>> json,
    List<ParadaNavegacao> paradas,
  ) {
    final ausentes = <String>{};
    for (final (i, p) in json.indexed) {
      final quem = p['passageiros'] as List<dynamic>? ?? const [];
      for (final (k, x) in quem.indexed) {
        final passageiro = paradas[i].passageiros[k];
        final m = x as Map<String, dynamic>;
        if (m['situacao'] == 'Ausente') ausentes.add(passageiro.id);
        final acomp = m['acompanhantes'] as List<dynamic>? ?? const [];
        for (final (n, a) in acomp.indexed) {
          if ((a as Map<String, dynamic>)['situacao'] == 'Ausente') {
            ausentes.add(passageiro.idAcompanhante(n));
          }
        }
      }
    }
    return AndamentoViagem(
      rotaId: rotaId,
      concluidas: {
        for (final p in json)
          if (p['concluida'] == true) p['id'] as String,
      },
      ausentes: ausentes,
      iniciada: status != 'Planejada',
      encerrada: status == 'Concluida',
    );
  }

  /// `HH:mm:ss` do servidor no dia da rota; sem horário, null.
  static DateTime? _hora(String? hhmmss, DateTime dia) {
    if (hhmmss == null) return null;
    final partes = hhmmss.split(':');
    if (partes.length < 2) return null;
    final h = int.tryParse(partes[0]);
    final m = int.tryParse(partes[1]);
    if (h == null || m == null) return null;
    return DateTime(dia.year, dia.month, dia.day, h, m);
  }

  static StatusConfirmacao confirmacao(String? valor) => switch (valor) {
    'Confirmado' => StatusConfirmacao.confirmado,
    'Cancelado' => StatusConfirmacao.ausente,
    _ => StatusConfirmacao.pendente,
  };

  /// "Fileira 1, banco 2" (+ cadeirante quando o assento é adaptado).
  static String? assento(Map<String, dynamic>? a) {
    if (a == null) return null;
    final texto = 'Fileira ${a['fileira']}, banco ${a['numero']}';
    return a['tipo'] == 'Cadeirante' ? '$texto (cadeirante)' : texto;
  }

  static String? rotuloParentesco(String? p) => switch (p) {
    'Mae' => 'mãe',
    'Pai' => 'pai',
    'Filho' => 'filho(a)',
    'Conjuge' => 'cônjuge',
    'Irmao' => 'irmão(ã)',
    'OutroParente' => 'parente',
    'Cuidador' => 'cuidador(a)',
    _ => null,
  };

  /// O que o motorista precisa saber antes de chegar: a condição do paciente
  /// (cadastro do Atendimento) e quanto tempo o tratamento costuma levar.
  static String? observacao(
    Map<String, dynamic> c, {
    String? tipoTratamento,
    int? tempoMedioMinutos,
  }) {
    final itens = <String>[
      ?switch (c['mobilidade']) {
        'CadeiranteTransfereParaBanco' => 'Cadeirante — passa para o banco',
        'CadeiranteVeiculoAdaptado' => 'Cadeirante — só em veículo adaptado',
        'Maca' => 'Vai de maca',
        _ => null,
      },
      if (c['dificuldadeVeiculoAlto'] == true) 'Dificuldade com veículo alto',
      if (c['isolamento'] == true) 'Isolamento — transporte exclusivo',
      if (c['usaOxigenio'] == true) 'Usa oxigênio',
      if (c['necessitaAjuda'] == true)
        (c['ajudaDescricao'] as String?)?.trim().isNotEmpty ?? false
            ? 'Ajuda: ${(c['ajudaDescricao'] as String).trim()}'
            : 'Precisa de ajuda',
      if (tipoTratamento != null)
        tempoMedioMinutos == null
            ? tipoTratamento
            : '$tipoTratamento (cerca de ${_duracao(tempoMedioMinutos)})',
    ];
    return itens.isEmpty ? null : itens.join(' · ');
  }

  static String _duracao(int minutos) {
    if (minutos < 60) return '$minutos min';
    final h = minutos ~/ 60;
    final m = minutos % 60;
    return m == 0 ? '$h h' : '$h h ${m.toString().padLeft(2, '0')}';
  }
}
