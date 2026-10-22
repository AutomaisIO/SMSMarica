import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/demo_adapter.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// Entrada de DEMONSTRAÇÃO, para a revisão das lojas (Google Play, App Store — "Detalhes do login").
/// Com este CPF e esta data, o app passa a responder tudo pelo [DemoAdapter]: dados fictícios, sem
/// rede, sem tocar a produção. O CPF tem os 11 dígitos iguais — é inválido por regra, então nunca é
/// de ninguém, e o app não o mandaria ao servidor de qualquer jeito.
const cpfDemonstracao = '11111111111';

/// Data de nascimento da entrada de demonstração, em ISO (como o login manda ao servidor).
const nascimentoDemonstracao = '2000-01-01';

bool ehEntradaDeDemonstracao(String cpfDigitos, String? nascimentoIso) =>
    cpfDigitos == cpfDemonstracao && nascimentoIso == nascimentoDemonstracao;

/// O app está em demonstração? Liga no build com `DEMO=true` ([modoDemo]) ou na entrada de
/// demonstração; uma sessão de demonstração salva reabre em demonstração. Desliga ao sair dela.
final demonstracaoProvider = NotifierProvider<DemonstracaoController, bool>(DemonstracaoController.new);

class DemonstracaoController extends Notifier<bool> {
  @override
  bool build() {
    ref.listen(sessaoProvider, (antes, depois) {
      if (depois == null && antes?.token == DemoAdapter.tokenDemo) state = modoDemo;
    });
    return modoDemo || ref.read(sessaoInicialProvider)?.token == DemoAdapter.tokenDemo;
  }

  void ligar() => state = true;

  /// Volta ao normal — exceto no build de demonstração, que é demonstração do começo ao fim.
  void desligar() => state = modoDemo;
}
