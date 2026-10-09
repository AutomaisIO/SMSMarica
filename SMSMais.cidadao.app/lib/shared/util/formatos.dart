import 'package:intl/intl.dart';

// Datas e tamanhos como o PWA mostra (`toLocaleDateString('pt-BR')` etc.). A API manda instantes
// em UTC (ISO com `Z`) e datas puras (`aaaa-mm-dd`); instantes são exibidos no horário local.

DateTime? _instante(String iso) {
  final d = DateTime.tryParse(iso);
  return d?.toLocal();
}

/// 02/10/2026
String formatarData(String iso) {
  final d = _instante(iso);
  return d == null ? iso : DateFormat('dd/MM/yyyy', 'pt_BR').format(d);
}

/// 02/10/2026, 14:30 (mesmo formato do `toLocaleString('pt-BR', …)` do PWA).
String formatarDataHora(String iso) {
  final d = _instante(iso);
  return d == null ? iso : DateFormat('dd/MM/yyyy, HH:mm', 'pt_BR').format(d);
}

/// Data pura `aaaa-mm-dd` (sem fuso: vale como está) → dd/mm/aaaa.
String? formatarDataPura(String? iso) {
  if (iso == null || iso.isEmpty) return null;
  final p = iso.substring(0, iso.length >= 10 ? 10 : iso.length).split('-');
  return p.length == 3 ? '${p[2]}/${p[1]}/${p[0]}' : iso;
}

/// "2026-10-02" → "sexta-feira, 02/10".
String formatarDiaSemana(String iso) {
  final p = iso.split('-').map(int.tryParse).toList();
  if (p.length < 3 || p.contains(null)) return iso;
  final d = DateTime(p[0]!, p[1]!, p[2]!);
  return DateFormat('EEEE, dd/MM', 'pt_BR').format(d);
}

String formatarTamanho(int bytes) {
  if (bytes < 1024) return '$bytes B';
  if (bytes < 1024 * 1024) return '${(bytes / 1024).round()} KB';
  return '${(bytes / (1024 * 1024)).toStringAsFixed(1).replaceAll('.', ',')} MB';
}

String capitalizar(String s) => s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);
