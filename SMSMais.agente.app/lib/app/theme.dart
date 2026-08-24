import 'package:flutter/material.dart';

/// Tema Maricá — vermelho (paleta da logo horizontal) + branco.
/// Tokens em um único lugar; expansão futura em `shared/ui/` conforme cresce.
class MaricaTheme {
  MaricaTheme._();

  static const Color vermelho = Color(0xFFC8102E);
  static const Color vermelhoEscuro = Color(0xFF8F0A1F);
  static const Color branco = Color(0xFFFFFFFF);
  static const Color cinzaFundo = Color(0xFFF5F5F5);
  static const Color cinzaTexto = Color(0xFF333333);

  static ThemeData light() {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: vermelho,
      primary: vermelho,
      onPrimary: branco,
      surface: branco,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: cinzaFundo,
      appBarTheme: const AppBarTheme(
        backgroundColor: vermelho,
        foregroundColor: branco,
        elevation: 0,
        centerTitle: true,
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: vermelho,
          foregroundColor: branco,
          minimumSize: const Size.fromHeight(52),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(8),
          ),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: branco,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: BorderSide.none,
        ),
      ),
    );
  }
}
