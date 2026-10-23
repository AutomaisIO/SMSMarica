import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/localizacao/gps.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// O app manda no máximo a cada 15 s, ou antes disso se andou 20 m, nunca antes de 10 s (o servidor
/// aceita um envio a cada 10 s, com folga). O mesmo ritmo do PWA.
const intervaloEnvio = Duration(seconds: 15);
const intervaloMinimoEnvio = Duration(seconds: 10);
const distanciaEnvioM = 20.0;

/// Em que pé está o GPS do compartilhamento.
enum SituacaoGps {
  /// Desligado: sem compartilhar, ou o app saiu da tela.
  parado,

  /// Esperando o aparelho responder sobre a permissão.
  pedindoPermissao,

  /// Ligado, mandando a posição.
  enviando,

  /// O aparelho não deixou usar a localização (o motivo está em [EstadoLocalizacao.acesso]).
  bloqueado,
}

const _manter = Object();

@immutable
class EstadoLocalizacao {
  const EstadoLocalizacao({
    this.espera,
    this.situacao = SituacaoGps.parado,
    this.acesso,
    this.ultimoEnvio,
    this.erro,
    this.iniciando = false,
    this.desligado = false,
  });

  /// O embarque que o paciente espera agora; nulo fora da espera.
  final LocalizacaoViagem? espera;
  final SituacaoGps situacao;

  /// A última resposta do aparelho sobre a permissão.
  final AcessoGps? acesso;

  /// Quando a última posição foi aceita pelo servidor.
  final DateTime? ultimoEnvio;

  /// Texto amigável do último "Compartilhar" que falhou.
  final String? erro;

  /// O "Compartilhar" está em andamento.
  final bool iniciando;

  /// O servidor não tem a função (404): não consulta mais nesta sessão do app.
  final bool desligado;

  bool get compartilhando => espera?.compartilhando ?? false;

  EstadoLocalizacao copiar({
    Object? espera = _manter,
    SituacaoGps? situacao,
    Object? acesso = _manter,
    Object? ultimoEnvio = _manter,
    Object? erro = _manter,
    bool? iniciando,
    bool? desligado,
  }) =>
      EstadoLocalizacao(
        espera: identical(espera, _manter) ? this.espera : espera as LocalizacaoViagem?,
        situacao: situacao ?? this.situacao,
        acesso: identical(acesso, _manter) ? this.acesso : acesso as AcessoGps?,
        ultimoEnvio: identical(ultimoEnvio, _manter) ? this.ultimoEnvio : ultimoEnvio as DateTime?,
        erro: identical(erro, _manter) ? this.erro : erro as String?,
        iniciando: iniciando ?? this.iniciando,
        desligado: desligado ?? this.desligado,
      );
}

/// Ponto aceito pelo servidor (para o ritmo dos envios).
typedef PontoAceito = ({double latitude, double longitude, DateTime em});

/// Distância em metros entre dois pontos (haversine).
double distanciaM(double lat1, double lng1, double lat2, double lng2) {
  const r = 6371000.0;
  double rad(double g) => g * math.pi / 180;
  final dLat = rad(lat2 - lat1);
  final dLng = rad(lng2 - lng1);
  final h = math.pow(math.sin(dLat / 2), 2) +
      math.cos(rad(lat1)) * math.cos(rad(lat2)) * math.pow(math.sin(dLng / 2), 2);
  return 2 * r * math.asin(math.sqrt(h));
}

/// A leitura nova vai para o servidor? Nunca antes de 10 s da última tentativa; depois, se já
/// passaram 15 s da última aceita ou se o paciente andou 20 m.
bool deveEnviar({
  required LeituraGps leitura,
  required DateTime agora,
  PontoAceito? ultimaAceita,
  DateTime? ultimaTentativa,
}) {
  if (ultimaTentativa != null && agora.difference(ultimaTentativa) < intervaloMinimoEnvio) return false;
  if (ultimaAceita == null) return true;
  return agora.difference(ultimaAceita.em) >= intervaloEnvio ||
      distanciaM(ultimaAceita.latitude, ultimaAceita.longitude, leitura.latitude, leitura.longitude) >=
          distanciaEnvioM;
}

/// Nunca a mensagem crua do servidor: um texto para cada caso conhecido.
String _mensagemDoErro(Object e) {
  final codigos = codigosDoErro(e);
  if (codigos.contains('localizacao.sessao_por_link')) {
    return 'Para compartilhar a sua localização, saia e entre no aplicativo com o código enviado por WhatsApp.';
  }
  if (codigos.contains('localizacao.personificacao')) {
    return 'Entrando como o paciente pelo Sandbox não é possível compartilhar a localização.';
  }
  if (codigos.contains('localizacao.fora_da_janela')) return 'Agora não há um embarque esperando por você.';
  if (semConexao(e)) return 'Sem conexão com a internet. Confira o sinal e tente de novo.';
  return 'Não foi possível começar agora. Tente de novo em instantes.';
}

