import 'package:agente/app/router.dart';
import 'package:agente/app/theme.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/gps/servico_gps.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

class AgenteApp extends ConsumerWidget {
  const AgenteApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    // Liga/desliga o compartilhamento de GPS conforme a sessão do motorista.
    ref.listen<Sessao?>(sessaoProvider, (anterior, atual) {
      final gps = ref.read(servicoGpsProvider);
      if (atual != null) {
        gps.iniciar(atual.motoristaId);
      } else {
        gps.parar();
      }
    });

    return MaterialApp.router(
      title: 'SMS Maricá — Agente',
      theme: MaricaTheme.light(),
      debugShowCheckedModeBanner: false,
      routerConfig: router,
    );
  }
}
