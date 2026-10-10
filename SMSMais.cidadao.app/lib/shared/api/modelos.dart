// Contrato com a API do cidadão (`/auth/paciente/*`) — espelho dos tipos de
// `SMSMais.cidadao.pwa/src/lib/api.ts`, que é o cliente em produção. Mudou lá, muda aqui.

typedef Json = Map<String, dynamic>;

String? _s(Object? v) => v as String?;
bool _b(Object? v) => v as bool? ?? false;
int? _i(Object? v) => (v as num?)?.toInt();
List<T> _lista<T>(Object? v, T Function(Json) de) =>
    (v as List<dynamic>? ?? const []).map((e) => de(e as Json)).toList();

class PacienteSessao {
  const PacienteSessao({required this.id, required this.nome, required this.cpf});

  factory PacienteSessao.deJson(Json j) =>
      PacienteSessao(id: j['id'] as String, nome: _s(j['nome']) ?? '', cpf: _s(j['cpf']) ?? '');

  final String id;
  final String nome;
  final String cpf;

  Json paraJson() => {'id': id, 'nome': nome, 'cpf': cpf};
}

class Perfil {
  const Perfil({
    required this.id,
    required this.nome,
    required this.cpf,
    this.nomeSocial,
    this.cns,
    this.dataNascimento,
    this.email,
    this.telefonePrincipal,
    this.telefoneCelular,
    this.telefoneResidencial,
    this.fotoBase64,
  });

  factory Perfil.deJson(Json j) => Perfil(
        id: j['id'] as String,
        nome: _s(j['nome']) ?? '',
        nomeSocial: _s(j['nomeSocial']),
        cpf: _s(j['cpf']) ?? '',
        cns: _s(j['cns']),
        dataNascimento: _s(j['dataNascimento']),
        email: _s(j['email']),
        telefonePrincipal: _s(j['telefonePrincipal']),
        telefoneCelular: _s(j['telefoneCelular']),
        telefoneResidencial: _s(j['telefoneResidencial']),
        fotoBase64: _s(j['fotoBase64']),
      );

  final String id;
  final String nome;
  final String? nomeSocial;
  final String cpf;
  final String? cns;
  final String? dataNascimento;
  final String? email;
  final String? telefonePrincipal;
  final String? telefoneCelular;
  final String? telefoneResidencial;
  final String? fotoBase64;

  /// Nome que o app mostra: o social, quando houver.
  String get nomeExibicao => (nomeSocial?.isNotEmpty ?? false) ? nomeSocial! : nome;

  Json paraJson() => {
        'id': id,
        'nome': nome,
        'nomeSocial': nomeSocial,
        'cpf': cpf,
        'cns': cns,
        'dataNascimento': dataNascimento,
        'email': email,
        'telefonePrincipal': telefonePrincipal,
        'telefoneCelular': telefoneCelular,
        'telefoneResidencial': telefoneResidencial,
        'fotoBase64': fotoBase64,
      };

  Perfil copiar({
    String? fotoBase64,
    String? email,
    String? telefonePrincipal,
    String? telefoneCelular,
    String? telefoneResidencial,
    bool limparEmail = false,
    bool limparResidencial = false,
  }) =>
      Perfil(
        id: id,
        nome: nome,
        nomeSocial: nomeSocial,
        cpf: cpf,
        cns: cns,
        dataNascimento: dataNascimento,
        email: limparEmail ? null : (email ?? this.email),
        telefonePrincipal: telefonePrincipal ?? this.telefonePrincipal,
        telefoneCelular: telefoneCelular ?? this.telefoneCelular,
        telefoneResidencial: limparResidencial ? null : (telefoneResidencial ?? this.telefoneResidencial),
        fotoBase64: fotoBase64 ?? this.fotoBase64,
      );
}

class ConsentimentoStatus {
  const ConsentimentoStatus({required this.versao, required this.texto, required this.aceito, this.aceitoEm});

