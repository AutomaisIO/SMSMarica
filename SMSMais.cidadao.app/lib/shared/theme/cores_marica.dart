import 'package:flutter/material.dart';

/// Paleta de cores da identidade visual da Prefeitura de Maricá.
///
/// Valores derivados da logo horizontal vermelha da Prefeitura. Caso a
/// Secretaria forneça paleta oficial com tokens exatos, atualizar aqui.
abstract final class CoresMarica {
  /// Vermelho principal da Prefeitura.
  static const Color vermelhoPrincipal = Color(0xFFC8102E);

  /// Variante mais escura para estados pressionados / hover / elevação.
  static const Color vermelhoEscuro = Color(0xFF9F0B22);

  /// Variante clara para fundos de destaque sutis.
  static const Color vermelhoClaro = Color(0xFFE04A5F);

  /// Branco base — fundos e texto sobre vermelho.
  static const Color branco = Color(0xFFFFFFFF);

  /// Cinza para fundo neutro da UI.
  static const Color cinzaFundo = Color(0xFFF5F5F5);

  /// Cinza para texto secundário.
  static const Color cinzaTexto = Color(0xFF6B6B6B);

  /// Preto para texto principal.
  static const Color preto = Color(0xFF1A1A1A);

  /// Verde sucesso (mantido em tom neutro para não competir com o vermelho).
  static const Color sucesso = Color(0xFF2E7D32);

  /// Amarelo de atenção.
  static const Color atencao = Color(0xFFF9A825);
}
