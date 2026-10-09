import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/app/router.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/push/destinos.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';

// Configuração do Firebase: `--dart-define-from-file=firebase.json` (fora do git; o modelo é o
// `firebase.exemplo.json`). São identificadores públicos do projeto — o segredo (conta de serviço)
// só existe no servidor. Sem `google-services.json`/`GoogleService-Info.plist`: tudo pelo Dart.
const _projeto = String.fromEnvironment('FIREBASE_PROJECT_ID');
const _remetente = String.fromEnvironment('FIREBASE_SENDER_ID');
const _androidApiKey = String.fromEnvironment('FIREBASE_ANDROID_API_KEY');
const _androidAppId = String.fromEnvironment('FIREBASE_ANDROID_APP_ID');
const _iosApiKey = String.fromEnvironment('FIREBASE_IOS_API_KEY');
const _iosAppId = String.fromEnvironment('FIREBASE_IOS_APP_ID');
const _iosBundleId = String.fromEnvironment('FIREBASE_IOS_BUNDLE_ID');

FirebaseOptions? _opcoesFirebase() {
  if (kIsWeb || modoDemo || _projeto.isEmpty || _remetente.isEmpty) return null;
  return switch (defaultTargetPlatform) {
    TargetPlatform.android when _androidApiKey.isNotEmpty && _androidAppId.isNotEmpty => const FirebaseOptions(
        apiKey: _androidApiKey,
        appId: _androidAppId,
        messagingSenderId: _remetente,
        projectId: _projeto,
      ),
    TargetPlatform.iOS when _iosApiKey.isNotEmpty && _iosAppId.isNotEmpty => FirebaseOptions(
        apiKey: _iosApiKey,
        appId: _iosAppId,
        messagingSenderId: _remetente,
        projectId: _projeto,
        iosBundleId: _iosBundleId.isEmpty ? null : _iosBundleId,
      ),
    _ => null,
  };
}

/// Sem a configuração da plataforma (e sempre no modo demonstração e na web) o push fica
/// desligado e o app funciona igual — só não recebe notificações.
bool get pushConfigurado => _opcoesFirebase() != null;

String get _plataforma => defaultTargetPlatform == TargetPlatform.iOS ? 'ios' : 'android';

/// O mesmo `channel_id` que o servidor manda na mensagem e o manifest declara como padrão.
const _canal = AndroidNotificationChannel(
  'avisos',
  'Avisos',
  description: 'Mensagens da equipe de saúde.',
  importance: Importance.high,
);

final _locais = FlutterLocalNotificationsPlugin();

/// Toque numa notificação local com o app vivo. O plugin é iniciado no `main`, antes de existir o
/// [pushProvider] — o toque passa por aqui até ele.
final _toquesLocais = StreamController<String?>.broadcast();

Future<bool>? _iniciado;

/// Inicia o Firebase e as notificações locais (uma vez por processo). O `main` não espera: o
/// Firebase não segura o primeiro quadro; quem precisa dele espera este mesmo Future.
Future<bool> iniciarPush() => _iniciado ??= _iniciar();

Future<bool> _iniciar() async {
  final opcoes = _opcoesFirebase();
  if (opcoes == null) return false;
  try {
    await Firebase.initializeApp(options: opcoes);
    if (defaultTargetPlatform == TargetPlatform.android) {
      await _locais.initialize(
        settings: const InitializationSettings(android: AndroidInitializationSettings('ic_stat_notificacao')),
        onDidReceiveNotificationResponse: (r) => _toquesLocais.add(r.payload),
      );
      await _locais
          .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
          ?.createNotificationChannel(_canal);
    } else {
      // No iOS o próprio sistema mostra o aviso com o app aberto — sem notificação local (duplicaria).
      await FirebaseMessaging.instance.setForegroundNotificationPresentationOptions(
        alert: true,
        badge: true,
        sound: true,
      );
    }
    return true;
  } on Object catch (e) {
    _logFalha(e);
    return false;
  }
}

/// Logout: o Firebase esquece este aparelho (o celular pode passar a outra pessoa). O servidor já
/// apagou o token junto com a sessão; isto é só a outra ponta.
Future<void> apagarTokenDoPush() async {
  if (!pushConfigurado) return;
  try {
    if (!await iniciarPush()) return;
    await FirebaseMessaging.instance.deleteToken().timeout(const Duration(seconds: 5));
  } on Object catch (e) {
    _logFalha(e);
  }
}

