import { http } from './httpClient';
import { comCacheLocal } from './dadosCache';

export type Perfil = {
  id: string;
  nome: string;
  nomeSocial: string | null;
  cpf: string;
  cns: string | null;
  dataNascimento: string | null;
  email: string | null;
  telefonePrincipal: string | null;
  telefoneCelular: string | null;
  telefoneResidencial: string | null;
  fotoBase64: string | null;
};

export type AtualizarContato = {
  email: string | null;
  telefonePrincipal: string | null;
  telefoneCelular: string | null;
  telefoneResidencial: string | null;
};

/** Uma viagem do Transporte de Pacientes (sessão do atendimento). */
export type ViagemTransporte = {
  sessaoId: string;
  data: string;
  destino: string;
  cidade: string | null;
  tipoTratamento: string | null;
  /** Pendente | Confirmada | Realizada | Cancelada | NaoRealizada | AguardandoRetorno */
  status: string;
  /** "HH:mm:ss" quando a rota já definiu; senão, informado na véspera. */
  horaBusca: string | null;
  acompanhantes: string[];
  limiteAcompanhantes: number;
};

/** Um trecho de viagem dos últimos 30 dias, para avaliar o motorista: a ida (até a unidade) e o
 * resgate (de volta para casa), cada um com o seu motorista. `nota` nula = ainda não avaliou. */
export type ViagemParaAvaliar = {
  sessaoId: string;
  data: string;
  destino: string;
  /** Só o primeiro nome de quem dirigiu aquele trecho. */
  motorista: string | null;
  nota: number | null;
  comentario: string | null;
  trecho: 'Ida' | 'Resgate';
  /** "HH:mm:ss": quando desceu (na unidade ou em casa). */
  hora: string | null;
};

/** O resgate de hoje (buscar na unidade depois do atendimento e levar para casa). */
export type ResgateDoPaciente = {
  sessaoId: string;
  unidade: string;
  disponibilidade: 'NoLocal' | 'PelaPrevisao' | 'EmAtendimento' | 'SemPrevisao';
  /** "HH:mm:ss": quando fica pronto pela previsão. */
  disponivelAs: string | null;
  /** Desde quando avisaram que está no local esperando. */
  noLocalDesde: string | null;
  avisadoPor: 'Paciente' | 'Central' | 'Motorista' | null;
  /** O carro que vai buscar (nulo = nenhum carro marcou ainda). */
  veiculoPlaca: string | null;
  veiculoModelo: string | null;
  /** Já embarcou: está a caminho de casa. */
  embarcou: boolean;
  /** A previsão estourou: hora de responder "você já está disponível para retorno?". */
  perguntaPendente: boolean;
  /** Depois de "ainda não terminei": quando a pergunta volta. */
  proximaPerguntaAs: string | null;
};

export const PARENTESCOS: { valor: string; rotulo: string }[] = [
  { valor: 'Mae', rotulo: 'Mãe' },
  { valor: 'Pai', rotulo: 'Pai' },
  { valor: 'Filho', rotulo: 'Filho(a)' },
  { valor: 'Conjuge', rotulo: 'Cônjuge' },
  { valor: 'Irmao', rotulo: 'Irmão(ã)' },
  { valor: 'OutroParente', rotulo: 'Outro parente' },
  { valor: 'Cuidador', rotulo: 'Cuidador(a)' },
  { valor: 'Outro', rotulo: 'Outro' },
];

/** Pessoa que pode acompanhar o paciente no transporte. */
export type AcompanhanteCidadao = {
  id: string;
  cpf: string;
  nome: string;
  dataNascimento: string;
  parentesco: string | null;
  telefone: string | null;
  /** Painel = cadastrado pela equipe; App = pelo próprio paciente. */
  origem: 'Painel' | 'App';
};

