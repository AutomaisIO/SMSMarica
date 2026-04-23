import 'package:flutter/material.dart';
import 'package:sms_marica_cidadao/shared/theme/cores_marica.dart';

ThemeData construirTemaMarica() {
  const seedColor = CoresMarica.vermelhoPrincipal;

  final colorScheme = ColorScheme.fromSeed(
    seedColor: seedColor,
    primary: CoresMarica.vermelhoPrincipal,
    onPrimary: CoresMarica.branco,
    secondary: CoresMarica.vermelhoEscuro,
    surface: CoresMarica.branco,
    surfaceContainer: CoresMarica.cinzaFundo,
    error: CoresMarica.vermelhoEscuro,
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: colorScheme,
    scaffoldBackgroundColor: CoresMarica.cinzaFundo,
    appBarTheme: const AppBarTheme(
      backgroundColor: CoresMarica.vermelhoPrincipal,
      foregroundColor: CoresMarica.branco,
      elevation: 0,
      centerTitle: false,
      titleTextStyle: TextStyle(
        color: CoresMarica.branco,
        fontSize: 20,
        fontWeight: FontWeight.w600,
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: CoresMarica.vermelhoPrincipal,
        foregroundColor: CoresMarica.branco,
        minimumSize: const Size.fromHeight(52),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(8),
        ),
        textStyle: const TextStyle(
          fontSize: 16,
          fontWeight: FontWeight.w600,
        ),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: CoresMarica.vermelhoPrincipal,
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: CoresMarica.branco,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: Colors.transparent),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: Color(0xFFDDDDDD)),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(
          color: CoresMarica.vermelhoPrincipal,
          width: 2,
        ),
      ),
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
    ),
    cardTheme: CardThemeData(
      elevation: 0,
      color: CoresMarica.branco,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFEEEEEE)),
      ),
    ),
  );
}
