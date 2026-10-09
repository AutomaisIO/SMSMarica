import 'package:flutter/material.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';

ThemeData construirTemaMarica() {
  final colorScheme = ColorScheme.fromSeed(
    seedColor: CoresMarica.marica,
    primary: CoresMarica.marica,
    onPrimary: CoresMarica.branco,
    secondary: CoresMarica.lagoa,
    onSecondary: CoresMarica.branco,
    surface: CoresMarica.papel,
    onSurface: CoresMarica.tinta,
    error: CoresMarica.marica,
  );

  final base = ThemeData(useMaterial3: true, colorScheme: colorScheme, fontFamily: FontesMarica.sans);

  return base.copyWith(
    scaffoldBackgroundColor: CoresMarica.papel,
    // O PWA não tem "ripple" de Material: o retorno do toque é escala + fundo. Aqui o splash
    // vira um realce discreto para não parecer outro app.
    splashFactory: InkSparkle.splashFactory,
    highlightColor: CoresMarica.areia.withValues(alpha: 0.4),
    // Tracking zerado em toda a escala do Material 3: o PWA (Tailwind) não tem espaçamento extra
    // entre letras, e o padrão do M3 (0,25–0,5px) deixava os textos mais largos e quebrando linha.
    textTheme: _semTracking(
      base.textTheme.apply(
        fontFamily: FontesMarica.sans,
        bodyColor: CoresMarica.tinta,
        displayColor: CoresMarica.tinta,
      ),
    ),
    textSelectionTheme: const TextSelectionThemeData(
      cursorColor: CoresMarica.lagoa,
      selectionHandleColor: CoresMarica.lagoa,
    ),
    progressIndicatorTheme: const ProgressIndicatorThemeData(color: CoresMarica.marica),
    sliderTheme: const SliderThemeData(
      activeTrackColor: CoresMarica.marica,
      thumbColor: CoresMarica.marica,
      inactiveTrackColor: CoresMarica.areia,
    ),
    snackBarTheme: SnackBarThemeData(
      backgroundColor: CoresMarica.tinta,
      contentTextStyle: Txt.sans(14, cor: CoresMarica.branco),
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(RaiosMarica.xl)),
    ),
    pageTransitionsTheme: const PageTransitionsTheme(
      builders: {
        TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
        TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
      },
    ),
  );
}

TextTheme _semTracking(TextTheme t) {
  TextStyle? z(TextStyle? s) => s?.copyWith(letterSpacing: 0);
  return t.copyWith(
    displayLarge: z(t.displayLarge),
    displayMedium: z(t.displayMedium),
    displaySmall: z(t.displaySmall),
    headlineLarge: z(t.headlineLarge),
    headlineMedium: z(t.headlineMedium),
    headlineSmall: z(t.headlineSmall),
    titleLarge: z(t.titleLarge),
    titleMedium: z(t.titleMedium),
    titleSmall: z(t.titleSmall),
    bodyLarge: z(t.bodyLarge),
    bodyMedium: z(t.bodyMedium),
    bodySmall: z(t.bodySmall),
    labelLarge: z(t.labelLarge),
    labelMedium: z(t.labelMedium),
    labelSmall: z(t.labelSmall),
  );
}
