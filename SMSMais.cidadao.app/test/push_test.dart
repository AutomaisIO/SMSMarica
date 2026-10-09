import 'package:flutter_test/flutter_test.dart';
import 'package:sms_mais_cidadao/shared/push/destinos.dart';
import 'package:sms_mais_cidadao/shared/push/push.dart';

void main() {
  group('MarcaDoRegistro (montar + volta ao primeiro plano)', () {
    test('registra uma vez por sessão; as voltas seguintes não fazem nada', () {
      final marca = MarcaDoRegistro();
      expect(marca.proximo('a'), PassoDoRegistro.registrar);
      expect(marca.proximo('a'), PassoDoRegistro.nada);
      expect(marca.proximo('a'), PassoDoRegistro.nada);
    });

    test('falha (API fora, sem rede) libera nova tentativa na volta', () {
      final marca = MarcaDoRegistro()..proximo('a');
      marca.falhou('a');
      expect(marca.proximo('a'), PassoDoRegistro.registrar);
      expect(marca.proximo('a'), PassoDoRegistro.nada);
    });

    test('negou: a volta só confere a permissão (sem diálogo), uma conferência por vez', () {
      final marca = MarcaDoRegistro()..proximo('a');
      marca.negou('a');
      expect(marca.proximo('a'), PassoDoRegistro.conferirPermissao);
      // Conferência em andamento: outra volta não confere de novo nem pede a permissão.
      expect(marca.proximo('a'), PassoDoRegistro.nada);
      // Continua negada: a próxima volta confere de novo — nunca volta a "registrar".
      marca.negou('a');
      expect(marca.proximo('a'), PassoDoRegistro.conferirPermissao);
    });

    test('sessão nova registra de novo; falha/negativa da sessão antiga não mexe na nova', () {
      final marca = MarcaDoRegistro()..proximo('a');
      expect(marca.proximo('b'), PassoDoRegistro.registrar);
      marca
        ..falhou('a')
        ..negou('a');
      expect(marca.proximo('b'), PassoDoRegistro.nada);
    });
  });

  group('rotaDoPush (lista fixa de destinos)', () {
    test('aceita as 9 telas da lista', () {
      const telas = [
        '/',
        '/agendados/consultas',
        '/agendados/exames',
        '/atendimentos',
        '/exames',
        '/documentos',
        '/transporte',
        '/chat',
        '/perfil',
      ];
      expect(destinosDoPush, hasLength(telas.length));
      for (final rota in telas) {
        expect(rotaDoPush(rota), rota, reason: rota);
      }
    });

    test('recusa o que não está na lista: nulo, vazio, rota pública, externo', () {
      const recusadas = [
        null,
        '',
        ' ',
        '/login',
        '/entrar/abc',
        '/documento/x',
        '/agendados/exames/123',
        '/exames?exame=1',
        '/exames/',
        'exames',
        '/EXAMES',
        ' /exames',
        'https://exemplo.com/',
        '//exemplo.com/exames',
        'javascript:alert(1)',
      ];
      for (final rota in recusadas) {
        expect(rotaDoPush(rota), isNull, reason: '$rota');
      }
    });
  });

  group('destinoDoToque', () {
    test('sem rota abre o Início (o "Início" do painel não manda rota)', () {
      expect(destinoDoToque(null), '/');
    });

    test('rota da lista abre a tela; fora da lista não navega', () {
      expect(destinoDoToque('/exames'), '/exames');
      expect(destinoDoToque('/'), '/');
      expect(destinoDoToque(''), isNull);
      expect(destinoDoToque('/login'), isNull);
      expect(destinoDoToque('https://exemplo.com/'), isNull);
    });
  });
}
