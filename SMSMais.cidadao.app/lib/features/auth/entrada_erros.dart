import 'package:sms_mais_cidadao/shared/api/http.dart';

// Erros da entrada do cidadão (ADR-0078) — espelho de `src/lib/entrada.ts` do PWA. O servidor já
// manda a mensagem para a pessoa; só dois códigos pedem outra coisa da tela.

/// O desafio da entrada venceu (vive 10 min no servidor): volta ao CPF com este aviso.
const avisoEntradaExpirada = 'O tempo para entrar acabou. Comece de novo.';

/// As rotas antigas de login respondem 410 `entrada.atualizar`: este app está desatualizado.
const _avisoAtualizar = 'Atualize o aplicativo para entrar.';

bool entradaExpirou(Object erro) => codigosDoErro(erro).contains('entrada.expirada');

/// Mensagem para a tela: a do servidor (bloqueio, dados que não conferem, e-SUS fora do ar…).
String mensagemDaEntrada(Object erro) {
  if (codigosDoErro(erro).contains('entrada.atualizar')) return _avisoAtualizar;
  return extrairMensagemDeErro(erro);
}