  factory ConsentimentoStatus.deJson(Json j) => ConsentimentoStatus(
        versao: _s(j['versao']) ?? '',
        texto: _s(j['texto']) ?? '',
        aceito: _b(j['aceito']),
        aceitoEm: _s(j['aceitoEm']),
      );

  final String versao;
  final String texto;
  final bool aceito;
  final String? aceitoEm;
}

/// Uma viagem do Transporte de Pacientes (sessão do atendimento).
class ViagemTransporte {
  const ViagemTransporte({
    required this.sessaoId,
    required this.data,
    required this.destino,
    required this.status,
    required this.acompanhantes,
    required this.limiteAcompanhantes,
    this.cidade,
    this.tipoTratamento,
    this.horaBusca,
  });

  factory ViagemTransporte.deJson(Json j) => ViagemTransporte(
        sessaoId: j['sessaoId'] as String,
        data: _s(j['data']) ?? '',
        destino: _s(j['destino']) ?? '',
        cidade: _s(j['cidade']),
        tipoTratamento: _s(j['tipoTratamento']),
        status: _s(j['status']) ?? '',
        horaBusca: _s(j['horaBusca']),
        acompanhantes: (j['acompanhantes'] as List<dynamic>? ?? const []).cast<String>(),
        limiteAcompanhantes: _i(j['limiteAcompanhantes']) ?? 1,
      );

  final String sessaoId;

  /// Data pura `aaaa-mm-dd`.
  final String data;
  final String destino;
  final String? cidade;
  final String? tipoTratamento;

  /// Pendente | Confirmada | Realizada | Cancelada | NaoRealizada | AguardandoRetorno
  final String status;

  /// "HH:mm:ss" quando a rota já definiu; senão, informado na véspera.
  final String? horaBusca;
  final List<String> acompanhantes;
  final int limiteAcompanhantes;
}

const parentescos = <(String valor, String rotulo)>[
  ('Mae', 'Mãe'),
  ('Pai', 'Pai'),
  ('Filho', 'Filho(a)'),
  ('Conjuge', 'Cônjuge'),
  ('Irmao', 'Irmão(ã)'),
  ('OutroParente', 'Outro parente'),
  ('Cuidador', 'Cuidador(a)'),
  ('Outro', 'Outro'),
];

String? rotuloParentesco(String? valor) {
  for (final p in parentescos) {
    if (p.$1 == valor) return p.$2;
  }
  return null;
}

/// Pessoa que pode acompanhar o paciente no transporte.
class AcompanhanteCidadao {
  const AcompanhanteCidadao({
    required this.id,
    required this.cpf,
    required this.nome,
    required this.dataNascimento,
    required this.origem,
    this.parentesco,
    this.telefone,
  });

  factory AcompanhanteCidadao.deJson(Json j) => AcompanhanteCidadao(
        id: j['id'] as String,
        cpf: _s(j['cpf']) ?? '',
        nome: _s(j['nome']) ?? '',
        dataNascimento: _s(j['dataNascimento']) ?? '',
        parentesco: _s(j['parentesco']),
        telefone: _s(j['telefone']),
        origem: _s(j['origem']) ?? 'App',
      );

  final String id;
  final String cpf;
  final String nome;
  final String dataNascimento;
  final String? parentesco;
  final String? telefone;

  /// Painel = cadastrado pela equipe; App = pelo próprio paciente.
  final String origem;
}

class ConsultaAcompanhante {
  const ConsultaAcompanhante({
    required this.cpf,
    required this.nome,
    required this.dataNascimento,
    required this.jaCadastrado,
  });

  factory ConsultaAcompanhante.deJson(Json j) => ConsultaAcompanhante(
        cpf: _s(j['cpf']) ?? '',
        nome: _s(j['nome']) ?? '',
        dataNascimento: _s(j['dataNascimento']) ?? '',
        jaCadastrado: _b(j['jaCadastrado']),
      );

  final String cpf;
  final String nome;
  final String dataNascimento;
  final bool jaCadastrado;
}

class DocumentoAtendimento {
  const DocumentoAtendimento({required this.id, required this.tipo, required this.conteudoHtml, this.data});