/// O que o pedido de registro faz agora (ver [MarcaDoRegistro.proximo]).
enum PassoDoRegistro { registrar, conferirPermissao, nada }

/// Marca do registro do aparelho, por sessão. O AppShell pede o registro ao montar e a cada volta
/// ao primeiro plano: a marca faz disso um registro só por sessão, libera nova tentativa quando ele
/// falha (API fora, sem rede) e nunca repete o pedido de permissão a quem negou — no Android 13+ o
/// plugin pede ao sistema sempre que não está liberada (depois da 1ª recusa o diálogo reabre), e
/// cada diálogo fechado é uma volta ao primeiro plano: seria um laço.
@visibleForTesting
class MarcaDoRegistro {
  String? _sessao;
  bool _negou = false;

  /// Sessão nova (ou registro que falhou): registrar. Negou: só ler a permissão, sem diálogo — ele
  /// pode ter liberado nas configurações do celular. Já registrado ou em andamento: nada.
  PassoDoRegistro proximo(String sessao) {
    if (_sessao != sessao) {
      _sessao = sessao;
      _negou = false;
      return PassoDoRegistro.registrar;
    }
    if (_negou) {
      _negou = false; // duas voltas seguidas não conferem em dobro; [negou] devolve a marca
      return PassoDoRegistro.conferirPermissao;
    }
    return PassoDoRegistro.nada;
  }

  void falhou(String sessao) {
    if (_sessao == sessao) _sessao = null;
  }

  void negou(String sessao) {
    if (_sessao == sessao) _negou = true;
  }
}

/// Registro do aparelho na sessão e toques nas notificações. Tudo best-effort: falha de push nunca
/// derruba o app nem aparece para o cidadão.
class PushCidadao {
  PushCidadao(this._ref);

  final Ref _ref;
  final _assinaturas = <StreamSubscription<Object?>>[];
  StreamSubscription<String>? _renovacao;
  final _marca = MarcaDoRegistro();
  bool _tratouAbertura = false;

  /// Chamado quando o AppShell monta (depois do login e do termo aceito) e a cada volta do app ao
  /// primeiro plano. Registra uma vez por sessão; a volta só refaz o que falhou (ver [MarcaDoRegistro]).
  void registrarAparelho() {
    final sessao = _ref.read(sessaoProvider)?.token;
    if (!pushConfigurado || sessao == null) return;
    switch (_marca.proximo(sessao)) {
      case PassoDoRegistro.registrar:
        _executar(sessao).ignore();
      case PassoDoRegistro.conferirPermissao:
        _conferirPermissao(sessao).ignore();
      case PassoDoRegistro.nada:
        break;
    }
  }

  Future<void> _executar(String sessao) async {
    // Firebase que não subiu não sobe neste processo (o Future fica guardado): sem nova tentativa.
    if (!await iniciarPush()) return;
    _escutar();
    await _abrirPeloToqueDaAbertura();
    await _registrar(sessao);
  }

  void _escutar() {
    if (_assinaturas.isNotEmpty) return;
    _assinaturas.addAll([
      FirebaseMessaging.onMessage.listen((m) => _mostrarComAppAberto(m).ignore(), onError: _logFalha),
      FirebaseMessaging.onMessageOpenedApp.listen((m) => _abrir(m.data['rota']), onError: _logFalha),
      _toquesLocais.stream.listen(_abrir),
    ]);
  }

  /// App aberto do zero por um toque: na notificação do Firebase (app estava fechado) ou numa
  /// local (mostrada com o app aberto, que depois foi fechado — o Firebase não a conhece).
  Future<void> _abrirPeloToqueDaAbertura() async {
    if (_tratouAbertura) return;
    _tratouAbertura = true;
    try {
      final inicial = await FirebaseMessaging.instance.getInitialMessage();
      if (inicial != null) {
        _abrir(inicial.data['rota']);
        return;
      }
      if (defaultTargetPlatform != TargetPlatform.android) return;
      final local = await _locais.getNotificationAppLaunchDetails();
      if (local != null && local.didNotificationLaunchApp) _abrir(local.notificationResponse?.payload);
    } on Object catch (e) {
      _logFalha(e);
    }
  }

