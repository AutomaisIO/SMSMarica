import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/app/router.dart';
import 'package:sms_mais_cidadao/app/theme.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';

class AppCidadaoMarica extends ConsumerWidget {
  const AppCidadaoMarica({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'Saúde Maricá',
      debugShowCheckedModeBanner: false,
      theme: construirTemaMarica(),
      routerConfig: router,
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: const [Locale('pt', 'BR')],
      locale: const Locale('pt', 'BR'),
      builder: (context, filho) => _Moldura(child: filho ?? const SizedBox.shrink()),
    );
  }
}

/// Em tela larga (tablet, web) o app fica numa coluna de 460px sobre a moldura — como o PWA no
/// desktop. No celular a coluna ocupa a tela toda e a moldura não aparece.
class _Moldura extends StatelessWidget {
  const _Moldura({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final largura = MediaQuery.sizeOf(context).width;
    if (largura <= 460) return child;
    return ColoredBox(
      color: CoresMarica.moldura,
      child: Center(
        child: Container(
          width: 460,
          decoration: const BoxDecoration(boxShadow: SombrasMarica.flutuante),
          child: MediaQuery(
            data: MediaQuery.of(context).copyWith(size: Size(460, MediaQuery.sizeOf(context).height)),
            child: child,
          ),
        ),
      ),
    );
  }
}