  factory DocumentoAtendimento.deJson(Json j) => DocumentoAtendimento(
        id: j['id'] as String,
        tipo: _s(j['tipo']) ?? 'Documento',
        data: _s(j['data']),
        conteudoHtml: _s(j['conteudoHtml']) ?? '',
      );

  final String id;
  final String tipo;
  final String? data;
  final String conteudoHtml;
}

class Atendimento {
  const Atendimento({
    required this.id,
    required this.data,
    required this.estabelecimento,
    required this.profissional,
    required this.descricao,
    required this.documentos,
  });

  factory Atendimento.deJson(Json j) => Atendimento(
        id: j['id'] as String,
        data: _s(j['data']) ?? '',
        estabelecimento: _s(j['estabelecimento']) ?? '',
        profissional: _s(j['profissional']) ?? '',
        descricao: _s(j['descricao']) ?? '',
        documentos: _lista(j['documentos'], DocumentoAtendimento.deJson),
      );

  final String id;
  final String data;
  final String estabelecimento;
  final String profissional;
  final String descricao;
  final List<DocumentoAtendimento> documentos;
}

class AnexoResumo {
  const AnexoResumo({required this.id, required this.nome, required this.tamanhoBytes, this.paginas});

  factory AnexoResumo.deJson(Json j) => AnexoResumo(
        id: j['id'] as String,
        nome: _s(j['nome']) ?? 'Documento',
        tamanhoBytes: _i(j['tamanhoBytes']) ?? 0,
        paginas: _i(j['paginas']),
      );

  final String id;
  final String nome;
  final int tamanhoBytes;
  final int? paginas;
}

class Exame {
  const Exame({
    required this.id,
    required this.data,
    required this.nome,
    required this.status,
    required this.temImagens,
    required this.documentos,
    required this.laudoAssinado,
    this.studyInstanceUid,
    this.laudoId,
  });

  factory Exame.deJson(Json j) => Exame(
        id: j['id'] as String,
        data: _s(j['data']) ?? '',
        nome: _s(j['nome']) ?? '',
        status: _s(j['status']) ?? '',
        studyInstanceUid: _s(j['studyInstanceUID']),
        temImagens: _b(j['temImagens']),
        documentos: _lista(j['documentos'], AnexoResumo.deJson),
        laudoId: _s(j['laudoId']),
        laudoAssinado: _b(j['laudoAssinado']),
      );

  final String id;
  final String data;
  final String nome;
  final String status;
  final String? studyInstanceUid;
  final bool temImagens;
  final List<AnexoResumo> documentos;
  final String? laudoId;
  final bool laudoAssinado;
}

class Laudo {
  const Laudo({required this.id, required this.data, required this.titulo, required this.status});

  factory Laudo.deJson(Json j) => Laudo(
        id: j['id'] as String,
        data: _s(j['data']) ?? '',
        titulo: _s(j['titulo']) ?? '',
        status: _s(j['status']) ?? '',
      );

  final String id;
  final String data;
  final String titulo;
  final String status;
}

class Agendamento {
  const Agendamento({
    required this.id,
    required this.tipo,
    required this.titulo,
    required this.status,
    required this.podeResponder,
    this.inicioEm,
    this.fimEm,
    this.profissional,
    this.unidade,
    this.solicitacaoExameId,
    this.statusConfirmacao,
    this.origem,
    this.naFila = false,
    this.momento,
    this.temHora = true,
  });

  factory Agendamento.deJson(Json j) => Agendamento(
        id: j['id'] as String,
        inicioEm: _s(j['inicioEm']),
        fimEm: _s(j['fimEm']),
        tipo: _s(j['tipo']) ?? 'Consulta',
        titulo: _s(j['titulo']) ?? '',
        profissional: _s(j['profissional']),
        unidade: _s(j['unidade']),
        status: _s(j['status']) ?? '',
        solicitacaoExameId: _s(j['solicitacaoExameId']),
        statusConfirmacao: _s(j['statusConfirmacao']),
        podeResponder: _b(j['podeResponder']),
        origem: _s(j['origem']),
        naFila: _b(j['naFila']),
        momento: _s(j['momento']),
        // Servidor antigo não manda: as datas dele sempre traziam hora.
        temHora: j['temHora'] as bool? ?? true,
      );

