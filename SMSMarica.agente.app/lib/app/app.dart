import 'package:agente/app/router.dart';
import 'package:agente/app/theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

class AgenteApp extends ConsumerWidget {
  const AgenteApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'SMS Maricá — Agente',
      theme: MaricaTheme.light(),
      debugShowCheckedModeBanner: false,
      routerConfig: router,
    );
  }
}
