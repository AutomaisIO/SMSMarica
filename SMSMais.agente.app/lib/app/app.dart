import 'package:agente/app/router.dart';
import 'package:agente/app/theme.dart';
import 'package:agente/features/dispositivo/application/envio_posicoes_service.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

class AgenteApp extends ConsumerWidget {
  const AgenteApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    // GPS do tablet e envio ao Mapa da frota rodam desde a abertura do app — o
    // tablet é do veículo, não depende do login do motorista. O antigo
    // ServicoGps (POST /rastreamento/pontos com motoristaId mock, sem auth)
    // foi desligado: só gerava 401. Ver docs/modulos/tfd/deslocamento-tablet.md.
    ref
      ..watch(gpsAoVivoProvider.notifier)
      ..watch(envioPosicoesProvider);

    return MaterialApp.router(
      title: 'SMS Maricá — Agente',
      theme: MaricaTheme.light(),
      debugShowCheckedModeBanner: false,
      routerConfig: router,
    );
  }
}