export type ConsultaAcompanhante = { cpf: string; nome: string; dataNascimento: string; jaCadastrado: boolean };
export type DocumentoAtendimento = {
  id: string;
  tipo: string;
  data: string | null;
  conteudoHtml: string;
};
export type Atendimento = {
  id: string;
  data: string;
  estabelecimento: string;
  profissional: string;
  descricao: string;
  documentos: DocumentoAtendimento[];
};
export type AnexoResumo = { id: string; nome: string; tamanhoBytes: number; paginas: number | null };
export type Exame = {
  id: string;
  data: string;
  nome: string;
  status: string;
  studyInstanceUID: string | null;
  temImagens: boolean;
  documentos: AnexoResumo[];
  laudoId: string | null;
  laudoAssinado: boolean;
};
export type Laudo = { id: string; data: string; titulo: string; status: string };
export type PesquisaPublica = {
  unidade: string | null;
  atendimentoEm: string;
  expiraEm: string;
  expirada: boolean;
  jaRespondida: boolean;
  instrumentoVersao: string;
};
/** Parte da agenda: agendado de hoje em diante, esperando vaga, ou já passou/encerrou. */
export type MomentoAgendamento = 'Proximo' | 'NaFila' | 'Passado';

export type Agendamento = {
  id: string;
  // Nulo quando o pedido ainda está NA FILA da regulação (sem data).
  inicioEm: string | null;
  fimEm: string | null;
  tipo: 'Consulta' | 'Exame';
  titulo: string;
  profissional: string | null;
  unidade: string | null;
  status: string;
  // Pedido do SISREG: abre o ticket e, no próximo ainda sem resposta, confirma/avisa ausência.
  solicitacaoExameId: string | null;
  statusConfirmacao: 'Pendente' | 'Confirmada' | 'Cancelada' | null;
  podeResponder: boolean;
  // Quem regula/marcou (SISREG, SER, SERNIT, ESUS SG), em linguagem do paciente.
  origem?: string | null;
  // Na fila da regulação: só "está na fila" — nunca motivo de pendência, posição ou previsão.
  naFila?: boolean;
  // A lista vem em blocos, nessa ordem: Proximo → NaFila → Passado. Servidor antigo não manda.
  momento?: MomentoAgendamento;
  // false: a fonte só deu o dia — não mostrar "00:00".
  temHora?: boolean;
  // SER/SERNIT/ESUS: a guia do agendamento no acervo (a mesma de Documentos). Servidor antigo não manda.
  guiaDocumentoId?: string | null;
};

export type AgendamentoExameDetalhe = {
  solicitacaoExameId: string;
  tipoExame: string;
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  dataRegulacao: string | null;
  unidadeExecutoraNome: string | null;
  unidadeExecutoraEndereco: string | null;
  unidadeExecutoraTelefone: string | null;
  unidadeSolicitanteNome: string | null;
  solicitanteNome: string | null;
  accessionNumber: string | null;
  codigoSolicitacao: string | null;
  prioridade: string;
  observacoes: string | null;
  statusConfirmacao: 'Pendente' | 'Confirmada' | 'Cancelada';
  confirmadoEm: string | null;
  confirmadoCanal: string | null;
  confirmacaoCanceladaEm: string | null;
  motivoCancelamentoPaciente: string | null;
  /** Decidido no back: true só no dia do atendimento (Brasília). */
  chaveAcessoDisponivelHoje: boolean;
  /** O ticket vale para qualquer pedido do SISREG; servidor antigo não manda (= exame). */
  tipo?: 'Consulta' | 'Exame';
};

/** Chave de acesso (confirmação do SISREG) — entregue só no dia do exame. */
export type ChaveAcessoExame = { chave: string; codigoSolicitacao: string };

/**
 * Um item do acervo do paciente (menu Documentos): o que está no cadastro dele — documentos,
 * anexos da anamnese, laudos assinados, PDF das imagens dos exames — e o que ele mesmo enviou
 * e ainda espera a equipe conferir (`situacao: 'Pendente'`).
 */
