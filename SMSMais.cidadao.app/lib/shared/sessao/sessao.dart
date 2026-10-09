import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';

/// Sessão do cidadão: o JWT (claims `tipo=cidadao`, `sub=patientId`, `jti=sessão`) e o resumo do
/// paciente devolvido no login. Single-device (ADR-0018): entrar em outro aparelho revoga esta
/// sessão e a API passa a responder 401 — o interceptor HTTP então chama [SessaoController.sair].
class SessaoCidadao {
  const SessaoCidadao({required this.token, required this.paciente});

  final String token;
  final PacienteSessao paciente;
}

/// Mesmo nome de chave do PWA (`smsmarica-paciente-auth`) — carve-out do ADR-0046, não renomear.
const _chave = 'smsmarica-paciente-auth';
const _armazem = FlutterSecureStorage();

/// Lê a sessão salva no aparelho. Chamado uma vez no `main`, antes do primeiro quadro, para o
/// roteador já nascer sabendo se vai para o login ou para o Início.
Future<SessaoCidadao?> lerSessaoSalva() async {
  try {
    final bruto = await _armazem.read(key: _chave);
    if (bruto == null) return null;
    final j = jsonDecode(bruto) as Json;
    return SessaoCidadao(
      token: j['token'] as String,
      paciente: PacienteSessao.deJson(j['paciente'] as Json),
    );
  } on Object {
    return null;
  }
}

/// Valor lido no `main` — sobrescrito no `ProviderScope`.
final sessaoInicialProvider = Provider<SessaoCidadao?>((_) => null);

class SessaoController extends Notifier<SessaoCidadao?> {
  @override
  SessaoCidadao? build() => ref.read(sessaoInicialProvider);

  Future<void> entrar(String token, PacienteSessao paciente) async {
    state = SessaoCidadao(token: token, paciente: paciente);
    try {
      await _armazem.write(
        key: _chave,
        value: jsonEncode({'token': token, 'paciente': paciente.paraJson()}),
      );
    } on Object {
      /* sem armazenamento seguro: a sessão vale até fechar o app */
    }
  }

  Future<void> sair() async {
    if (state == null) return;
    state = null;
    try {
      await _armazem.delete(key: _chave);
    } on Object {
      /* best-effort */
    }
  }
}

final sessaoProvider = NotifierProvider<SessaoController, SessaoCidadao?>(SessaoController.new);
