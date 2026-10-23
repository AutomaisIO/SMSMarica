import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sms_mais_cidadao/app/app.dart';
import 'package:sms_mais_cidadao/shared/api/demo_adapter.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/localizacao/gps.dart';
import 'package:sms_mais_cidadao/shared/localizacao/localizacao.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// GPS de mentira: responde a permissão combinada e solta as posições que o teste mandar.
class _GpsFalso implements FonteGps {
  AcessoGps acesso = AcessoGps.liberado;
  int pedidos = 0;
  final configuracoes = <AcessoGps>[];
  final _posicoes = StreamController<LeituraGps>.broadcast();

  /// Alguém está ouvindo = o GPS está ligado.
  bool get ligado => _posicoes.hasListener;

  @override
  Future<AcessoGps> conferirAcesso() async => acesso;

  @override
  Future<AcessoGps> pedirAcesso() async {
    pedidos++;
    return acesso;
  }

  @override
  Stream<LeituraGps> posicoes() => _posicoes.stream;

  @override
  Future<void> abrirConfiguracoes(AcessoGps motivo) async => configuracoes.add(motivo);

  void emitir({double latitude = -22.9194, double longitude = -42.8186}) => _posicoes.add((
        latitude: latitude,
        longitude: longitude,
        precisaoM: 8,
        capturadaEm: DateTime.now().toUtc(),
        simulada: false,
      ));
}

/// O "servidor" da demonstração, anotando cada pedido; o teste pode forçar uma resposta.
class _Servidor implements HttpClientAdapter {
  final _demo = DemoAdapter(atraso: Duration.zero, jaConsentiu: true);
  final pedidos = <String>[];

  /// Respostas forçadas por "MÉTODO /caminho".
  final forcar = <String, (int, Object?)>{};

  int contar(String pedido) => pedidos.where((p) => p == pedido).length;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) {
    final chave = '${options.method.toUpperCase()} ${options.uri.path}';
    pedidos.add(chave);
    final forcada = forcar[chave];
    if (forcada == null) return _demo.fetch(options, requestStream, cancelFuture);
    final (status, dados) = forcada;
    return Future.value(
      ResponseBody.fromString(
        dados == null ? '' : jsonEncode(dados),
        status,
        headers: {
          if (dados != null) Headers.contentTypeHeader: ['application/problem+json'],
        },
      ),
    );
  }

  @override
  void close({bool force = false}) {}
}

/// A plataforma do geolocator, falsa: anota cada pedido de posições e devolve um fluxo controlável.
class _PlataformaFalsa extends GeolocatorPlatform {
  bool servicoLigado = true;
  final pedidos = <LocationSettings?>[];
  final fluxos = <StreamController<Position>>[];

  @override
  Future<bool> isLocationServiceEnabled() async => servicoLigado;

  @override
  Stream<Position> getPositionStream({LocationSettings? locationSettings}) {
    pedidos.add(locationSettings);
    final fluxo = StreamController<Position>();
    fluxos.add(fluxo);
    return fluxo.stream;
  }
}

Position _posicao() => Position(
      latitude: -22.9194,
      longitude: -42.8186,
      timestamp: DateTime.now().toUtc(),
      accuracy: 8,
      altitude: 0,
      altitudeAccuracy: 0,
      heading: 0,
      headingAccuracy: 0,
      speed: 0,
      speedAccuracy: 0,
    );

const _sessao = SessaoCidadao(
  token: 'demo',
  paciente: PacienteSessao(id: 'p1', nome: 'MARIA DA CONCEIÇÃO EXEMPLO', cpf: '52998224725'),
);

Dio _dio(_Servidor servidor) => Dio(BaseOptions(baseUrl: 'https://demo'))..httpClientAdapter = servidor;

