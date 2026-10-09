import 'package:flutter/services.dart';

// Espelho de `lib/cpf.ts`, `lib/telefone.ts` e das máscaras das telas do PWA.

String soDigitos(String? valor) => (valor ?? '').replaceAll(RegExp(r'\D'), '');

/// Só os dígitos do CPF (até 11).
String digitosCpf(String valor) {
  final d = soDigitos(valor);
  return d.length > 11 ? d.substring(0, 11) : d;
}

/// 000.000.000-00 enquanto digita.
String mascararCpf(String valor) {
  final d = digitosCpf(valor);
  if (d.length <= 3) return d;
  if (d.length <= 6) return '${d.substring(0, 3)}.${d.substring(3)}';
  if (d.length <= 9) return '${d.substring(0, 3)}.${d.substring(3, 6)}.${d.substring(6)}';
  return '${d.substring(0, 3)}.${d.substring(3, 6)}.${d.substring(6, 9)}-${d.substring(9)}';
}

/// CPF válido pelos dígitos verificadores (rejeita sequências repetidas).
bool cpfValido(String valor) {
  final d = digitosCpf(valor);
  if (d.length != 11 || RegExp(r'^(\d)\1{10}$').hasMatch(d)) return false;
  int digito(int ate) {
    var soma = 0;
    for (var i = 0; i < ate; i++) {
      soma += int.parse(d[i]) * (ate + 1 - i);
    }
    final resto = (soma * 10) % 11;
    return resto == 10 ? 0 : resto;
  }

  return digito(9) == int.parse(d[9]) && digito(10) == int.parse(d[10]);
}

/// Mostra só o meio do CPF: ***.456.789-**
String cpfParcial(String valor) {
  final d = digitosCpf(valor);
  return d.length == 11 ? '***.${d.substring(3, 6)}.${d.substring(6, 9)}-**' : valor;
}

/// CPF completo formatado (ou como veio, se não tiver 11 dígitos).
String formatarCpf(String cpf) {
  final d = soDigitos(cpf);
  return d.length == 11 ? mascararCpf(d) : cpf;
}

/// Cartão SUS em blocos: 000 0000 0000 0000.
String formatarCns(String cns) {
  final d = soDigitos(cns);
  if (d.length != 15) return cns;
  return '${d.substring(0, 3)} ${d.substring(3, 7)} ${d.substring(7, 11)} ${d.substring(11)}';
}

/// dd/mm/aaaa enquanto digita.
String mascararData(String valor) {
  var d = soDigitos(valor);
  if (d.length > 8) d = d.substring(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return '${d.substring(0, 2)}/${d.substring(2)}';
  return '${d.substring(0, 2)}/${d.substring(2, 4)}/${d.substring(4)}';
}

/// dd/mm/aaaa → aaaa-mm-dd; null se a data não existe, está no futuro ou é anterior a 1900.
String? dataParaIso(String valor) {
  final m = RegExp(r'^(\d{2})/(\d{2})/(\d{4})$').firstMatch(valor);
  if (m == null) return null;
  final dia = int.parse(m.group(1)!);
  final mes = int.parse(m.group(2)!);
  final ano = int.parse(m.group(3)!);
  final d = DateTime(ano, mes, dia);
  if (d.year != ano || d.month != mes || d.day != dia || d.isAfter(DateTime.now()) || ano < 1900) {
    return null;
  }
  return '${m.group(3)}-${m.group(2)}-${m.group(1)}';
}

/// (DDD) 9XXXX-XXXX enquanto digita.
String mascararTelefone(String valor) {
  var d = soDigitos(valor);
  if (d.length > 11) d = d.substring(0, 11);
  if (d.length <= 2) return d;
  if (d.length <= 6) return '(${d.substring(0, 2)}) ${d.substring(2)}';
  if (d.length <= 10) return '(${d.substring(0, 2)}) ${d.substring(2, 6)}-${d.substring(6)}';
  return '(${d.substring(0, 2)}) ${d.substring(2, 7)}-${d.substring(7)}';
}

/// Normaliza o celular digitado para 55 + DDD + número (Maricá = DDD 21). Aceita com/sem DDI,
/// com 0 de tronco, com/sem DDD e com qualquer pontuação.
String normalizarCelularBr(String entrada) {
  var d = soDigitos(entrada);
  if (d.isEmpty) return '';
  if (d.startsWith('55') && (d.length == 12 || d.length == 13)) return d;
  d = d.replaceFirst(RegExp('^0+'), '');
  if (d.length == 10 || d.length == 11) return '55$d';
  if (d.length == 8 || d.length == 9) return '5521$d';
  return d.startsWith('55') ? d : '5521$d';
}

/// Formatador que reaplica uma máscara a cada tecla, com o cursor no fim.
class MascaraFormatter extends TextInputFormatter {
  MascaraFormatter(this.mascarar);

  final String Function(String) mascarar;

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final texto = mascarar(newValue.text);
    return TextEditingValue(text: texto, selection: TextSelection.collapsed(offset: texto.length));
  }
}
