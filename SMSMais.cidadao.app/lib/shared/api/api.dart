import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/cache/dados_cache.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// Cliente da API do cidadão — espelho do objeto `api` de `SMSMais.cidadao.pwa/src/lib/api.ts`.
/// Leituras clínicas: rede-primeiro com fallback OFFLINE ([DadosCache]); escritas nunca usam cache.
class ApiCidadao {
  ApiCidadao(this._dio, this._cache);

  final Dio _dio;
  final DadosCache _cache;

  Future<Object?> _get(String caminho, {Map<String, dynamic>? query}) async =>
      (await _dio.get<Object?>(caminho, queryParameters: query)).data;

  List<T> Function(Object?) _lista<T>(T Function(Json) de) =>
      (dados) => (dados as List<dynamic>? ?? const []).map((e) => de(e as Json)).toList();

  // ── Login (rotas públicas) ────────────────────────────────────────────────────────────────

  Future<RespostaSolicitarOtp> solicitarOtp(String cpf) async {
    final r = await _dio.post<Json>('/auth/paciente/solicitar-otp', data: {'cpf': cpf});
    return RespostaSolicitarOtp.deJson(r.data);
  }

  Future<RespostaSolicitarOtp> solicitarOtpVerificacao({
    required String cpf,
    required String dataNascimento,
    required String? codigoSolicitacao,
    required String telefone,
  }) async {
    final r = await _dio.post<Json>(
      '/auth/paciente/solicitar-otp-verificacao',
      data: {
        'cpf': cpf,
        'dataNascimento': dataNascimento,
        'codigoSolicitacao': codigoSolicitacao,
        'telefone': telefone,
      },
    );
    return RespostaSolicitarOtp.deJson(r.data);
  }

  Future<RespostaLogin> validarOtp(String cpf, String codigo) async {
    final r = await _dio.post<Json>('/auth/paciente/validar-otp', data: {'cpf': cpf, 'codigo': codigo});
    return RespostaLogin.deJson(r.data!);
  }

  Future<RespostaMagic> magic(String token) async {
    final r = await _dio.post<Json>('/auth/paciente/magic', data: {'token': token});
    return RespostaMagic.deJson(r.data!);
  }

  Future<RespostaMagic> magicConfirmar(String token, String cpf) async {
    final r = await _dio.post<Json>('/auth/paciente/magic/confirmar', data: {'token': token, 'cpf': cpf});
    return RespostaMagic.deJson(r.data!);
  }

  // ── Link público de download (uso único) ──────────────────────────────────────────────────

  Future<Json> downloadStatus(String token) async =>
      (await _dio.get<Json>('/publico/download/$token/status')).data ?? const {};

  Future<Json> downloadConfirmar(String token, String cpf) async =>
      (await _dio.post<Json>('/publico/download/$token/confirmar', data: {'cpf': cpf})).data ?? const {};

  /// Conteúdo do link de uso único (relativo: vai pela mesma base da API).
  String urlDownloadPublico(String token, String? liberacao) =>
      '/publico/download/$token?liberacao=${Uri.encodeQueryComponent(liberacao ?? '')}';

  Future<InstituicaoPublica> instituicao() async =>
      InstituicaoPublica.deJson((await _dio.get<Json>('/publico/instituicao')).data ?? const {});

  // ── Sessão, consentimento, perfil ─────────────────────────────────────────────────────────

  Future<ConsentimentoStatus> consentimento() async =>
      ConsentimentoStatus.deJson((await _dio.get<Json>('/auth/paciente/consentimento')).data!);

  Future<void> aceitarConsentimento() => _dio.post<void>('/auth/paciente/consentimento');

  Future<void> logout() => _dio.post<void>('/auth/paciente/logout');

  /// Token do push (Firebase) desta sessão — o servidor guarda na sessão e apaga no logout.
  /// [plataforma] = 'android' | 'ios'.
  Future<void> registrarDispositivo(String token, String plataforma) =>
      _dio.put<void>('/auth/paciente/dispositivo', data: {'token': token, 'plataforma': plataforma});

  Future<Perfil> perfil() =>
      _cache.comCache('perfil', () => _get('/auth/paciente/me'), (d) => Perfil.deJson(d! as Json));

