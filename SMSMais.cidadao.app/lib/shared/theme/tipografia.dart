import 'package:flutter/material.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';

/// Famílias empacotadas (pubspec): `Bricolage` = `font-display` do PWA; `Inter` = `font-sans`.
abstract final class FontesMarica {
  static const String display = 'Bricolage';
  static const String sans = 'Inter';

  /// `font-mono` do PWA.
  static const String mono = 'RobotoMono';
}

/// Atalhos de estilo com a escala do Tailwind (xs=12, sm=14, base=16, lg=18, xl=20, 2xl=24),
/// para as telas lerem como as classes do PWA: `Txt.display(18, w600)` ≈ `font-display text-lg
/// font-semibold`.
abstract final class Txt {
  static TextStyle display(
    double tamanho, {
    FontWeight peso = FontWeight.w600,
    Color cor = CoresMarica.tinta,
    double? altura,
    double? espacamento,
  }) =>
      TextStyle(
        fontFamily: FontesMarica.display,
        fontSize: tamanho,
        fontWeight: peso,
        color: cor,
        height: altura,
        // Sem isto o Text herda o tracking do Material 3 (0,25–0,5px) e fica ~4% mais largo que o
        // PWA (Tailwind = tracking normal, 0).
        letterSpacing: espacamento ?? 0,
      );

  static TextStyle sans(
    double tamanho, {
    FontWeight peso = FontWeight.w400,
    Color cor = CoresMarica.tinta,
    double? altura,
    double? espacamento,
    List<FontFeature>? recursos,
  }) =>
      TextStyle(
        fontFamily: FontesMarica.sans,
        fontSize: tamanho,
        fontWeight: peso,
        color: cor,
        height: altura,
        letterSpacing: espacamento ?? 0,
        fontFeatures: recursos,
      );

  /// `text-[11px] font-semibold uppercase tracking-[0.18em] text-marica` — o "eyebrow".
  static TextStyle sobretitulo({Color cor = CoresMarica.marica, double espacamentoEm = 0.18}) =>
      sans(11, peso: FontWeight.w600, cor: cor, espacamento: 11 * espacamentoEm);

  /// Números alinhados (`tabular-nums`).
  static const List<FontFeature> tabular = [FontFeature.tabularFigures()];
}
