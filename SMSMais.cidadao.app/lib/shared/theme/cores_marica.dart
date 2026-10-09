import 'package:flutter/material.dart';

/// Paleta do App do Cidadão — **a mesma do PWA** (`SMSMais.cidadao.pwa/tailwind.config.ts`).
/// Mudou lá, muda aqui: os dois apps precisam ter a mesma cara.
///
/// Civismo Maricá (vermelho/vinho), superfícies quentes (papel de documento, não cinza-frio) e a
/// afordância clínica `lagoa` (orla/lagoas de Maricá).
abstract final class CoresMarica {
  // Civismo Maricá
  static const Color marica = Color(0xFFC8102E);
  static const Color maricaEscuro = Color(0xFFA00C24);
  static const Color vinho = Color(0xFF6E1322);

  // Superfícies quentes
  static const Color papel = Color(0xFFFBF8F6);
  static const Color areia = Color(0xFFECE3E1);

  /// Moldura atrás do "frame" do app em tela larga (tablet/web) — o `body` do PWA.
  static const Color moldura = Color(0xFFEFE7E3);

  // Tinta (texto)
  static const Color tinta = Color(0xFF201A1B);
  static const Color tintaMute = Color(0xFF6F6466);

  // Afordância clínica
  static const Color lagoa = Color(0xFF0E7C7B);
  static const Color lagoaEscuro = Color(0xFF0A5E5D);
  static const Color lagoaClaro = Color(0xFFE5F2F1);

  static const Color branco = Color(0xFFFFFFFF);

  // Tons semânticos do Tailwind usados pelo PWA (amber / red / green).
  static const Color ambar50 = Color(0xFFFFFBEB);
  static const Color ambar100 = Color(0xFFFEF3C7);
  static const Color ambar200 = Color(0xFFFDE68A);
  static const Color ambar300 = Color(0xFFFCD34D);
  static const Color ambar600 = Color(0xFFD97706);
  static const Color ambar700 = Color(0xFFB45309);
  static const Color ambar800 = Color(0xFF92400E);
  static const Color ambar900 = Color(0xFF78350F);

  static const Color vermelho50 = Color(0xFFFEF2F2);
  static const Color vermelho700 = Color(0xFFB91C1C);
  static const Color vermelho800 = Color(0xFF991B1B);

  static const Color verde50 = Color(0xFFF0FDF4);
  static const Color verde500 = Color(0xFF22C55E);
  static const Color verde600 = Color(0xFF16A34A);
  static const Color verde800 = Color(0xFF166534);

  /// Gradiente do "Cartão do Cidadão", do hero do login e do cabeçalho do ticket
  /// (`bg-gradient-to-br from-vinho to-marica`).
  static const LinearGradient gradienteCivico = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [vinho, marica],
  );
}

/// Raios do PWA (Tailwind com `xl`/`2xl`/`3xl` estendidos).
abstract final class RaiosMarica {
  static const double lg = 8;
  static const double xl = 14;
  static const double x2l = 20;
  static const double x3l = 28;
}

/// Sombras do PWA (`shadow-carta`, `shadow-cartao`, `shadow-topo`).
abstract final class SombrasMarica {
  static const List<BoxShadow> carta = [
    BoxShadow(color: Color(0x0A201A1B), offset: Offset(0, 1), blurRadius: 2),
    BoxShadow(color: Color(0x2E201A1B), offset: Offset(0, 8), blurRadius: 24, spreadRadius: -12),
  ];

  static const List<BoxShadow> cartao = [
    BoxShadow(color: Color(0x8C6E1322), offset: Offset(0, 18), blurRadius: 40, spreadRadius: -20),
  ];

  static const List<BoxShadow> topo = [
    BoxShadow(color: Color(0x40201A1B), offset: Offset(0, 2), blurRadius: 12, spreadRadius: -6),
  ];

  /// `shadow-2xl` do Tailwind — folhas e menus flutuantes.
  static const List<BoxShadow> flutuante = [
    BoxShadow(color: Color(0x40000000), offset: Offset(0, 25), blurRadius: 50, spreadRadius: -12),
  ];
}
