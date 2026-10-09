import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

/// Respostas FICTÍCIAS para `--dart-define=DEMO=true` e para os testes: deixa conferir todas as
/// telas sem backend e sem dado de paciente real. Pessoas, CPFs e números aqui são inventados
/// (o CPF é o de exemplo que circula em documentação). Nunca ligado num build de loja.
class DemoAdapter implements HttpClientAdapter {
  DemoAdapter({this.atraso = const Duration(milliseconds: 350), bool jaConsentiu = false})
      : _consentiu = jaConsentiu;

  final Duration atraso;
  bool _consentiu;
  final List<Map<String, dynamic>> _enviados = [];
  final List<Map<String, dynamic>> _acompanhantes = [
    {
      'id': 'a1',
      'cpf': '11144477735',
      'nome': 'JOSÉ CARLOS EXEMPLO',
      'dataNascimento': '1958-03-12',
      'parentesco': 'Conjuge',
      'telefone': null,
      'origem': 'Painel',
    },
  ];

  @override
  void close({bool force = false}) {}

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    await Future<void>.delayed(atraso);
    // Só o caminho (sem host nem query): as URLs públicas de download chegam com `?liberacao=`.
    final caminho = options.uri.path;
    final metodo = options.method.toUpperCase();
    Object? corpo;
    try {
      corpo = options.data is String ? jsonDecode(options.data as String) : options.data;
    } on FormatException {
      corpo = null;
    }

    final ehConteudo = caminho.endsWith('/pdf') ||
        caminho.endsWith('/conteudo') ||
        caminho.endsWith('/imagens-pdf') ||
        RegExp(r'^/publico/download/[^/]+$').hasMatch(caminho);
    if (ehConteudo) {
      return ResponseBody.fromBytes(
        _pdf('Documento de demonstração'),
        200,
        headers: {
          Headers.contentTypeHeader: ['application/pdf'],
          'content-disposition': ['inline; filename="documento-demonstracao.pdf"'],
        },
      );
    }

