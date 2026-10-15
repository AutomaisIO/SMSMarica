import 'dart:math';

import 'package:agente/features/auth/facial/domain/desafio_liveness.dart';
import 'package:flutter_test/flutter_test.dart';

/// Sorteio fixo: o lado do desafio fica previsível no teste.
class _Sorteio implements Random {
  _Sorteio({required this.esquerda});

  final bool esquerda;

  @override
  bool nextBool() => esquerda;

  @override
  double nextDouble() => 0;

  @override
  int nextInt(int max) => 0;
}

const _semRosto = MedidaRosto(quantidade: 0);
const _doisRostos = MedidaRosto(quantidade: 2);

MedidaRosto _frente({double olhos = 0.9, int id = 7}) => MedidaRosto(
  quantidade: 1,
  frontal: true,
  olhosAbertos: olhos,
  yaw: 0,
  trackingId: id,
);

MedidaRosto _virado(double yaw, {int id = 7}) =>
    MedidaRosto(quantidade: 1, olhosAbertos: 0.9, yaw: yaw, trackingId: id);

/// Rosto de frente que pisca: aberto → fechado → aberto.
void _piscar(DesafioLiveness d) {
  d
    ..avaliar(_frente())
    ..avaliar(_frente())
    ..avaliar(_frente(olhos: 0.1))
    ..avaliar(_frente());
}

void main() {
  test('piscar, virar para o lado pedido e voltar de frente aprova', () {
    final d = DesafioLiveness(sorteio: _Sorteio(esquerda: true));

    expect(d.avaliar(_frente()), EtapaLiveness.aguardandoPiscada);
    _piscar(d);
    expect(d.etapa, EtapaLiveness.aguardandoVirar);
    expect(d.lado, LadoVirar.esquerda);

    expect(d.avaliar(_virado(25)), EtapaLiveness.aguardandoFrente);
    expect(d.avaliar(_frente()), EtapaLiveness.aprovado);
    expect(d.aprovado, isTrue);
  });

  test('virar para o lado errado não avança', () {
    final d = DesafioLiveness(sorteio: _Sorteio(esquerda: false));
    _piscar(d);
    expect(d.lado, LadoVirar.direita);

    expect(d.avaliar(_virado(30)), EtapaLiveness.aguardandoVirar);
    expect(d.avaliar(_virado(-30)), EtapaLiveness.aguardandoFrente);
  });

  test('olho fechado sem ter visto aberto antes não conta como piscada', () {
    final d = DesafioLiveness(sorteio: _Sorteio(esquerda: true))
      ..avaliar(_frente(olhos: 0.1))
      ..avaliar(_frente(olhos: 0.1))
      ..avaliar(_frente());

    expect(d.etapa, EtapaLiveness.aguardandoPiscada);
  });

  test('sumir da câmera, dois rostos ou outra pessoa recomeçam', () {
    final d = DesafioLiveness(sorteio: _Sorteio(esquerda: true));
    _piscar(d);
    expect(d.avaliar(_semRosto), EtapaLiveness.procurandoRosto);

    _piscar(d);
    expect(d.avaliar(_doisRostos), EtapaLiveness.procurandoRosto);

    _piscar(d);
    expect(d.avaliar(_virado(25, id: 8)), EtapaLiveness.procurandoRosto);
  });

  test('etapa que passa do prazo recomeça e marca expirou', () {
    var agora = DateTime(2026, 10, 15, 10);
    final d = DesafioLiveness(
      sorteio: _Sorteio(esquerda: true),
      prazoEtapa: const Duration(seconds: 10),
      relogio: () => agora,
    )..avaliar(_frente());
    expect(d.etapa, EtapaLiveness.aguardandoPiscada);

    agora = agora.add(const Duration(seconds: 11));
    expect(d.avaliar(_frente()), EtapaLiveness.procurandoRosto);
    expect(d.expirou, isTrue);

    d.avaliar(_frente());
    expect(d.expirou, isFalse);
  });
}