  Future<void> salvarContato({
    required String? email,
    required String? telefonePrincipal,
    required String? telefoneCelular,
    required String? telefoneResidencial,
  }) =>
      _dio.put<void>(
        '/auth/paciente/me/contato',
        data: {
          'email': email,
          'telefonePrincipal': telefonePrincipal,
          'telefoneCelular': telefoneCelular,
          'telefoneResidencial': telefoneResidencial,
        },
      );

  /// Troca do celular por OTP: envia código ao número NOVO; só efetiva ao confirmar.
  Future<TelefoneOtpEmitido> solicitarOtpContato(String numero) async => TelefoneOtpEmitido.deJson(
        (await _dio.post<Json>('/auth/paciente/me/contato/otp', data: {'numero': numero})).data!,
      );

  Future<TelefoneValidado> confirmarContato(String numero, String codigo) async => TelefoneValidado.deJson(
        (await _dio.post<Json>('/auth/paciente/me/contato/confirmar', data: {'numero': numero, 'codigo': codigo}))
            .data!,
      );

  Future<void> salvarFoto(String? fotoBase64) =>
      _dio.put<void>('/auth/paciente/me/foto', data: {'fotoBase64': fotoBase64});

  // ── Transporte e acompanhantes ────────────────────────────────────────────────────────────

  Future<List<ViagemTransporte>> translados() => _cache.comCache(
        'translados',
        () => _get('/auth/paciente/meus-translados'),
        _lista(ViagemTransporte.deJson),
      );

  Future<List<AcompanhanteCidadao>> acompanhantes() => _cache.comCache(
        'acompanhantes',
        () => _get('/auth/paciente/me/acompanhantes'),
        _lista(AcompanhanteCidadao.deJson),
      );

  /// Confere CPF + nascimento e traz o nome para confirmar. Tem cota diária (429).
  Future<ConsultaAcompanhante> consultarAcompanhante(String cpf, String dataNascimento) async =>
      ConsultaAcompanhante.deJson(
        (await _dio.post<Json>(
          '/auth/paciente/me/acompanhantes/consulta',
          data: {'cpf': cpf, 'dataNascimento': dataNascimento},
        ))
            .data!,
      );

  Future<void> adicionarAcompanhante(String cpf, String dataNascimento, String? parentesco) => _dio.post<void>(
        '/auth/paciente/me/acompanhantes',
        data: {'cpf': cpf, 'dataNascimento': dataNascimento, 'parentesco': parentesco, 'telefone': null},
      );

  Future<void> removerAcompanhante(String id) => _dio.delete<void>('/auth/paciente/me/acompanhantes/$id');

  // ── Histórico clínico ─────────────────────────────────────────────────────────────────────

  Future<List<Atendimento>> atendimentos() =>
      _cache.comCache('atendimentos', () => _get('/auth/paciente/atendimentos'), _lista(Atendimento.deJson));

  Future<List<Exame>> exames() =>
      _cache.comCache('exames', () => _get('/auth/paciente/exames'), _lista(Exame.deJson));

  Future<List<Laudo>> laudos() =>
      _cache.comCache('laudos', () => _get('/auth/paciente/laudos'), _lista(Laudo.deJson));

  // ── Agenda ────────────────────────────────────────────────────────────────────────────────

  /// [tipo] = 'consulta' | 'exame'.
  Future<List<Agendamento>> agendamentos(String tipo) => _cache.comCache(
        'agendamentos.$tipo',
        () => _get('/auth/paciente/agendamentos', query: {'tipo': tipo}),
        _lista(Agendamento.deJson),
      );

  Future<AgendamentoExameDetalhe> agendamentoExame(String id) => _cache.comCache(
        'agendamento.$id',
        () => _get('/auth/paciente/agendamentos/exames/$id'),
        (d) => AgendamentoExameDetalhe.deJson(d! as Json),
      );

  Future<void> confirmarExame(String id) => _dio.post<void>('/auth/paciente/agendamentos/exames/$id/confirmar');