export type ItemAcervo = {
  /** "tipo:id" — única na lista. */
  chave: string;
  tipo: 'Documento' | 'AnexoExame' | 'Laudo' | 'ImagensExame';
  id: string;
  titulo: string;
  descricao: string | null;
  mimeType: string;
  tamanhoBytes: number | null;
  paginas: number | null;
  data: string;
  /** Rótulo pronto para exibir: "Cadastro", "Enviado pelo paciente", "Laudo"… */
  origem: string;
  situacao: 'Pendente' | 'Aceito' | null;
  editavel: boolean;
};

/** Rótulo de origem que o servidor dá ao que o próprio paciente mandou pelo app. */
export const ORIGEM_ENVIADO_PELO_PACIENTE = 'Enviado pelo paciente';

/** Um número para onde o código pode ir. O app só vê o final; o número fica no servidor (ADR-0078). */
export type OpcaoTelefoneEntrada = {
  id: string;
  /** Últimos 4 dígitos. */
  final: string;
  /** Do cadastro da Saúde, ou achado no e-SUS PEC na hora ("Nenhum desses é meu"). */
  origem: 'cadastro' | 'esus';
  /** É o WhatsApp já confirmado — aí ele é o único da lista (trocar é no posto). */
  verificado: boolean;
};

/** Resposta de `POST /auth/paciente/entrada` e de `.../esus` (que devolve a lista inteira). */
export type EntradaCidadao = {
  desafioId: string;
  opcoes: OpcaoTelefoneEntrada[];
  /** O canal deixa procurar no e-SUS e ainda não se procurou nesta entrada. */
  podeBuscarEsus: boolean;
  validadeSegundos: number;
};

/** Código emitido. `codigoTeste` só vem quando o servidor não está com o WhatsApp configurado. */
export type OtpEmitido = {
  enviado: boolean;
  canal: string;
  codigoTeste: string | null;
  validadeSegundos: number;
  telefoneMascarado: string | null;
};

export type TelefoneOtpEmitido ={ canal: string; mascara: string | null; expiraEmSegundos: number };
export type TelefoneValidado = { numero: string; validado: boolean; validadoEm: string | null };

export type ConsentimentoStatus = {
  versao: string;
  texto: string;
  aceito: boolean;
  aceitoEm: string | null;
};

