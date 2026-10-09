import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:sms_mais_cidadao/app/app.dart';
import 'package:sms_mais_cidadao/shared/api/demo_adapter.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/push/push.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('pt_BR');
  await SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      systemNavigationBarColor: Color(0xFFFBF8F6),
      systemNavigationBarIconBrightness: Brightness.dark,
    ),
  );

  // A sessão salva é lida antes do 1º quadro: o app já abre no Início ou no login, sem piscar.
  final sessao = demoLogado
      ? SessaoCidadao(
          token: 'demo',
          paciente: PacienteSessao(
            id: DemoAdapter.sessaoDemo.id,
            nome: DemoAdapter.sessaoDemo.nome,
            cpf: DemoAdapter.sessaoDemo.cpf,
          ),
        )
      : await lerSessaoSalva();

  // Push (Firebase) só com a configuração do build; sem ela o app funciona igual. Não espera:
  // o registro do aparelho (no AppShell) aguarda a inicialização.
  if (pushConfigurado) iniciarPush().ignore();

  runApp(
    ProviderScope(
      overrides: [sessaoInicialProvider.overrideWithValue(sessao)],
      child: const AppCidadaoMarica(),
    ),
  );
}