  /// Sem cache local de propósito: a chave é lida na hora (o back decide se é o dia).
  Future<ChaveAcessoExame> chaveAcessoExame(String id) async => ChaveAcessoExame.deJson(
        (await _dio.post<Json>('/auth/paciente/agendamentos/exames/$id/chave-acesso')).data!,
      );

  Future<void> cancelarExame(String id, String motivo) =>
      _dio.post<void>('/auth/paciente/agendamentos/exames/$id/cancelar', data: {'motivo': motivo});

  // ── Acervo (Documentos) ───────────────────────────────────────────────────────────────────

  Future<List<ItemAcervo>> documentos() =>
      _cache.comCache('documentos', () => _get('/auth/paciente/documentos'), _lista(ItemAcervo.deJson));

  /// Envia foto/PDF para o cadastro — entra Pendente até a equipe conferir.
  Future<void> enviarDocumento({
    required Uint8List bytes,
    required String nomeArquivo,
    required String mimeType,
    required String titulo,
    required String? descricao,
    void Function(double fracao)? aoProgresso,
  }) async {
    final form = FormData.fromMap({
      'arquivo': MultipartFile.fromBytes(bytes, filename: nomeArquivo, contentType: DioMediaType.parse(mimeType)),
      'titulo': titulo,
      if (descricao != null && descricao.isNotEmpty) 'descricao': descricao,
    });
    await _dio.post<void>(
      '/auth/paciente/documentos',
      data: form,
      // Sinal fraco é a regra: o envio de uma foto pode levar um bom tempo — mostra o andamento.
      onSendProgress: (enviado, total) {
        if (aoProgresso != null && total > 0) aoProgresso(enviado / total);
      },
      options: Options(sendTimeout: const Duration(minutes: 5)),
    );
  }

  /// Retira um envio do próprio paciente que a equipe ainda não conferiu.
  Future<void> retirarDocumento(String id) => _dio.delete<void>('/auth/paciente/documentos/$id');

  /// Baixa um conteúdo protegido (PDF/foto) — bytes, nome sugerido e tipo informado pelo servidor.
  Future<({Uint8List bytes, String? nome, String? tipo})> baixar(String url) async {
    final r = await _dio.get<List<int>>(url, options: Options(responseType: ResponseType.bytes));
    final tipo = r.headers.value('content-type')?.split(';').first.trim();
    return (
      bytes: Uint8List.fromList(r.data ?? const []),
      nome: nomeDoHeader(r.headers.value('content-disposition')),
      tipo: (tipo?.isEmpty ?? true) ? null : tipo,
    );
  }
}

String? nomeDoHeader(String? disposition) {
  if (disposition == null) return null;
  final m = RegExp(r'''filename\*?=(?:UTF-8'')?"?([^";]+)"?''', caseSensitive: false).firstMatch(disposition);
  final bruto = m?.group(1);
  if (bruto == null) return null;
  try {
    return Uri.decodeComponent(bruto);
  } on Object {
    return bruto;
  }
}

/// URLs de conteúdo protegido (abertas no visualizador embutido).
abstract final class UrlsConteudo {
  static String laudo(String laudoId) => '/auth/paciente/laudos/$laudoId/pdf';
  static String anexo(String anexoId) => '/auth/paciente/anexos/$anexoId/conteudo';
  static String exameImagens(String solicitacaoId) => '/auth/paciente/exames/$solicitacaoId/imagens-pdf';

  /// Laudo, imagens e anexos da anamnese usam as MESMAS URLs da tela Exames (mesmos ids, mesmo
  /// arquivo): assim reaproveitam o que a sincronização já deixou no aparelho e não geram de novo
  /// o PDF de imagens, que é pesado. Só o documento do cadastro tem endpoint próprio.
  static String acervo(ItemAcervo item) => switch (item.tipo) {
        'Laudo' => laudo(item.id),
        'ImagensExame' => exameImagens(item.id),
        'AnexoExame' => anexo(item.id),
        _ => '/auth/paciente/documentos/${item.tipo}/${item.id}/conteudo',
      };
}

final apiProvider = Provider<ApiCidadao>((ref) {
  final pacienteId = ref.watch(sessaoProvider.select((s) => s?.paciente.id));
  return ApiCidadao(ref.watch(dioProvider), DadosCache(pacienteId));
});