/// O app está na tela (o pedido de permissão do aparelho deixa o app "inativo", não escondido).
bool _appNaTela() {
  final ciclo = WidgetsBinding.instance.lifecycleState;
  return ciclo == null || ciclo == AppLifecycleState.resumed || ciclo == AppLifecycleState.inactive;
}

/// Localização do paciente no dia da viagem (ADR-0082; `store/localizacao.ts` do PWA). O servidor
/// diz se ele espera um embarque agora (a ida ou o resgate) e se aceitou compartilhar; aqui fica o
/// GPS do aparelho, ligado só com o app na tela e só enquanto ele compartilha.
///
/// Tudo o que der errado só esconde a função: servidor sem os endpoints (404), sem GPS, permissão
/// negada, rede ou limite. O resto do app segue igual.
class LocalizacaoController extends Notifier<EstadoLocalizacao> {
  StreamSubscription<LeituraGps>? _assinatura;
  PontoAceito? _ultimaAceita;
  DateTime? _ultimaTentativa;
  bool _enviandoPosicao = false;
  bool _ligando = false;

  /// Muda a cada sessão: resposta que chega depois de sair (ou de entrar em outra conta) não vale.
  int _geracao = 0;

  @override
  EstadoLocalizacao build() {
    // Sessão nova, ou saída: começa do zero, com o GPS desligado.
    ref
      ..watch(sessaoProvider.select((s) => s?.token))
      ..onDispose(_desligarGps);
    _geracao++;
    _ultimaTentativa = null;
    _enviandoPosicao = false;
    _ligando = false;
    return const EstadoLocalizacao();
  }

  /// Consulta o servidor: há uma espera agora? Compartilhando, liga o GPS (com o app na tela).
  Future<void> atualizar() async {
    if (state.desligado) return;
    final geracao = _geracao;
    try {
      final espera = await ref.read(apiProvider).localizacao();
      if (geracao != _geracao) return;
      state = state.copiar(
        espera: espera,
        ultimoEnvio: state.ultimoEnvio ?? DateTime.tryParse(espera?.ultimoEnvioEm ?? ''),
      );
      if (espera?.compartilhando ?? false) {
        // Aceitou nesta viagem (talvez antes de fechar o app): o GPS volta com o app na tela.
        if (_appNaTela()) await _ligarGps(pedir: false);
      } else {
        _desligarGps();
        state = state.copiar(situacao: SituacaoGps.parado, ultimoEnvio: null);
      }
    } on Object catch (e) {
      if (geracao != _geracao) return;
      if (classificarErro(e).status == 404) {
        _desligarGps();
        state = state.copiar(desligado: true, espera: null, situacao: SituacaoGps.parado);
      }
      // Outros erros (rede, limite, termo ainda não aceito): fica como estava; a próxima consulta tenta.
    }
  }

  /// "Compartilhar": registra o aceite desta viagem e liga o GPS (o aparelho pede a permissão).
  Future<void> compartilhar() async {
    final espera = state.espera;
    if (espera == null || state.iniciando) return;
    final geracao = _geracao;
    state = state.copiar(iniciando: true, erro: null);
    try {
      final nova = await ref.read(apiProvider).iniciarLocalizacao(espera.sessaoId);
      if (geracao != _geracao) return;
      state = state.copiar(espera: nova);
      await _ligarGps(pedir: true);
    } on Object catch (e) {
      if (geracao != _geracao) return;
      state = state.copiar(erro: _mensagemDoErro(e));
      unawaited(atualizar());
    } finally {
      if (geracao == _geracao) state = state.copiar(iniciando: false);
    }
  }

  /// "Parar": desliga o GPS na hora e avisa o servidor, que apaga a posição.
  Future<void> parar() async {
    final espera = state.espera;
    _desligarGps();
    state = state.copiar(situacao: SituacaoGps.parado, ultimoEnvio: null, espera: espera?.semCompartilhar());
    if (espera == null || !espera.compartilhando) return;
    try {
      await ref.read(apiProvider).pararLocalizacao(espera.sessaoId);
    } on Object {
      // O servidor encerra sozinho quando a espera acaba.
    }
  }