  final String id;

  /// Nulo quando o pedido ainda está NA FILA da regulação (sem data).
  final String? inicioEm;
  final String? fimEm;

  /// 'Consulta' | 'Exame'
  final String tipo;
  final String titulo;
  final String? profissional;
  final String? unidade;
  final String status;

  /// Pedido do SISREG: abre o ticket e, no próximo ainda sem resposta, confirma/avisa ausência.
  final String? solicitacaoExameId;

  /// 'Pendente' | 'Confirmada' | 'Cancelada' | null
  final String? statusConfirmacao;
  final bool podeResponder;

  /// Quem regula/marcou (SISREG, SER, SERNIT, ESUS SG), em linguagem do paciente.
  final String? origem;

  /// Na fila da regulação: só "está na fila" — nunca motivo de pendência, posição ou previsão.
  final bool naFila;

  /// 'Proximo' | 'NaFila' | 'Passado' — a lista vem em blocos, nessa ordem. Servidor antigo não manda.
  final String? momento;

  /// false: a fonte só deu o dia — não mostrar "00:00".
  final bool temHora;

  /// Parte da agenda, com o servidor antigo (sem [momento]): o que não está na fila era próximo.
  String get momentoEfetivo => momento ?? (naFila ? 'NaFila' : 'Proximo');
}

class AgendamentoExameDetalhe {
  const AgendamentoExameDetalhe({
    required this.solicitacaoExameId,
    required this.tipoExame,
    required this.prioridade,
    required this.statusConfirmacao,
    required this.chaveAcessoDisponivelHoje,
    this.dataAgendada,
    this.dataSolicitacao,
    this.dataRegulacao,
    this.unidadeExecutoraNome,
    this.unidadeExecutoraEndereco,
    this.unidadeExecutoraTelefone,
    this.unidadeSolicitanteNome,
    this.solicitanteNome,
    this.accessionNumber,
    this.codigoSolicitacao,
    this.observacoes,
    this.confirmadoEm,
    this.confirmadoCanal,
    this.confirmacaoCanceladaEm,
    this.motivoCancelamentoPaciente,
    this.tipo = 'Exame',
  });

  factory AgendamentoExameDetalhe.deJson(Json j) => AgendamentoExameDetalhe(
        solicitacaoExameId: j['solicitacaoExameId'] as String,
        tipoExame: _s(j['tipoExame']) ?? '',
        dataAgendada: _s(j['dataAgendada']),
        dataSolicitacao: _s(j['dataSolicitacao']),
        dataRegulacao: _s(j['dataRegulacao']),
        unidadeExecutoraNome: _s(j['unidadeExecutoraNome']),
        unidadeExecutoraEndereco: _s(j['unidadeExecutoraEndereco']),
        unidadeExecutoraTelefone: _s(j['unidadeExecutoraTelefone']),
        unidadeSolicitanteNome: _s(j['unidadeSolicitanteNome']),
        solicitanteNome: _s(j['solicitanteNome']),
        accessionNumber: _s(j['accessionNumber']),
        codigoSolicitacao: _s(j['codigoSolicitacao']),
        prioridade: _s(j['prioridade']) ?? '',
        observacoes: _s(j['observacoes']),
        statusConfirmacao: _s(j['statusConfirmacao']) ?? 'Pendente',
        confirmadoEm: _s(j['confirmadoEm']),
        confirmadoCanal: _s(j['confirmadoCanal']),
        confirmacaoCanceladaEm: _s(j['confirmacaoCanceladaEm']),
        motivoCancelamentoPaciente: _s(j['motivoCancelamentoPaciente']),
        chaveAcessoDisponivelHoje: _b(j['chaveAcessoDisponivelHoje']),
        tipo: _s(j['tipo']) ?? 'Exame',
      );