export const api = {
  // Entrada (rotas públicas, ADR-0078): CPF + nascimento → os números que a Saúde já conhece →
  // o código vai para o escolhido. Nunca para um número digitado.
  entrada: (cpf: string, dataNascimento: string) =>
    http.post<EntradaCidadao>('/auth/paciente/entrada', { cpf, dataNascimento }).then((r) => r.data),
  /** "Nenhum desses é meu": procura no e-SUS. Pode levar uns 10–20 s. */
  procurarTelefoneNoEsus: (desafioId: string) =>
    http
      .post<EntradaCidadao>(`/auth/paciente/entrada/${encodeURIComponent(desafioId)}/esus`)
      .then((r) => r.data),
  enviarCodigo: (desafioId: string, opcaoId: string) =>
    http
      .post<OtpEmitido>(`/auth/paciente/entrada/${encodeURIComponent(desafioId)}/enviar`, { opcaoId })
      .then((r) => r.data),

  consentimento: () =>
    http.get<ConsentimentoStatus>('/auth/paciente/consentimento').then((r) => r.data),
  aceitarConsentimento: () => http.post('/auth/paciente/consentimento'),
  // Leituras clínicas: rede-primeiro com fallback OFFLINE (ver lib/dadosCache).
  perfil: () => comCacheLocal('perfil', () => http.get<Perfil>('/auth/paciente/me').then((r) => r.data)),
  salvarContato: (body: AtualizarContato) => http.put('/auth/paciente/me/contato', body),
  // Troca do celular por OTP: envia código ao número NOVO; só efetiva ao confirmar.
  solicitarOtpContato: (numero: string) =>
    http
      .post<TelefoneOtpEmitido>('/auth/paciente/me/contato/otp', { numero })
      .then((r) => r.data),
  confirmarContato: (numero: string, codigo: string) =>
    http
      .post<TelefoneValidado>('/auth/paciente/me/contato/confirmar', { numero, codigo })
      .then((r) => r.data),
  salvarFoto: (fotoBase64: string | null) => http.put('/auth/paciente/me/foto', { fotoBase64 }),
  logout: () => http.post('/auth/paciente/logout'),

  /** Contexto da pesquisa pelo token do WhatsApp — rota pública, sem sessão. */
  pesquisa: (token: string) =>
    http.get<PesquisaPublica>(`/publico/pesquisa/${token}`).then((r) => r.data),
  responderPesquisaPorToken: (token: string, respostas: Record<string, string>) =>
    http.post(`/publico/pesquisa/${token}`, { respostas }),
  /** Mesma pesquisa, entrando pelo histórico do app (já autenticado). */
  responderPesquisaDoAtendimento: (encounterId: string, respostas: Record<string, string>) =>
    http.post(`/auth/paciente/atendimentos/${encounterId}/pesquisa`, { respostas }),
  translados: () =>
    comCacheLocal('translados', () =>
      http.get<ViagemTransporte[]>('/auth/paciente/meus-translados').then((r) => r.data)),
  acompanhantes: () =>
    comCacheLocal('acompanhantes', () =>
      http.get<AcompanhanteCidadao[]>('/auth/paciente/me/acompanhantes').then((r) => r.data)),
  /** Confere CPF + nascimento e traz o nome para confirmar. Tem cota diária. */
  consultarAcompanhante: (cpf: string, dataNascimento: string) =>
    http
      .post<ConsultaAcompanhante>('/auth/paciente/me/acompanhantes/consulta', { cpf, dataNascimento })
      .then((r) => r.data),
  adicionarAcompanhante: (cpf: string, dataNascimento: string, parentesco: string | null) =>
    http
      .post<AcompanhanteCidadao>('/auth/paciente/me/acompanhantes', { cpf, dataNascimento, parentesco, telefone: null })
      .then((r) => r.data),
  removerAcompanhante: (id: string) => http.delete(`/auth/paciente/me/acompanhantes/${id}`),
  /** Viagens recentes para avaliar (sem cache: a nota muda na hora). */
  viagensParaAvaliar: () =>
    http.get<ViagemParaAvaliar[]>('/auth/paciente/viagens-para-avaliar').then((r) => r.data),
  /** O motorista de cada trecho (ida e resgate), uma vez, até 30 dias depois. Nota de 1 a 5. */
  avaliarViagem: (sessaoId: string, trecho: 'Ida' | 'Resgate', nota: number, comentario: string | null) =>
    http.post(`/auth/paciente/viagens/${sessaoId}/avaliacao`, { nota, comentario, trecho }),
  /** O resgate de hoje; nulo quando não há (ainda não foi à unidade, ou já voltou). Sem cache: muda na hora. */
  resgate: () =>
    http.get<ResgateDoPaciente>('/auth/paciente/resgate').then((r) => (r.status === 204 ? null : r.data)),
  /** Resposta à pergunta: sim = "já estou disponível"; não = "ainda não terminei" (volta em 10 min). */
  responderResgate: (sessaoId: string, disponivel: boolean) =>
    http.post(`/auth/paciente/resgate/${sessaoId}/resposta`, { disponivel }),
  atendimentos: () =>
    comCacheLocal('atendimentos', () =>
      http.get<Atendimento[]>('/auth/paciente/atendimentos').then((r) => r.data)),
  exames: () =>
    comCacheLocal('exames', () => http.get<Exame[]>('/auth/paciente/exames').then((r) => r.data)),
  laudos: () =>
    comCacheLocal('laudos', () => http.get<Laudo[]>('/auth/paciente/laudos').then((r) => r.data)),
  agendamentos: (tipo: 'consulta' | 'exame') =>
    comCacheLocal(`agendamentos.${tipo}`, () =>
      http.get<Agendamento[]>('/auth/paciente/agendamentos', { params: { tipo } }).then((r) => r.data)),
  agendamentoExame: (solicitacaoExameId: string) =>
    comCacheLocal(`agendamento.${solicitacaoExameId}`, () =>
      http
        .get<AgendamentoExameDetalhe>(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}`)
        .then((r) => r.data)),
  confirmarExame: (solicitacaoExameId: string) =>
    http.post(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/confirmar`),
  // Sem cache local de propósito: a chave é lida na hora (o back decide se é o dia).
  chaveAcessoExame: (solicitacaoExameId: string) =>
    http
      .post<ChaveAcessoExame>(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/chave-acesso`)
      .then((r) => r.data),
  cancelarExame: (solicitacaoExameId: string, motivo: string) =>
    http.post(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/cancelar`, { motivo }),

  // Acervo (menu Documentos). A LISTA tem cache offline como as demais; o envio nunca.
  documentos: () =>
    comCacheLocal('documentos', () =>
      http.get<ItemAcervo[]>('/auth/paciente/documentos').then((r) => r.data)),
  /**
   * Envia foto/PDF para o cadastro — entra Pendente até a equipe conferir. O cliente padrão
   * manda `Content-Type: application/json`; com FormData isso faria o axios serializar em
   * JSON, então forçamos multipart (o navegador completa com o `boundary`).
   */
  enviarDocumento: (
    arquivo: File,
    titulo: string,
    descricao: string | null,
    aoProgresso?: (fracao: number) => void,
  ) => {
    const form = new FormData();
    form.append('arquivo', arquivo, arquivo.name);
    form.append('titulo', titulo);
    if (descricao) form.append('descricao', descricao);
    return http
      .post<ItemAcervo>('/auth/paciente/documentos', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
        // Sinal fraco é a regra: o envio de uma foto pode levar um bom tempo — mostra o andamento.
        onUploadProgress: (e) => {
          if (aoProgresso && e.total) aoProgresso(e.loaded / e.total);
        },
      })
      .then((r) => r.data);
  },
  /** Retira um envio do próprio paciente que a equipe ainda não conferiu. */
  retirarDocumento: (id: string) => http.delete(`/auth/paciente/documentos/${id}`),
};