  /// O app saiu da tela: desliga o GPS. O aceite continua e o GPS volta quando o app reaparece.
  void pausar() {
    _desligarGps();
    if (state.situacao == SituacaoGps.enviando || state.situacao == SituacaoGps.pedindoPermissao) {
      state = state.copiar(situacao: SituacaoGps.parado);
    }
  }

  /// O "Permitir" da faixa: pergunta de novo (negou uma vez) ou abre as configurações do aparelho.
  Future<void> permitir() async {
    final acesso = state.acesso;
    if (acesso == null) return;
    if (acesso == AcessoGps.negado) {
      await _ligarGps(pedir: true);
    } else {
      await ref.read(fonteGpsProvider).abrirConfiguracoes(acesso);
    }
  }

  /// Liga o GPS, se o aparelho deixar. [pedir]: pode mostrar o pedido de permissão do aparelho.
  Future<void> _ligarGps({required bool pedir}) async {
    if (_assinatura != null || _ligando) return;
    _ligando = true;
    final geracao = _geracao;
    // Só o pedido mostra "permita no aviso"; a conferência silenciosa não muda a faixa.
    if (pedir) state = state.copiar(situacao: SituacaoGps.pedindoPermissao);
    try {
      final gps = ref.read(fonteGpsProvider);
      final acesso = pedir ? await gps.pedirAcesso() : await gps.conferirAcesso();
      if (geracao != _geracao) return;
      // Parou (ou a espera acabou) enquanto o aparelho respondia: não liga.
      if (!state.compartilhando) {
        state = state.copiar(situacao: SituacaoGps.parado);
        return;
      }
      if (!acesso.podeUsar) {
        state = state.copiar(situacao: SituacaoGps.bloqueado, acesso: acesso);
        return;
      }
      state = state.copiar(situacao: SituacaoGps.enviando, acesso: acesso);
      _assinatura = gps.posicoes().listen(
        (leitura) => unawaited(_enviar(leitura)),
        onError: (Object _) {
          // Permissão retirada ou localização desligada no meio do caminho.
          _desligarGps();
          unawaited(_reavaliarAcesso());
        },
      );
    } finally {
      if (geracao == _geracao) _ligando = false;
    }
  }

  Future<void> _reavaliarAcesso() async {
    final geracao = _geracao;
    final acesso = await ref.read(fonteGpsProvider).conferirAcesso();
    if (geracao != _geracao || !state.compartilhando) return;
    state = acesso.podeUsar
        // Falha passageira: a próxima consulta (ou a volta ao app) liga de novo.
        ? state.copiar(situacao: SituacaoGps.parado)
        : state.copiar(situacao: SituacaoGps.bloqueado, acesso: acesso);
  }

  Future<void> _enviar(LeituraGps leitura) async {
    final espera = state.espera;
    if (espera == null || !espera.compartilhando || _enviandoPosicao) return;
    if (ref.read(sessaoProvider) == null) {
      _desligarGps();
      return;
    }
    final agora = DateTime.now();
    if (!deveEnviar(leitura: leitura, agora: agora, ultimaAceita: _ultimaAceita, ultimaTentativa: _ultimaTentativa)) {
      return;
    }

    final geracao = _geracao;
    _enviandoPosicao = true;
    _ultimaTentativa = agora;
    try {
      await ref.read(apiProvider).enviarLocalizacao(
            espera.sessaoId,
            PosicaoCelular(
              latitude: leitura.latitude,
              longitude: leitura.longitude,
              precisaoM: leitura.precisaoM?.roundToDouble(),
              capturadaEm: leitura.capturadaEm,
              simulada: leitura.simulada,
            ),
          );
      if (geracao != _geracao) return;
      _ultimaAceita = (latitude: leitura.latitude, longitude: leitura.longitude, em: agora);
      state = state.copiar(situacao: SituacaoGps.enviando, ultimoEnvio: agora);
    } on Object catch (e) {
      if (geracao != _geracao) return;
      final status = classificarErro(e).status;
      if (status == 409 || status == 404) {
        // A espera acabou (embarcou, cancelou) ou o servidor não tem a função: para e pergunta de novo.
        _desligarGps();
        state = state.copiar(situacao: SituacaoGps.parado);
        unawaited(atualizar());
      }
      // 400 (posição antiga), 429 ou rede: fica para a próxima posição.
    } finally {
      if (geracao == _geracao) _enviandoPosicao = false;
    }
  }

  void _desligarGps() {
    unawaited(_assinatura?.cancel());
    _assinatura = null;
    _ultimaAceita = null;
  }
}

final localizacaoProvider =
    NotifierProvider<LocalizacaoController, EstadoLocalizacao>(LocalizacaoController.new);
