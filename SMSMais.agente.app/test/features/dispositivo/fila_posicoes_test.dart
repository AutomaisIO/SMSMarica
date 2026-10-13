import 'package:agente/features/dispositivo/data/fila_posicoes.dart';
import 'package:agente/features/dispositivo/domain/veiculo_vinculado.dart';
import 'package:flutter_test/flutter_test.dart';

/// Ponto capturado [segundo] segundos depois do meio-dia (UTC).
PontoDispositivo _ponto(int segundo) => PontoDispositivo(
  latitude: -22.91,
  longitude: -42.82,
  capturadoEm: DateTime.utc(2026, 10, 13, 12, 0, segundo),
);

Future<void> _encher(FilaPosicoes fila, int quantidade) async {
  for (var i = 0; i < quantidade; i++) {
    await fila.adicionar(_ponto(i));
  }
}

List<int> _segundos(List<PontoPendente> lote) =>
    lote.map((item) => item.ponto.capturadoEm.second).toList();

void main() {
  group('FilaPosicoesMemoria', () {
    test('entrega do mais antigo ao mais novo, no tamanho pedido', () async {
      final fila = FilaPosicoesMemoria();
      await _encher(fila, 5);

      expect(_segundos(await fila.lote(3)), [0, 1, 2]);
    });

    test('removerAte tira só o lote aceito', () async {
      final fila = FilaPosicoesMemoria();
      await _encher(fila, 5);

      final lote = await fila.lote(3);
      await fila.removerAte(lote.last.id);

      expect(_segundos(await fila.lote(10)), [3, 4]);
    });

    test('ponto novo durante o envio não sai com o lote anterior', () async {
      final fila = FilaPosicoesMemoria();
      await _encher(fila, 2);

      final lote = await fila.lote(10);
      await fila.adicionar(_ponto(30));
      await fila.removerAte(lote.last.id);

      expect(_segundos(await fila.lote(10)), [30]);
    });

    test('passando do limite, descarta os mais antigos', () async {
      final fila = FilaPosicoesMemoria(limite: 3);
      await _encher(fila, 5);

      expect(_segundos(await fila.lote(10)), [2, 3, 4]);
    });

    test('limpar esvazia a fila', () async {
      final fila = FilaPosicoesMemoria();
      await _encher(fila, 3);

      await fila.limpar();

      expect(await fila.lote(10), isEmpty);
    });
  });
}
