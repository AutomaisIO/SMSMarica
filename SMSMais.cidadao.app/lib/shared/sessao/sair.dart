import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/cache/arquivos_cache.dart';
import 'package:sms_mais_cidadao/shared/cache/dados_cache.dart';
import 'package:sms_mais_cidadao/shared/localizacao/localizacao.dart';
import 'package:sms_mais_cidadao/shared/push/push.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/sincronizar.dart';

/// Encerra a sessão: avisa o servidor (se falhar, encerra local do mesmo jeito) e — LGPD — tira do
/// aparelho os dados e documentos sincronizados e o token do push (o celular pode ser compartilhado).
Future<void> sairDoApp(WidgetRef ref, {bool avisarServidor = true}) async {
  if (avisarServidor) {
    // Para de compartilhar a localização antes de encerrar a sessão (ADR-0082). No máximo 2 s, e nada
    // aqui segura a saída.
    try {
      await ref.read(localizacaoProvider.notifier).parar().timeout(const Duration(seconds: 2));
    } on Object {
      /* segue para a saída */
    }
    try {
      await ref.read(apiProvider).logout();
    } on Object {
      /* mesmo se falhar no servidor, encerra a sessão local */
    }
  }
  await apagarTokenDoPush();
  await DadosCache.limparTudo();
  await ArquivosCache.limpar();
  ref.read(sincronizadorProvider).resetar();
  ref.read(confirmacaoPendenteProvider.notifier).state = null;
  await ref.read(sessaoProvider.notifier).sair();
}

/// Confirmação de agendamento feita pelo link do WhatsApp — a tela Exames agendados mostra o
/// modal "Agenda confirmada" e destaca o card (no PWA isto vai pelo `sessionStorage`).
final confirmacaoPendenteProvider = StateProvider<ConfirmacaoAgendamento?>((_) => null);
