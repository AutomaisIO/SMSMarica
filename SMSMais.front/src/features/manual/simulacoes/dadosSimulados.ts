/**
 * Fichas de mentira da simulação de Confirmações.
 *
 * Nomes propositalmente genéricos e CPFs inválidos (terminados em 00) — ninguém pode confundir
 * uma linha do manual com um paciente de verdade, nem colar isso num cadastro.
 */
export type AbaSim = 'NaoConfirmados' | 'Confirmados' | 'ContatoErrado' | 'Pendentes' | 'TelefoneComprometido';

export type StatusEnvioSim = 'Lida' | 'Entregue' | 'Enviada' | 'Falhou' | 'NaFila' | 'NaoEnviada' | 'NumeroNegado' | 'AtendidaPorPessoa';

export type FichaSim = {
  id: string;
  nome: string;
  cpf: string;
  telefone: string;
  telefoneVerificado: boolean;
  procedimento: string;
  unidade: string;
  /** Dias a partir de hoje — a data é calculada na hora, para o exemplo nunca "vencer". */
  emDias: number;
  hora: string;
  envio: StatusEnvioSim;
  confirmado: boolean;
  /** Canal que confirmou, quando já confirmado. */
  canal?: string;
  cancelado?: boolean;
  respondeuNoZap?: boolean;
  motivoSemCanal?: 'SemCelular' | 'NaoEhWhatsApp';
  numeroNegado?: boolean;
  /** Posse humana: quem está com a ficha na mão. */
  posse?: { de: 'voce' | 'colega'; nome: string; desde: string; situacao: 'EmAtendimento' | 'Pendente' | 'ContatoErrado'; motivo?: string };
};

export const FICHAS_INICIAIS: FichaSim[] = [
  {
    id: 'f1',
    nome: 'Maria Aparecida (exemplo)',
    cpf: '000.000.000-00',
    telefone: '(21) 90000-0001',
    telefoneVerificado: true,
    procedimento: 'Ultrassonografia de abdome total',
    unidade: 'Policlínica Central (exemplo)',
    emDias: 1,
    hora: '08:20',
    envio: 'Lida',
    confirmado: false,
    respondeuNoZap: false,
  },
  {
    id: 'f2',
    nome: 'Antônio dos Santos (exemplo)',
    cpf: '000.000.000-00',
    telefone: '(21) 90000-0002',
    telefoneVerificado: false,
    procedimento: 'Consulta em cardiologia',
    unidade: 'Centro de Especialidades (exemplo)',
    emDias: 3,
    hora: '14:00',
    envio: 'Falhou',
    confirmado: false,
    motivoSemCanal: 'NaoEhWhatsApp',
  },
  {
    id: 'f3',
    nome: 'Joana Ferreira (exemplo)',
    cpf: '000.000.000-00',
    telefone: '(21) 90000-0003',
    telefoneVerificado: true,
    procedimento: 'Mamografia bilateral',
    unidade: 'Policlínica Central (exemplo)',
    emDias: 2,
    hora: '10:40',
    envio: 'Lida',
    confirmado: true,
    canal: 'link',
    respondeuNoZap: true,
  },
  {
    id: 'f4',
    nome: 'Pedro Henrique (exemplo)',
    cpf: '000.000.000-00',
    telefone: '(21) 90000-0004',
    telefoneVerificado: false,
    procedimento: 'Consulta em ortopedia',
    unidade: 'Centro de Especialidades (exemplo)',
    emDias: 5,
    hora: '09:00',
    envio: 'NumeroNegado',
    confirmado: false,
    numeroNegado: true,
    posse: { de: 'colega', nome: 'Rita (colega de exemplo)', desde: '09:12', situacao: 'ContatoErrado', motivo: 'atendeu outra pessoa' },
  },
];

/** Em que fila a ficha cai — a mesma derivação da tela real, em miniatura. */
export function abaDaFicha(f: FichaSim): AbaSim | null {
  if (f.cancelado) return null;
  if (f.numeroNegado || f.posse?.situacao === 'ContatoErrado') return 'ContatoErrado';
  if (f.confirmado) return 'Confirmados';
  if (f.posse?.situacao === 'Pendente') return 'Pendentes';
  if (f.motivoSemCanal) return 'TelefoneComprometido';
  return 'NaoConfirmados';
}

/** Data do exemplo, sempre no futuro: "hoje", "amanhã" ou a data mesmo. */
export function quandoDaFicha(f: FichaSim): { texto: string; ehHoje: boolean } {
  const d = new Date();
  d.setDate(d.getDate() + f.emDias);
  const data = d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
  const prefixo = f.emDias === 0 ? 'hoje' : f.emDias === 1 ? 'amanhã' : data;
  return { texto: `${data} ${f.hora}${f.emDias <= 1 ? ` · ${prefixo}` : ''}`, ehHoje: f.emDias === 0 };
}