/**
 * URL do conteúdo de um item do acervo. Laudo, imagens e anexos da anamnese usam as MESMAS
 * URLs da tela Exames (mesmos ids, mesmo arquivo): assim reaproveitam o PDF que a sincronização
 * já deixou no aparelho e não geram de novo o PDF de imagens, que é pesado. Só o documento do
 * cadastro tem endpoint próprio.
 */
export function urlConteudoAcervo(item: Pick<ItemAcervo, 'tipo' | 'id'>): string {
  switch (item.tipo) {
    case 'Laudo':
      return pdfUrls.laudo(item.id);
    case 'ImagensExame':
      return pdfUrls.exameImagens(item.id);
    case 'AnexoExame':
      return pdfUrls.anexo(item.id);
    default:
      return `/auth/paciente/documentos/${item.tipo}/${item.id}/conteudo`;
  }
}

// URLs de PDF protegido (abertas/baixadas via blob — ver lib/pdf.ts).
export const pdfUrls = {
  // A guia do agendamento é um documento do acervo: mesma URL de Documentos, mesmo cache local.
  guia: (documentoId: string) => urlConteudoAcervo({ tipo: 'Documento', id: documentoId }),
  laudo: (laudoId: string) => `/auth/paciente/laudos/${laudoId}/pdf`,
  anexo: (anexoId: string) => `/auth/paciente/anexos/${anexoId}/conteudo`,
  exameImagens: (solicitacaoId: string) => `/auth/paciente/exames/${solicitacaoId}/imagens-pdf`,
};
