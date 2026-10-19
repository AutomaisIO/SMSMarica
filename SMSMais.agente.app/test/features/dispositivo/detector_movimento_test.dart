import 'package:agente/features/deslocamento/application/deslocamento_controller.dart';
import 'package:agente/features/dispositivo/application/envio_posicoes_service.dart';
import 'package:flutter_test/flutter_test.dart';

final _t0 = DateTime(2026, 10, 15, 8);
DateTime _mais(int segundos) => _t0.add(Duration(seconds: segundos));

void main() {
  group('DetectorMovimento', () {
    test('parado desde o início: não está andando', () {
      final d = DetectorMovimento();

      expect(d.emMovimento(0, _t0), isFalse);
      expect(d.emMovimento(3, _mais(5)), isFalse);
    });

    test('acima de 5 km/h está andando', () {
      final d = DetectorMovimento();

      expect(d.emMovimento(30, _t0), isTrue);
    });

    test('parada curta (semáforo) continua contando como andando', () {
      final d = DetectorMovimento()..emMovimento(40, _t0);

      expect(d.emMovimento(0, _mais(20)), isTrue);
    });

    test('parado por mais de 30 s volta para o envio lento', () {
      final d = DetectorMovimento()..emMovimento(40, _t0);

      expect(d.emMovimento(0, _mais(31)), isFalse);
    });
  });

  group('DeslocamentoController.iniciarSeParado', () {
    test('liga o contador quando está parado', () {
      final c = DeslocamentoController()..iniciarSeParado();

      expect(c.state.ativo, isTrue);
      expect(c.state.inicio, isNotNull);
    });

    test('não zera um translado em andamento', () {
      final c = DeslocamentoController()..iniciar();
      final inicio = c.state.inicio;

      c.iniciarSeParado();

      expect(c.state.inicio, same(inicio));
    });
  });
}
