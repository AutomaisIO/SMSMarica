import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sms_mais_cidadao/app/app.dart';
import 'package:sms_mais_cidadao/shared/api/demo_adapter.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';

/// App com a API trocada pelo modo demonstração (sem rede, dados fictícios).
Widget _app({SessaoCidadao? sessao, Duration atraso = Duration.zero}) {
  final dio = Dio(BaseOptions(baseUrl: 'https://demo'))..httpClientAdapter = DemoAdapter(atraso: atraso);
  return ProviderScope(
    overrides: [
      sessaoInicialProvider.overrideWithValue(sessao),
      dioProvider.overrideWithValue(dio),
    ],
    child: const AppCidadaoMarica(),
  );
}

/// Tela de celular comum (360×780 dp), não o 800×600 padrão do teste.
void _celular(WidgetTester tester) {
  tester.view
    ..physicalSize = const Size(1080, 2340)
    ..devicePixelRatio = 3;
  addTearDown(tester.view.reset);
}

/// Toca depois de rolar até o alvo (o botão pode estar abaixo da dobra).
Future<void> _tocar(WidgetTester tester, Finder alvo) async {
  await tester.ensureVisible(alvo);
  await tester.pump();
  await tester.tap(alvo);
}

/// Avança a tela sem esperar animações infinitas (o esqueleto pulsa até a lista chegar).
Future<void> _assentar(WidgetTester tester) async {
  for (var i = 0; i < 20; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

void main() {
  setUpAll(() async {
    await initializeDateFormatting('pt_BR');
  });

  setUp(() {
    SharedPreferences.setMockInitialValues({});
  });

  group('documentos_br', () {
    test('CPF: máscara, validação e parcial', () {
      expect(mascararCpf('52998224725'), '529.982.247-25');
      expect(cpfValido('529.982.247-25'), isTrue);
      expect(cpfValido('111.111.111-11'), isFalse);
      expect(cpfParcial('52998224725'), '***.982.247-**');
    });

    test('celular: normaliza como o PWA (DDD 21 por padrão)', () {
      expect(normalizarCelularBr('999990000'), '5521999990000');
      expect(normalizarCelularBr('021 99999-0000'), '5521999990000');
      expect(normalizarCelularBr('+55 (21) 99999-0000'), '5521999990000');
    });

    test('data: dd/mm/aaaa → ISO, recusando data impossível ou futura', () {
      expect(dataParaIso('20/05/1961'), '1961-05-20');
      expect(dataParaIso('31/02/1990'), isNull);
      expect(dataParaIso('01/01/2999'), isNull);
    });
  });

  testWidgets('sem sessão abre o login com CPF e data de nascimento', (tester) async {
    _celular(tester);
    await tester.pumpWidget(_app());
    await _assentar(tester);

    expect(find.text('Entrar'), findsWidgets);
    expect(find.text('SEU CPF'), findsOneWidget);
    expect(find.text('DATA DE NASCIMENTO'), findsOneWidget);
    expect(find.text('Continuar'), findsOneWidget);
  });

  testWidgets('login confere CPF e data antes de continuar', (tester) async {
    _celular(tester);
    await tester.pumpWidget(_app());
    await _assentar(tester);

    // CPF com dígito verificador errado: avisa e não sai da tela.
    await tester.enterText(find.byType(TextField).at(0), '52998224724');
    await tester.enterText(find.byType(TextField).at(1), '20051961');
    await tester.pump();
    expect(find.text('CPF inválido. Confira os números.'), findsOneWidget);
    await _tocar(tester, find.text('Continuar'));
    await _assentar(tester);
    expect(find.text('Para onde enviamos o código?'), findsNothing);

    // Data que não existe no calendário.
    await tester.enterText(find.byType(TextField).at(0), '52998224725');
    await tester.enterText(find.byType(TextField).at(1), '31021990');
    await tester.pump();
    expect(find.text('CPF inválido. Confira os números.'), findsNothing);
    expect(find.text('Data inválida. Confira o dia, o mês e o ano.'), findsOneWidget);
    await _tocar(tester, find.text('Continuar'));
    await _assentar(tester);
    expect(find.text('Para onde enviamos o código?'), findsNothing);

    await tester.enterText(find.byType(TextField).at(1), '20051961');
    await tester.pump();
    expect(find.text('Data inválida. Confira o dia, o mês e o ano.'), findsNothing);
    await _tocar(tester, find.text('Continuar'));
    await _assentar(tester);
    expect(find.text('Para onde enviamos o código?'), findsOneWidget);
  });

  testWidgets('escolha do WhatsApp: finais mascarados, e-SUS e posto', (tester) async {
    _celular(tester);
    // Com atraso, para ver o "procurando" enquanto o e-SUS responde.
    await tester.pumpWidget(_app(atraso: const Duration(milliseconds: 500)));
    await _assentar(tester);
    await tester.enterText(find.byType(TextField).at(0), '52998224725');
    await tester.enterText(find.byType(TextField).at(1), '20051961');
    await tester.pump();
    await _tocar(tester, find.text('Continuar'));
    await _assentar(tester);

    // Só o final de cada número, com a origem; o botão espera a escolha.
    expect(find.text('WhatsApp final 4321'), findsOneWidget);
    expect(find.text('WhatsApp final 7788'), findsOneWidget);
    expect(find.text('Do seu cadastro'), findsNWidgets(2));
    expect(find.text('Enviar código'), findsOneWidget);
    expect(find.text('Procurar outros números'), findsNothing);

    // "Nenhum desses é meu" com o e-SUS permitido: oferece a busca, não o envio.
    await _tocar(tester, find.text('Nenhum desses é meu'));
    await tester.pump();
    expect(find.text('Enviar código'), findsNothing);
    expect(find.text('Procurar outros números'), findsOneWidget);

    await _tocar(tester, find.text('Procurar outros números'));
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.text('Procurando outros números… isso pode levar alguns segundos.'), findsOneWidget);
    await _assentar(tester);

    // O número novo entra na lista e a escolha recomeça.
    expect(find.text('WhatsApp final 5566'), findsOneWidget);
    expect(find.text('Encontrado no e-SUS'), findsOneWidget);
    expect(find.text('Enviar código'), findsOneWidget);

    // e-SUS já consultado: "nenhum desses" agora leva ao posto.
    await _tocar(tester, find.text('Nenhum desses é meu'));
    await tester.pump();
    expect(find.text('Procurar outros números'), findsNothing);
    expect(find.textContaining('para atualizar o seu WhatsApp'), findsOneWidget);
  });

  testWidgets('login por CPF + nascimento + código chega ao Cartão do Cidadão', (tester) async {
    _celular(tester);
    await tester.pumpWidget(_app());
    await _assentar(tester);

    await tester.enterText(find.byType(TextField).at(0), '52998224725');
    await tester.enterText(find.byType(TextField).at(1), '20051961');
    await tester.pump();
    await _tocar(tester, find.text('Continuar'));
    await _assentar(tester);

    await _tocar(tester, find.text('WhatsApp final 4321'));
    await tester.pump();
    await _tocar(tester, find.text('Enviar código'));
    await _assentar(tester);

    expect(find.text('Código de acesso'), findsOneWidget);
    expect(find.text('Enviado para o WhatsApp final 4321.'), findsOneWidget);
    await tester.enterText(find.byType(TextField).first, '123456');
    await tester.pump();
    await _tocar(tester, find.text('Entrar').last);
    await _assentar(tester);

    // Termo LGPD (o modo demonstração começa sem aceite).
    expect(find.text('Li e concordo'), findsOneWidget);
    await _tocar(tester, find.text('Li e concordo'));
    await _assentar(tester);

    expect(find.text('CARTÃO DO CIDADÃO'), findsOneWidget);
    expect(find.text('Saúde Maricá'), findsWidgets);
  });

  testWidgets('com sessão salva abre direto no Início', (tester) async {
    _celular(tester);
    await tester.pumpWidget(
      _app(
        sessao: const SessaoCidadao(
          token: 'demo',
          paciente: PacienteSessao(id: 'p1', nome: 'MARIA DA CONCEIÇÃO EXEMPLO', cpf: '52998224725'),
        ),
      ),
    );
    await _assentar(tester);
    await _tocar(tester, find.text('Li e concordo'));
    await _assentar(tester);

    expect(find.textContaining('Olá,'), findsOneWidget);
    expect(find.text('Consultas'), findsOneWidget);
  });

  testWidgets('agenda de exames mostra próximos, fila e anteriores', (tester) async {
    _celular(tester);
    await tester.pumpWidget(
      _app(
        sessao: const SessaoCidadao(
          token: 'demo',
          paciente: PacienteSessao(id: 'p1', nome: 'MARIA DA CONCEIÇÃO EXEMPLO', cpf: '52998224725'),
        ),
      ),
    );
    await _assentar(tester);
    await _tocar(tester, find.text('Li e concordo'));
    await _assentar(tester);

    // O primeiro "Exames" do Início é a agenda; o outro, os resultados.
    await _tocar(tester, find.text('Exames').first);
    await _assentar(tester);

    expect(find.text('PRÓXIMOS EXAMES'), findsOneWidget);
    expect(find.text('NA FILA, AGUARDANDO VAGA'), findsOneWidget);
    expect(find.text('EXAMES ANTERIORES'), findsOneWidget);
    expect(find.textContaining('Está na fila da regulação municipal (SISREG)'), findsOneWidget);
    expect(find.text('Realizado'), findsOneWidget);
  });
}