  final String solicitacaoExameId;
  final String tipoExame;
  final String? dataAgendada;
  final String? dataSolicitacao;
  final String? dataRegulacao;
  final String? unidadeExecutoraNome;
  final String? unidadeExecutoraEndereco;
  final String? unidadeExecutoraTelefone;
  final String? unidadeSolicitanteNome;
  final String? solicitanteNome;
  final String? accessionNumber;
  final String? codigoSolicitacao;
  final String prioridade;
  final String? observacoes;

  /// 'Pendente' | 'Confirmada' | 'Cancelada'
  final String statusConfirmacao;
  final String? confirmadoEm;
  final String? confirmadoCanal;
  final String? confirmacaoCanceladaEm;
  final String? motivoCancelamentoPaciente;

  /// Decidido no back: true só no dia do atendimento (Brasília).
  final bool chaveAcessoDisponivelHoje;

  /// 'Consulta' | 'Exame' — o ticket vale para qualquer pedido do SISREG.
  final String tipo;
}

/// Chave de acesso (confirmação do SISREG) — entregue só no dia do exame.
class ChaveAcessoExame {
  const ChaveAcessoExame({required this.chave, required this.codigoSolicitacao});

  factory ChaveAcessoExame.deJson(Json j) =>
      ChaveAcessoExame(chave: _s(j['chave']) ?? '', codigoSolicitacao: _s(j['codigoSolicitacao']) ?? '');

  final String chave;
  final String codigoSolicitacao;
}

/// Rótulo de origem que o servidor dá ao que o próprio paciente mandou pelo app.
const origemEnviadoPeloPaciente = 'Enviado pelo paciente';

/// Um item do acervo do paciente (menu Documentos).
class ItemAcervo {
  const ItemAcervo({
    required this.chave,
    required this.tipo,
    required this.id,
    required this.titulo,
    required this.mimeType,
    required this.data,
    required this.origem,
    required this.editavel,
    this.descricao,
    this.tamanhoBytes,
    this.paginas,
    this.situacao,
  });

  factory ItemAcervo.deJson(Json j) => ItemAcervo(
        chave: _s(j['chave']) ?? '${j['tipo']}:${j['id']}',
        tipo: _s(j['tipo']) ?? 'Documento',
        id: j['id'] as String,
        titulo: _s(j['titulo']) ?? '',
        descricao: _s(j['descricao']),
        mimeType: _s(j['mimeType']) ?? 'application/pdf',
        tamanhoBytes: _i(j['tamanhoBytes']),
        paginas: _i(j['paginas']),
        data: _s(j['data']) ?? '',
        origem: _s(j['origem']) ?? '',
        situacao: _s(j['situacao']),
        editavel: _b(j['editavel']),
      );

  /// "tipo:id" — única na lista.
  final String chave;

  /// 'Documento' | 'AnexoExame' | 'Laudo' | 'ImagensExame'
  final String tipo;
  final String id;
  final String titulo;
  final String? descricao;
  final String mimeType;
  final int? tamanhoBytes;
  final int? paginas;
  final String data;

  /// Rótulo pronto para exibir: "Cadastro", "Enviado pelo paciente", "Laudo"…
  final String origem;

  /// 'Pendente' | 'Aceito' | null
  final String? situacao;
  final bool editavel;

  bool get pendente => situacao == 'Pendente';
}

class TelefoneOtpEmitido {
  const TelefoneOtpEmitido({required this.canal, required this.expiraEmSegundos, this.mascara});

  factory TelefoneOtpEmitido.deJson(Json j) => TelefoneOtpEmitido(
        canal: _s(j['canal']) ?? '',
        mascara: _s(j['mascara']),
        expiraEmSegundos: _i(j['expiraEmSegundos']) ?? 0,
      );

  final String canal;
  final String? mascara;
  final int expiraEmSegundos;
}

class TelefoneValidado {
  const TelefoneValidado({required this.numero, required this.validado, this.validadoEm});