    final (status, dados) = _rota(metodo, caminho, corpo, options.queryParameters);
    return ResponseBody.fromString(
      dados == null ? '' : jsonEncode(dados),
      status,
      headers: {
        Headers.contentTypeHeader: [if (dados is Map && dados.containsKey('title')) 'application/problem+json' else 'application/json'],
      },
    );
  }

  (int, Object?) _rota(String metodo, String caminho, Object? corpo, Map<String, dynamic> query) {
    final hoje = DateTime.now();
    String em(int dias, [int hora = 8]) =>
        DateTime(hoje.year, hoje.month, hoje.day + dias, hora).toUtc().toIso8601String();
    String dia(int dias) => DateTime(hoje.year, hoje.month, hoje.day + dias).toIso8601String().substring(0, 10);

    switch ((metodo, caminho)) {
      case ('POST', '/auth/paciente/solicitar-otp'):
        return (200, {'situacao': 'otp', 'telefoneMascarado': '(**) *****-4321'});
      case ('POST', '/auth/paciente/solicitar-otp-verificacao'):
        return (200, {'telefoneMascarado': '(**) *****-4321'});
      case ('POST', '/auth/paciente/validar-otp'):
        final codigo = (corpo as Map?)?['codigo'];
        if (codigo == '000000') return (401, {'title': 'Código inválido ou expirado.', 'status': 401});
        return (200, {'token': 'demo', 'paciente': _paciente});
      case ('POST', '/auth/paciente/magic'):
        return (200, {'token': 'demo', 'paciente': _paciente, 'destino': '/exames'});
      case ('GET', '/auth/paciente/consentimento'):
        return (
          200,
          {
            'versao': '2026.1',
            'aceito': _consentiu,
            'aceitoEm': null,
            'texto': 'Termo de Consentimento para Tratamento de Dados Pessoais\n\n'
                'Este é um texto de DEMONSTRAÇÃO. No app de verdade, aqui aparece o termo vigente '
                'da Secretaria de Saúde, como a Lei Geral de Proteção de Dados exige.\n\n'
                'Seus dados de saúde são usados só para o seu atendimento na rede municipal.',
          },
        );
      case ('POST', '/auth/paciente/consentimento'):
        _consentiu = true;
        return (204, null);
      case ('POST', '/auth/paciente/logout'):
        return (204, null);
      case ('GET', '/publico/download/demo/status'):
        return (200, {'estado': 'valido', 'descricao': 'MAMOGRAFIA BILATERAL', 'requerCpf': true, 'tentativasRestantes': 3});
      case ('POST', '/publico/download/demo/confirmar'):
        final ok = (corpo as Map?)?['cpf'] == '52998224725';
        return (200, {'liberacao': ok ? 'demo-liberacao' : null, 'tentativasRestantes': ok ? 3 : 2, 'descricao': 'MAMOGRAFIA BILATERAL'});
      case ('GET', '/publico/instituicao'):
        return (200, {'whatsAppNumeroPublico': '552137315313', 'nomeSecretaria': 'Secretaria de Saúde'});
      case ('GET', '/auth/paciente/me'):
        return (200, _perfil);
      case ('PUT', '/auth/paciente/me/contato'):
        _perfil.addAll((corpo! as Map).cast<String, dynamic>());
        return (204, null);
      case ('PUT', '/auth/paciente/me/foto'):
        _perfil['fotoBase64'] = (corpo! as Map)['fotoBase64'];
        return (204, null);
      case ('POST', '/auth/paciente/me/contato/otp'):
        return (200, {'canal': 'whatsapp', 'mascara': '(**) *****-0000', 'expiraEmSegundos': 300});
      case ('POST', '/auth/paciente/me/contato/confirmar'):
        final numero = (corpo! as Map)['numero'] as String;
        _perfil['telefoneCelular'] = numero;
        return (200, {'numero': numero, 'validado': true, 'validadoEm': em(0)});
      case ('GET', '/auth/paciente/agendamentos'):
        return (200, query['tipo'] == 'consulta' ? _consultas(em) : _examesAgendados(em));
      case ('GET', '/auth/paciente/exames'):
        return (
          200,
          [
            {
              'id': 'e1',
              'data': em(-12),
              'nome': 'MAMOGRAFIA BILATERAL PARA RASTREAMENTO',
              'status': 'Laudo disponível',
              'studyInstanceUID': '1.2.3',
              'temImagens': true,
              'documentos': [
                {'id': 'd1', 'nome': 'Pedido médico', 'tamanhoBytes': 184320, 'paginas': 1},
              ],
              'laudoId': 'l1',
              'laudoAssinado': true,
            },
            {
              'id': 'e2',
              'data': em(-40),
              'nome': 'ULTRASSONOGRAFIA DE ABDOMEN TOTAL',
              'status': 'Realizado',
              'studyInstanceUID': null,
              'temImagens': false,
              'documentos': <Object>[],
              'laudoId': null,
              'laudoAssinado': false,
            },
          ],
        );
      case ('GET', '/auth/paciente/laudos'):
        return (200, [
          {'id': 'l1', 'data': em(-10), 'titulo': 'Mamografia', 'status': 'Assinado'},
        ]);
      case ('GET', '/auth/paciente/atendimentos'):
        return (
          200,
          [
            {
              'id': 'at1',
              'data': em(-20),
              'estabelecimento': 'UBS DEMONSTRAÇÃO',
              'profissional': 'Dra. Ana Exemplo · Clínica médica',
              'descricao': 'Consulta de rotina.',
              'documentos': [
                {
                  'id': 'doc1',
                  'tipo': 'Receituário',
                  'data': em(-20),
                  'conteudoHtml':
                      '<h2>Receituário (demonstração)</h2><p>1. Paracetamol 500 mg — 1 comprimido de 8 em 8 horas, se dor.</p>',
                },
              ],
            },
          ],
        );
      case ('GET', '/auth/paciente/documentos'):
        return (
          200,
          [
            ..._enviados,
            {
              'chave': 'Laudo:l1',
              'tipo': 'Laudo',
              'id': 'l1',
              'titulo': 'Laudo — Mamografia',
              'descricao': null,
              'mimeType': 'application/pdf',
              'tamanhoBytes': 98304,
              'paginas': 2,
              'data': em(-10),
              'origem': 'Laudo',
              'situacao': null,
              'editavel': false,
            },
            {
              'chave': 'ImagensExame:e1',
              'tipo': 'ImagensExame',
              'id': 'e1',
              'titulo': 'Imagens — Mamografia',
              'descricao': null,
              'mimeType': 'application/pdf',
              'tamanhoBytes': null,
              'paginas': null,
              'data': em(-12),
              'origem': 'Exame',
              'situacao': null,
              'editavel': false,
            },
            {
              'chave': 'AnexoExame:d1',
              'tipo': 'AnexoExame',
              'id': 'd1',
              'titulo': 'Pedido médico',
              'descricao': 'Anexado na recepção',
              'mimeType': 'application/pdf',
              'tamanhoBytes': 184320,
              'paginas': 1,
              'data': em(-12),
              'origem': 'Cadastro',
              'situacao': 'Aceito',
              'editavel': false,
            },
          ],
        );
      case ('POST', '/auth/paciente/documentos'):
        final form = corpo is FormData ? corpo : null;
        final titulo = form?.fields.firstWhere((f) => f.key == 'titulo', orElse: () => const MapEntry('', '')).value;
        _enviados.insert(0, {
          'chave': 'Documento:n${_enviados.length}',
          'tipo': 'Documento',
          'id': 'n${_enviados.length}',
          'titulo': (titulo?.isEmpty ?? true) ? 'Documento enviado' : titulo,
          'descricao': null,
          'mimeType': 'application/pdf',
          'tamanhoBytes': 120000,
          'paginas': null,
          'data': em(0, hoje.hour),
          'origem': 'Enviado pelo paciente',
          'situacao': 'Pendente',
          'editavel': true,
        });
        return (201, _enviados.first);
      case ('GET', '/auth/paciente/meus-translados'):
        return (
          200,
          [
            {
              'sessaoId': 's1',
              'data': dia(3),
              'destino': 'Hospital Demonstração',
              'cidade': 'Niterói',
              'tipoTratamento': 'Hemodiálise',
              'status': 'Confirmada',
              'horaBusca': '05:30:00',
              'acompanhantes': ['JOSÉ CARLOS EXEMPLO'],
              'limiteAcompanhantes': 1,
            },
            {
              'sessaoId': 's2',
              'data': dia(10),
              'destino': 'Hospital Demonstração',
              'cidade': 'Niterói',
              'tipoTratamento': 'Hemodiálise',
              'status': 'Pendente',
              'horaBusca': null,
              'acompanhantes': <String>[],
              'limiteAcompanhantes': 1,
            },
          ],
        );
      case ('GET', '/auth/paciente/me/acompanhantes'):
        return (200, _acompanhantes);
      case ('POST', '/auth/paciente/me/acompanhantes/consulta'):
        return (200, {'cpf': (corpo! as Map)['cpf'], 'nome': 'ANA PAULA EXEMPLO', 'dataNascimento': '1990-01-01', 'jaCadastrado': false});
      case ('POST', '/auth/paciente/me/acompanhantes'):
        _acompanhantes.add({
          'id': 'a${_acompanhantes.length + 1}',
          'cpf': (corpo! as Map)['cpf'],
          'nome': 'ANA PAULA EXEMPLO',
          'dataNascimento': '1990-01-01',
          'parentesco': (corpo as Map)['parentesco'],
          'telefone': null,
          'origem': 'App',
        });
        return (201, _acompanhantes.last);
    }

    final exameDetalhe = RegExp(r'^/auth/paciente/agendamentos/exames/([^/]+)$').firstMatch(caminho);
    if (exameDetalhe != null) {
      return (
        200,
        {
          'solicitacaoExameId': exameDetalhe.group(1),
          'tipoExame': 'MAMOGRAFIA BILATERAL PARA RASTREAMENTO',
          'dataAgendada': em(5, 9),
          'dataSolicitacao': dia(-30),
          'dataRegulacao': dia(-7),
          'unidadeExecutoraNome': 'CENTRO DE IMAGEM DEMONSTRAÇÃO',
          'unidadeExecutoraEndereco': 'Rua Exemplo, 100 — Centro',
          'unidadeExecutoraTelefone': '(21) 0000-0000',
          'unidadeSolicitanteNome': 'UBS DEMONSTRAÇÃO',
          'solicitanteNome': 'Dra. Ana Exemplo',
          'accessionNumber': 'DEMO000123',
          'codigoSolicitacao': '600000123',
          'prioridade': 'Rotina',
          'observacoes': null,
          'statusConfirmacao': 'Pendente',
          'confirmadoEm': null,
          'confirmadoCanal': null,
          'confirmacaoCanceladaEm': null,
          'motivoCancelamentoPaciente': null,
          'chaveAcessoDisponivelHoje': false,
        },
      );
    }
    if (metodo == 'POST' && caminho.endsWith('/chave-acesso')) {
      return (200, {'chave': '4821', 'codigoSolicitacao': '600000123'});
    }
    if (metodo == 'POST' && (caminho.endsWith('/confirmar') || caminho.endsWith('/cancelar'))) {
      return (204, null);
    }
    if (metodo == 'DELETE' && caminho.startsWith('/auth/paciente/documentos/')) {
      final id = caminho.split('/').last;
      _enviados.removeWhere((e) => e['id'] == id);
      return (204, null);
    }
    if (metodo == 'DELETE' && caminho.startsWith('/auth/paciente/me/acompanhantes/')) {
      final id = caminho.split('/').last;
      _acompanhantes.removeWhere((e) => e['id'] == id);
      return (204, null);
    }
    return (404, {'title': 'Não encontrado (demonstração).', 'status': 404});
  }

  /// Sessão fictícia usada pelo `DEMO_LOGADO`.
  static const sessaoDemo = (
    id: '00000000-0000-7000-0000-00000000d3e0',
    nome: 'MARIA DA CONCEIÇÃO EXEMPLO',
    cpf: '52998224725',
  );

  static final Map<String, dynamic> _paciente = {
    'id': '00000000-0000-7000-0000-00000000d3e0',
    'nome': 'MARIA DA CONCEIÇÃO EXEMPLO',
    'cpf': '52998224725',
  };

  final Map<String, dynamic> _perfil = {
    'id': '00000000-0000-7000-0000-00000000d3e0',
    'nome': 'MARIA DA CONCEIÇÃO EXEMPLO',
    'nomeSocial': null,
    'cpf': '52998224725',
    'cns': '700000000000000',
    'dataNascimento': '1961-05-20',
    'email': 'maria.exemplo@exemplo.com',
    'telefonePrincipal': '5521900004321',
    'telefoneCelular': '5521900004321',
    'telefoneResidencial': null,
    'fotoBase64': null,
  };

  List<Map<String, dynamic>> _consultas(String Function(int, [int]) em) => [
        {
          'id': 'c1',
          'inicioEm': em(2, 10),
          'fimEm': null,
          'tipo': 'Consulta',
          'titulo': 'CONSULTA EM CARDIOLOGIA',
          'profissional': 'Dr. Paulo Exemplo',
          'unidade': 'POLICLÍNICA DEMONSTRAÇÃO',
          'status': 'Agendada',
          'solicitacaoExameId': null,
          'statusConfirmacao': null,
          'podeResponder': false,
          'origem': null,
          'naFila': false,
        },
        {
          'id': 'c2',
          'inicioEm': null,
          'fimEm': null,
          'tipo': 'Consulta',
          'titulo': 'CONSULTA EM ORTOPEDIA',
          'profissional': null,
          'unidade': null,
          'status': 'Na fila',
          'solicitacaoExameId': null,
          'statusConfirmacao': null,
          'podeResponder': false,
          'origem': 'regulação estadual (SER)',
          'naFila': true,
        },
      ];

  List<Map<String, dynamic>> _examesAgendados(String Function(int, [int]) em) => [
        {
          'id': 'x1',
          'inicioEm': em(5, 9),
          'fimEm': null,
          'tipo': 'Exame',
          'titulo': 'MAMOGRAFIA BILATERAL PARA RASTREAMENTO',
          'profissional': null,
          'unidade': 'CENTRO DE IMAGEM DEMONSTRAÇÃO',
          'status': 'Agendado',
          'solicitacaoExameId': 'se1',
          'statusConfirmacao': 'Pendente',
          'podeResponder': true,
          'origem': null,
          'naFila': false,
        },
        {
          'id': 'x2',
          'inicioEm': em(12, 14),
          'fimEm': null,
          'tipo': 'Exame',
          'titulo': 'ECOCARDIOGRAMA TRANSTORACICO',
          'profissional': null,
          'unidade': 'POLICLÍNICA DEMONSTRAÇÃO',
          'status': 'Agendado',
          'solicitacaoExameId': 'se2',
          'statusConfirmacao': 'Confirmada',
          'podeResponder': false,
          'origem': null,
          'naFila': false,
        },
      ];

  /// PDF mínimo de 1 página com um título — o bastante para o visualizador abrir.
  static Uint8List _pdf(String texto) {
    final conteudo = 'BT /F1 22 Tf 60 760 Td ($texto) Tj ET\n'
        'BT /F1 12 Tf 60 730 Td (Arquivo ficticio do modo demonstracao.) Tj ET';
    final objetos = [
      '<< /Type /Catalog /Pages 2 0 R >>',
      '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
      '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
      '<< /Length ${latin1.encode(conteudo).length} >>\nstream\n$conteudo\nendstream',
      '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>',
    ];
    final b = StringBuffer('%PDF-1.4\n');
    final offsets = <int>[];
    for (var i = 0; i < objetos.length; i++) {
      offsets.add(latin1.encode(b.toString()).length);
      b.write('${i + 1} 0 obj\n${objetos[i]}\nendobj\n');
    }
    final xref = latin1.encode(b.toString()).length;
    b
      ..write('xref\n0 ${objetos.length + 1}\n0000000000 65535 f \n')
      ..writeAll(offsets.map((o) => '${o.toString().padLeft(10, '0')} 00000 n \n'))
      ..write('trailer\n<< /Size ${objetos.length + 1} /Root 1 0 R >>\nstartxref\n$xref\n%%EOF\n');
    return Uint8List.fromList(latin1.encode(b.toString()));
  }
}