  Future<void> _registrar(String sessao) async {
    final fcm = FirebaseMessaging.instance;
    try {
      if (!_liberada((await fcm.requestPermission()).authorizationStatus)) {
        // Negou — ou desligou depois, nas configurações, com o token já gravado na sessão: o
        // Firebase esquece o token, o próximo envio volta "desinstalado" e o servidor tira o
        // aparelho da sessão. Sem isso o painel mostraria "entregue" para um aviso que não aparece.
        // A marca fica (não é falha): a volta ao primeiro plano não pede a permissão de novo.
        _marca.negou(sessao);
        await _esquecerToken(fcm);
        return;
      }
      // Sem o token da Apple, tenta de novo na volta ao primeiro plano (já autorizado: sem diálogo).
      if (defaultTargetPlatform == TargetPlatform.iOS && !await _esperarApns(fcm)) {
        _marca.falhou(sessao);
        return;
      }
      final token = await fcm.getToken();
      if (token != null) await _enviar(token);
      _renovacao ??= fcm.onTokenRefresh.listen((t) => _enviar(t).ignore(), onError: _logFalha);
    } on Object catch (e) {
      _marca.falhou(sessao);
      _logFalha(e);
    }
  }

  /// Quem negou e volta ao app: lê a permissão (nunca abre o diálogo). Se ele liberou nas
  /// configurações do celular, registra agora — sem esperar a próxima abertura.
  Future<void> _conferirPermissao(String sessao) async {
    try {
      final permissao = (await FirebaseMessaging.instance.getNotificationSettings()).authorizationStatus;
      if (_liberada(permissao)) {
        await _registrar(sessao); // já autorizado: o requestPermission responde sem diálogo
        return;
      }
    } on Object catch (e) {
      _logFalha(e);
    }
    _marca.negou(sessao);
  }

  bool _liberada(AuthorizationStatus permissao) =>
      permissao == AuthorizationStatus.authorized || permissao == AuthorizationStatus.provisional;

  Future<void> _esquecerToken(FirebaseMessaging fcm) async {
    try {
      await fcm.deleteToken().timeout(const Duration(seconds: 5));
    } on Object catch (e) {
      _logFalha(e); // sem rede: fica para a próxima abertura
    }
  }

  /// No iOS o token do Firebase depende do token da Apple (APNs), que chega pouco depois da
  /// permissão — e nunca chega no simulador.
  Future<bool> _esperarApns(FirebaseMessaging fcm) async {
    for (var tentativa = 0; tentativa < 5; tentativa++) {
      if (await fcm.getAPNSToken() != null) return true;
      await Future<void>.delayed(const Duration(seconds: 2));
    }
    return false;
  }

  Future<void> _enviar(String token) async {
    final sessao = _ref.read(sessaoProvider)?.token;
    if (sessao == null) return; // saiu: o token vai na próxima entrada
    try {
      await _ref.read(apiProvider).registrarDispositivo(token, _plataforma);
    } on Object catch (e) {
      // API fora ou sem rede: libera a marca — tenta de novo na volta ao primeiro plano.
      _marca.falhou(sessao);
      _logFalha(e);
    }
  }

  /// Com o app aberto o Firebase não mostra nada: no Android vira notificação local no mesmo canal
  /// (o toque traz a rota no payload); no iOS o sistema já mostra.
  Future<void> _mostrarComAppAberto(RemoteMessage m) async {
    final n = m.notification;
    if (n == null || defaultTargetPlatform != TargetPlatform.android) return;
    try {
      await _locais.show(
        id: (m.data['notificacaoId'] ?? m.messageId ?? '').hashCode & 0x7fffffff,
        title: n.title,
        body: n.body,
        notificationDetails: NotificationDetails(
          android: AndroidNotificationDetails(
            _canal.id,
            _canal.name,
            channelDescription: _canal.description,
            importance: Importance.high,
            priority: Priority.high,
            color: CoresMarica.marica,
          ),
        ),
        payload: _texto(m.data['rota']),
      );
    } on Object catch (e) {
      _logFalha(e);
    }
  }

  void _abrir(Object? rota) {
    final destino = destinoDoToque(_texto(rota));
    if (destino == null || _ref.read(sessaoProvider) == null) return;
    _ref.read(routerProvider).go(destino);
  }

  void _encerrar() {
    for (final a in _assinaturas) {
      a.cancel().ignore();
    }
    _renovacao?.cancel().ignore();
  }
}

String? _texto(Object? valor) => valor is String ? valor : null;

void _logFalha(Object erro) {
  if (kDebugMode) debugPrint('push: $erro');
}

final pushProvider = Provider<PushCidadao>((ref) {
  final push = PushCidadao(ref);
  ref.onDispose(push._encerrar);
  return push;
});