LeituraGps _leitura(double latitude, double longitude) =>
    (latitude: latitude, longitude: longitude, precisaoM: 8, capturadaEm: DateTime.now(), simulada: false);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('ritmo dos envios (o mesmo do PWA)', () {
    final t0 = DateTime(2026, 10, 23, 10);
    final aceita = (latitude: -22.9194, longitude: -42.8186, em: t0);

    test('a primeira posição vai', () {
      expect(deveEnviar(leitura: _leitura(-22.9194, -42.8186), agora: t0), isTrue);
    });

    test('nunca antes de 10 s da última tentativa, nem se andou muito', () {
      final longe = _leitura(-22.9300, -42.8186);
      expect(
        deveEnviar(leitura: longe, agora: t0.add(const Duration(seconds: 9)), ultimaAceita: aceita, ultimaTentativa: t0),
        isFalse,
      );
    });

    test('parado: só depois de 15 s; andou 20 m: já aos 10 s', () {
      final mesmoLugar = _leitura(-22.9194, -42.8186);
      // ~25 m ao norte.
      final andou = _leitura(-22.91918, -42.8186);
      final aos12 = t0.add(const Duration(seconds: 12));
      expect(deveEnviar(leitura: mesmoLugar, agora: aos12, ultimaAceita: aceita, ultimaTentativa: t0), isFalse);
      expect(
        deveEnviar(
          leitura: mesmoLugar,
          agora: t0.add(const Duration(seconds: 15)),
          ultimaAceita: aceita,
          ultimaTentativa: t0,
        ),
        isTrue,
      );
      expect(deveEnviar(leitura: andou, agora: aos12, ultimaAceita: aceita, ultimaTentativa: t0), isTrue);
    });

    test('distância: 1 grau de latitude dá ~111 km', () {
      expect(distanciaM(-23, -43, -22, -43), closeTo(111195, 50));
      expect(distanciaM(-22.9194, -42.8186, -22.91918, -42.8186), closeTo(24.5, 1));
    });
  });

  group('GPS do aparelho no Android (achados do teste no emulador)', () {
    late _PlataformaFalsa plataforma;

    setUp(() {
      debugDefaultTargetPlatformOverride = TargetPlatform.android;
      GpsDoAparelho.esquecerRecusaDoGoogle();
      plataforma = _PlataformaFalsa();
      GeolocatorPlatform.instance = plataforma;
    });

    tearDown(() => debugDefaultTargetPlatformOverride = null);

    test('recusou a "Precisão do local" do Google: segue pelo Android, sem perguntar de novo', () async {
      final recebidas = <LeituraGps>[];
      final assinatura = const GpsDoAparelho().posicoes().listen(recebidas.add);
      expect((plataforma.pedidos.single! as AndroidSettings).forceLocationManager, isFalse);

      plataforma.fluxos.first.addError(const LocationServiceDisabledException());
      await pumpEventQueue();
      expect(plataforma.pedidos, hasLength(2));
      expect((plataforma.pedidos.last! as AndroidSettings).forceLocationManager, isTrue);

      plataforma.fluxos.last.add(_posicao());
      await pumpEventQueue();
      expect(recebidas.single.latitude, -22.9194);

      // A próxima ligação do GPS (outra consulta, volta ao app) já vai direto pelo Android.
      await assinatura.cancel();
      final outra = const GpsDoAparelho().posicoes().listen((_) {});
      expect((plataforma.pedidos.last! as AndroidSettings).forceLocationManager, isTrue);
      await outra.cancel();
    });

    test('localização do aparelho desligada: o erro chega ao controle (sem trocar de caminho)', () async {
      plataforma.servicoLigado = false;
      Object? erro;
      final assinatura = const GpsDoAparelho().posicoes().listen((_) {}, onError: (Object e) => erro = e);
      plataforma.fluxos.first.addError(const LocationServiceDisabledException());
      await pumpEventQueue();
      expect(erro, isA<LocationServiceDisabledException>());
      expect(plataforma.pedidos, hasLength(1));
      await assinatura.cancel();
    });
  });

  group('LocalizacaoController', () {
    late _GpsFalso gps;
    late _Servidor servidor;
    late ProviderContainer c;

    setUp(() {
      gps = _GpsFalso();
      servidor = _Servidor();
      c = ProviderContainer(
        overrides: [
          sessaoInicialProvider.overrideWithValue(_sessao),
          dioProvider.overrideWithValue(_dio(servidor)),
          fonteGpsProvider.overrideWithValue(gps),
        ],
      );
      // Mantém o controle vivo, como a faixa no app.
      c.listen(localizacaoProvider, (_, __) {});
    });

    tearDown(() => c.dispose());

    LocalizacaoController controle() => c.read(localizacaoProvider.notifier);
    EstadoLocalizacao estado() => c.read(localizacaoProvider);

    test('compartilhar pede a permissão, liga o GPS e manda a posição; Parar desliga e apaga', () async {
      await controle().atualizar();
      expect(estado().espera?.sessaoId, 'sr1');
      expect(estado().compartilhando, isFalse);
      expect(gps.ligado, isFalse);

      await controle().compartilhar();
      expect(gps.pedidos, 1);
      expect(estado().compartilhando, isTrue);
      expect(estado().situacao, SituacaoGps.enviando);
      expect(gps.ligado, isTrue);

      gps.emitir();
      await pumpEventQueue();
      expect(servidor.contar('PUT /auth/paciente/localizacao/sr1'), 1);
      expect(estado().ultimoEnvio, isNotNull);

      // Logo em seguida, outra leitura no mesmo lugar: fica para depois (ritmo).
      gps.emitir();
      await pumpEventQueue();
      expect(servidor.contar('PUT /auth/paciente/localizacao/sr1'), 1);

      await controle().parar();
      expect(gps.ligado, isFalse);
      expect(estado().compartilhando, isFalse);
      expect(estado().espera, isNotNull, reason: 'a espera continua: o convite volta');
      expect(servidor.contar('DELETE /auth/paciente/localizacao/sr1'), 1);
    });

    test('servidor sem a função (404): esconde e não consulta mais', () async {
      servidor.forcar['GET /auth/paciente/localizacao'] = (404, null);
      await controle().atualizar();
      await controle().atualizar();
      expect(estado().desligado, isTrue);
      expect(estado().espera, isNull);
      expect(servidor.contar('GET /auth/paciente/localizacao'), 1);
    });

    test('rede fora: fica como estava e tenta de novo na próxima consulta', () async {
      servidor.forcar['GET /auth/paciente/localizacao'] = (503, {'title': 'Indisponível', 'status': 503});
      await controle().atualizar();
      expect(estado().desligado, isFalse);
      servidor.forcar.clear();
      await controle().atualizar();
      expect(estado().espera, isNotNull);
    });

    test('embarcou (409 no envio): desliga o GPS e a espera acaba', () async {
      await controle().atualizar();
      await controle().compartilhar();
      servidor.forcar['PUT /auth/paciente/localizacao/sr1'] =
          (409, {'title': 'Fora da janela', 'status': 409, 'type': 'localizacao.fora_da_janela'});
      servidor.forcar['GET /auth/paciente/localizacao'] = (204, null);

      gps.emitir();
      await pumpEventQueue();
      expect(gps.ligado, isFalse);
      expect(estado().espera, isNull);
      expect(estado().compartilhando, isFalse);
    });

    test('permissão negada: não liga o GPS; "Permitir" pergunta de novo', () async {
      gps.acesso = AcessoGps.negado;
      await controle().atualizar();
      await controle().compartilhar();
      expect(estado().compartilhando, isTrue, reason: 'o aceite vale; falta o aparelho deixar');
      expect(estado().situacao, SituacaoGps.bloqueado);
      expect(estado().acesso, AcessoGps.negado);
      expect(gps.ligado, isFalse);

      gps.acesso = AcessoGps.liberado;
      await controle().permitir();
      expect(gps.pedidos, 2);
      expect(estado().situacao, SituacaoGps.enviando);
      expect(gps.ligado, isTrue);
    });

    test('negou de vez ou localização desligada: "Permitir" abre as configurações', () async {
      gps.acesso = AcessoGps.negadoDeVez;
      await controle().atualizar();
      await controle().compartilhar();
      await controle().permitir();
      expect(gps.configuracoes, [AcessoGps.negadoDeVez]);
      expect(gps.pedidos, 1, reason: 'não pergunta de novo: o aparelho não deixaria');
    });

    test('aceite pelo Sandbox ou pelo link do WhatsApp: mensagem própria, GPS desligado', () async {
      servidor.forcar['POST /auth/paciente/localizacao/sr1'] =
          (409, {'title': 'x', 'status': 409, 'type': 'localizacao.sessao_por_link'});
      await controle().atualizar();
      await controle().compartilhar();
      expect(estado().erro, contains('código enviado por WhatsApp'));
      expect(estado().compartilhando, isFalse);
      expect(gps.pedidos, 0);
      expect(gps.ligado, isFalse);
    });

    test('app saiu da tela: pausar desliga o GPS; a volta liga de novo sem perguntar', () async {
      await controle().atualizar();
      await controle().compartilhar();
      controle().pausar();
      expect(gps.ligado, isFalse);
      expect(estado().compartilhando, isTrue);

      await controle().atualizar();
      expect(gps.ligado, isTrue);
      expect(gps.pedidos, 1, reason: 'a volta só confere a permissão');
    });

    test('sair da conta desliga o GPS e zera o estado', () async {
      await controle().atualizar();
      await controle().compartilhar();
      expect(gps.ligado, isTrue);

      await c.read(sessaoProvider.notifier).sair();
      expect(gps.ligado, isFalse);
      expect(estado().espera, isNull);
    });
  });

  group('telas', () {
    setUpAll(() async {
      await initializeDateFormatting('pt_BR');
    });

    setUp(() {
      SharedPreferences.setMockInitialValues({});
    });

    Widget app(_GpsFalso gps, _Servidor servidor) => ProviderScope(
          overrides: [
            sessaoInicialProvider.overrideWithValue(_sessao),
            dioProvider.overrideWithValue(_dio(servidor)),
            fonteGpsProvider.overrideWithValue(gps),
          ],
          child: const AppCidadaoMarica(),
        );

    void celular(WidgetTester tester) {
      tester.view
        ..physicalSize = const Size(1080, 2340)
        ..devicePixelRatio = 3;
      addTearDown(tester.view.reset);
    }

    Future<void> assentar(WidgetTester tester) async {
      for (var i = 0; i < 20; i++) {
        await tester.pump(const Duration(milliseconds: 100));
      }
    }

    Future<void> tocar(WidgetTester tester, Finder alvo) async {
      await tester.ensureVisible(alvo);
      await tester.pump();
      await tester.tap(alvo);
    }

    testWidgets('Início: convite, explicação antes da permissão, faixa com o envio e Parar', (tester) async {
      celular(tester);
      final gps = _GpsFalso();
      final servidor = _Servidor();
      await tester.pumpWidget(app(gps, servidor));
      await assentar(tester);

      expect(find.text('CARTÃO DO CIDADÃO'), findsOneWidget);
      expect(find.text('Ajude o motorista a achar você'), findsOneWidget);
      expect(find.text('O carro vai buscar você em Hospital Demonstração.'), findsOneWidget);

      await tocar(tester, find.text('Compartilhar minha localização'));
      await tester.pump();
      expect(find.text('Só o motorista do carro que vai buscar você vê onde você está.'), findsOneWidget);
      expect(gps.pedidos, 0, reason: 'o aparelho só pergunta depois do Compartilhar');

      await tocar(tester, find.text('Compartilhar'));
      await assentar(tester);
      expect(gps.pedidos, 1);
      expect(find.text('Compartilhando sua localização com o motorista'), findsOneWidget);
      expect(find.textContaining('Procurando a sua posição…'), findsOneWidget);
      expect(find.text('Ajude o motorista a achar você'), findsNothing);

      gps.emitir();
      await assentar(tester);
      expect(find.textContaining('Enviada agora há pouco.'), findsOneWidget);
      expect(servidor.contar('PUT /auth/paciente/localizacao/sr1'), 1);

      await tocar(tester, find.text('Parar'));
      await assentar(tester);
      expect(find.text('Compartilhando sua localização com o motorista'), findsNothing);
      expect(gps.ligado, isFalse);
      expect(servidor.contar('DELETE /auth/paciente/localizacao/sr1'), 1);
      expect(find.text('Ajude o motorista a achar você'), findsOneWidget);
    });

    testWidgets('servidor antigo (404): o Início fica como sempre e não consulta de novo', (tester) async {
      celular(tester);
      final gps = _GpsFalso();
      final servidor = _Servidor()..forcar['GET /auth/paciente/localizacao'] = (404, null);
      await tester.pumpWidget(app(gps, servidor));
      await assentar(tester);

      expect(find.text('CARTÃO DO CIDADÃO'), findsOneWidget);
      expect(find.text('Consultas'), findsOneWidget);
      expect(find.text('Ajude o motorista a achar você'), findsNothing);
      expect(find.text('Compartilhando sua localização com o motorista'), findsNothing);

      // A consulta de minuto em minuto não volta a perguntar.
      await tester.pump(const Duration(minutes: 1));
      await assentar(tester);
      expect(servidor.contar('GET /auth/paciente/localizacao'), 1);
    });

    testWidgets('permissão negada: a faixa explica e oferece Permitir', (tester) async {
      celular(tester);
      final gps = _GpsFalso()..acesso = AcessoGps.negado;
      final servidor = _Servidor();
      await tester.pumpWidget(app(gps, servidor));
      await assentar(tester);

      await tocar(tester, find.text('Compartilhar minha localização'));
      await tester.pump();
      await tocar(tester, find.text('Compartilhar'));
      await assentar(tester);

      expect(find.textContaining('O celular não deixou usar a localização.'), findsOneWidget);
      expect(find.text('Permitir'), findsOneWidget);
      expect(gps.ligado, isFalse);
    });
  });
}
