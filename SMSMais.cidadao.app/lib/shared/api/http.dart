import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/demo_adapter.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// Endereço da API. Produção por padrão (como o build do PWA); troca com
/// `--dart-define=API_BASE_URL=http://10.0.2.2:5080` para apontar para o backend local.
const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'https://api.smsmarica.online');

/// Modo demonstração: `--dart-define=DEMO=true` responde tudo com dados fictícios, sem rede —
/// para conferir telas e para os testes. Nunca ligado num build de loja.
const modoDemo = bool.fromEnvironment('DEMO');

/// Com o modo demonstração: abre já logado e com o termo aceito (`--dart-define=DEMO_LOGADO=true`),
/// para fotografar as telas internas sem passar pelo login.
const demoLogado = modoDemo && bool.fromEnvironment('DEMO_LOGADO');

/// Endereço público do app (páginas estáticas de privacidade/termos e links do WhatsApp).
const appWebUrl = String.fromEnvironment('APP_WEB_URL', defaultValue: 'https://app.smsmarica.online');

final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: apiBaseUrl,
      connectTimeout: const Duration(seconds: 20),
      // PDF das imagens de um exame é gerado na hora e pode demorar.
      receiveTimeout: const Duration(seconds: 90),
      headers: {
        'Accept': 'application/json',
        // O painel mostra este texto na ficha do paciente (Histórico de acesso e aparelhos que
        // recebem notificação); sem ele a sessão do app aparece como "Dart/3.x (dart:io)".
        if (!kIsWeb)
          'User-Agent': 'SMSMais-AppCidadao (${defaultTargetPlatform == TargetPlatform.iOS ? 'iOS' : 'Android'})',
      },
    ),
  );
  // ignore: avoid_redundant_argument_values — `demoLogado` só é false no build sem a flag.
  if (modoDemo) dio.httpClientAdapter = DemoAdapter(jaConsentiu: demoLogado);

  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (opcoes, handler) {
        final token = ref.read(sessaoProvider)?.token;
        if (token != null) opcoes.headers['Authorization'] = 'Bearer $token';
        handler.next(opcoes);
      },
      onError: (erro, handler) {
        if (erro.response?.statusCode == 401) {
          // 401 nos endpoints de login (OTP) significa "código inválido" — NÃO deslogar. Em
          // qualquer outro endpoint autenticado, 401 = sessão expirada/revogada (inclusive
          // /consentimento): limpa a sessão e o roteador leva ao login.
          final ehLogin = erro.requestOptions.path.contains('-otp');
          if (!ehLogin && ref.read(sessaoProvider) != null) {
            ref.read(sessaoProvider.notifier).sair();
          }
        }
        handler.next(erro);
      },
    ),
  );

  if (kDebugMode && !modoDemo) {
    dio.interceptors.add(LogInterceptor(requestBody: true, responseHeader: false));
  }
  return dio;
});

/// Status HTTP + código de negócio (o `type` do ProblemDetails, ex.:
/// "confirmacao.ja_respondida"). Serve para a tela escolher um texto AMIGÁVEL — nunca exibimos
/// ao cidadão a mensagem crua de infraestrutura.
({int? status, String? codigo}) classificarErro(Object erro) {
  if (erro is! DioException) return (status: null, codigo: null);
  final dados = erro.response?.data;
  return (
    status: erro.response?.statusCode,
    codigo: dados is Map ? dados['type'] as String? : null,
  );
}

/// Sem resposta do servidor (sem internet, DNS, tempo esgotado).
bool semConexao(Object erro) =>
    erro is DioException &&
    erro.response == null &&
    erro.type != DioExceptionType.cancel &&
    erro.type != DioExceptionType.badCertificate;

/// Mensagem do ProblemDetails (erros de validação → detail → title), como o PWA.
String extrairMensagemDeErro(Object erro) {
  if (erro is DioException) {
    final dados = erro.response?.data;
    if (dados is Map) {
      final erros = dados['errors'];
      if (erros is Map) {
        final msgs = erros.values
            .expand((v) => v is List ? v : [v])
            .whereType<String>()
            .where((s) => s.isNotEmpty)
            .toList();
        if (msgs.isNotEmpty) return msgs.join(' ');
      }
      final detail = dados['detail'];
      if (detail is String && detail.isNotEmpty) return detail;
      final title = dados['title'];
      if (title is String && title.isNotEmpty) return title;
    }
    if (semConexao(erro)) return 'Sem conexão com a internet. Confira o sinal e tente de novo.';
    return 'Não foi possível concluir agora (erro ${erro.response?.statusCode ?? '-'}). Tente de novo.';
  }
  if (erro is Exception) return erro.toString().replaceFirst('Exception: ', '');
  return 'Erro desconhecido.';
}
