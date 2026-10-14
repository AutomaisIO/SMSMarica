import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:flutter_test/flutter_test.dart';

const _maria = PassageiroTranslado(
  id: 'pac-1',
  nome: 'Maria',
  confirmacao: StatusConfirmacao.confirmado,
  acompanhante: Acompanhante(
    nome: 'José',
    confirmacao: StatusConfirmacao.confirmado,
  ),
);
const _joao = PassageiroTranslado(
  id: 'pac-2',
  nome: 'João',
  confirmacao: StatusConfirmacao.confirmado,
);

ParadaNavegacao _parada(
  String id,
  TipoParada tipo,
  List<PassageiroTranslado> quem,
) => ParadaNavegacao(
  id: id,
  ordem: 0,
  tipo: tipo,
  rotulo: id,
  endereco: '',
  latitude: 0,
  longitude: 0,
  horarioPrevisto: DateTime(2026, 10, 14),
  passageiros: quem,
);

/// Coleta da Maria (+ José), coleta do João, destino de todos.
final _translado = TransladoNavegacao(
  id: 't1',
  veiculo: 'Van',
  paradas: [
    _parada('p1', TipoParada.coleta, const [_maria]),
    _parada('p2', TipoParada.coleta, const [_joao]),
    _parada('p3', TipoParada.destino, const [_maria, _joao]),
  ],
);

ParadaNavegacao _destino(Set<String> concluidas, Set<String> ausentes) =>
    _translado
        .paradasComAndamento(concluidas: concluidas, ausentes: ausentes)
        .last;

void main() {
  test('sem ausentes, o destino recebe todos com acompanhante', () {
    final destino = _destino({'p1', 'p2'}, {});

    expect(destino.passageiros.map((p) => p.nome), ['Maria', 'João']);
    expect(destino.passageiros.first.temAcompanhante, isTrue);
    expect(destino.concluida, isFalse);
  });

  test('acompanhante ausente: paciente desce sem ele', () {
    final destino = _destino({'p1', 'p2'}, {_maria.idAcompanhante});

    expect(destino.passageiros.map((p) => p.nome), ['Maria', 'João']);
    expect(destino.passageiros.first.temAcompanhante, isFalse);
  });

  test('paciente ausente sai do destino com o acompanhante', () {
    final destino = _destino({'p1', 'p2'}, {_maria.id});

    expect(destino.passageiros.map((p) => p.nome), ['João']);
  });

  test('destino sem ninguém fica dispensado e o translado acaba', () {
    final paradas = _translado.paradasComAndamento(
      concluidas: {'p1', 'p2'},
      ausentes: {_maria.id, _joao.id},
    );

    expect(paradas.last.passageiros, isEmpty);
    expect(paradas.every((p) => p.concluida), isTrue);
  });

  test('coletas seguem a ordem: a próxima é a primeira pendente', () {
    final paradas = _translado.paradasComAndamento(
      concluidas: {'p1'},
      ausentes: {_maria.id},
    );

    expect(paradas.firstWhere((p) => !p.concluida).id, 'p2');
  });
}