  factory TelefoneValidado.deJson(Json j) => TelefoneValidado(
        numero: _s(j['numero']) ?? '',
        validado: _b(j['validado']),
        validadoEm: _s(j['validadoEm']),
      );

  final String numero;
  final bool validado;
  final String? validadoEm;
}

/// Resposta de `POST /auth/paciente/solicitar-otp`.
class RespostaSolicitarOtp {
  const RespostaSolicitarOtp({required this.situacao, this.codigoTeste, this.telefoneMascarado});

  factory RespostaSolicitarOtp.deJson(Json? j) => RespostaSolicitarOtp(
        situacao: _s(j?['situacao']) ?? 'otp',
        codigoTeste: _s(j?['codigoTeste']),
        telefoneMascarado: _s(j?['telefoneMascarado']),
      );

  /// 'otp' (código enviado) | 'verificacao' (WhatsApp não verificado) | 'cadastro' (CPF novo).
  final String situacao;
  final String? codigoTeste;
  final String? telefoneMascarado;
}

/// Resposta do login (`validar-otp`, magic link).
class RespostaLogin {
  const RespostaLogin({required this.token, required this.paciente});

  factory RespostaLogin.deJson(Json j) =>
      RespostaLogin(token: j['token'] as String, paciente: PacienteSessao.deJson(j['paciente'] as Json));

  final String token;
  final PacienteSessao paciente;
}

/// Confirmação de agendamento devolvida pelo magic link do WhatsApp.
class ConfirmacaoAgendamento {
  const ConfirmacaoAgendamento({
    required this.solicitacaoExameId,
    required this.titulo,
    required this.confirmadaAgora,
    this.inicioEm,
    this.unidade,
  });

  factory ConfirmacaoAgendamento.deJson(Json j) => ConfirmacaoAgendamento(
        solicitacaoExameId: j['solicitacaoExameId'] as String,
        titulo: _s(j['titulo']) ?? '',
        inicioEm: _s(j['inicioEm']),
        unidade: _s(j['unidade']),
        confirmadaAgora: _b(j['confirmadaAgora']),
      );

  final String solicitacaoExameId;
  final String titulo;
  final String? inicioEm;
  final String? unidade;
  final bool confirmadaAgora;
}

/// Resposta de `POST /auth/paciente/magic` e `/magic/confirmar`.
class RespostaMagic {
  const RespostaMagic({
    required this.destino,
    this.token,
    this.paciente,
    this.confirmacaoAgendamento,
    this.requerConfirmacaoCpf = false,
    this.tentativasRestantes,
  });

  factory RespostaMagic.deJson(Json j) => RespostaMagic(
        token: _s(j['token']),
        paciente: j['paciente'] == null ? null : PacienteSessao.deJson(j['paciente'] as Json),
        destino: _s(j['destino']) ?? '/',
        confirmacaoAgendamento: j['confirmacaoAgendamento'] == null
            ? null
            : ConfirmacaoAgendamento.deJson(j['confirmacaoAgendamento'] as Json),
        requerConfirmacaoCpf: _b(j['requerConfirmacaoCpf']),
        tentativasRestantes: _i(j['tentativasRestantes']),
      );

  /// null quando o token já foi usado/expirado (não autentica).
  final String? token;
  final PacienteSessao? paciente;
  final String destino;
  final ConfirmacaoAgendamento? confirmacaoAgendamento;

  /// Link que carrega RESULTADO: só vira sessão depois de confirmar o CPF do titular.
  final bool requerConfirmacaoCpf;
  final int? tentativasRestantes;
}

/// O que o app usa de `GET /publico/instituicao`.
class InstituicaoPublica {
  const InstituicaoPublica({this.whatsAppNumeroPublico, this.nomeSecretaria});

  factory InstituicaoPublica.deJson(Json j) => InstituicaoPublica(
        whatsAppNumeroPublico: _s(j['whatsAppNumeroPublico']),
        nomeSecretaria: _s(j['nomeSecretaria']),
      );

  final String? whatsAppNumeroPublico;
  final String? nomeSecretaria;
}
