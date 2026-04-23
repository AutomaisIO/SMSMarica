import 'package:flutter/material.dart';
import 'package:sms_marica_cidadao/shared/theme/cores_marica.dart';

/// Placeholder da logo horizontal da Prefeitura de Maricá.
///
/// Substituir por `Image.asset('assets/brand/prefeitura_marica_horizontal.webp')`
/// quando o arquivo da logo for adicionado a `assets/brand/`.
class LogoMarica extends StatelessWidget {
  const LogoMarica({
    this.altura = 48,
    this.sobreVermelho = false,
    super.key,
  });

  final double altura;
  final bool sobreVermelho;

  @override
  Widget build(BuildContext context) {
    final corTexto = sobreVermelho
        ? CoresMarica.branco
        : CoresMarica.vermelhoPrincipal;
    final corSubtitulo = sobreVermelho
        ? CoresMarica.branco.withValues(alpha: 0.85)
        : CoresMarica.cinzaTexto;

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'PREFEITURA DE',
          style: TextStyle(
            color: corSubtitulo,
            fontSize: altura * 0.22,
            fontWeight: FontWeight.w500,
            letterSpacing: 2,
          ),
        ),
        Text(
          'MARICÁ',
          style: TextStyle(
            color: corTexto,
            fontSize: altura * 0.72,
            fontWeight: FontWeight.w900,
            height: 1,
            letterSpacing: -1,
          ),
        ),
        SizedBox(height: altura * 0.08),
        Text(
          'Secretaria Municipal de Saúde',
          style: TextStyle(
            color: corSubtitulo,
            fontSize: altura * 0.2,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}
