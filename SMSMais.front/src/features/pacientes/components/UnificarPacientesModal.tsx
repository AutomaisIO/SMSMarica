import { useState, type ReactNode } from 'react';
import { BadgeCheck, CheckCircle2, ShieldAlert } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { cn } from '@/shared/lib/cn';
import { formatarCpf } from '@/shared/lib/cpf';
import { Avatar } from '@/shared/ui/Avatar';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { formatarTelefoneBR } from '@/shared/ui/TelefoneCopiavel';
import {
  usePacientePorId,
  usePreverUnificacao,
  useUnificarPacientes,
} from '@/features/pacientes/api/queries';
import type {
  AtualizarPacientePayload,
  Paciente,
  ResultadoUnificacao,
} from '@/features/pacientes/types';

// ---- rótulos amigáveis dos enums ("NaoInformado" conta como vazio) ----
const SEXO: Record<string, string> = { Masculino: 'Masculino', Feminino: 'Feminino', Outro: 'Outro' };
const ESTADO_CIVIL: Record<string, string> = {
  Solteiro: 'Solteiro(a)', Casado: 'Casado(a)', UniaoEstavel: 'União estável',
  Divorciado: 'Divorciado(a)', Viuvo: 'Viúvo(a)', Separado: 'Separado(a)',
};
const RACA: Record<string, string> = {
  Branca: 'Branca', Preta: 'Preta', Parda: 'Parda', Amarela: 'Amarela', Indigena: 'Indígena',
};
const ESCOLARIDADE: Record<string, string> = {
  Analfabeto: 'Analfabeto', SemEscolaridade: 'Sem escolaridade',
  FundamentalIncompleto: 'Fundamental incompleto', FundamentalCompleto: 'Fundamental completo',
  MedioIncompleto: 'Médio incompleto', MedioCompleto: 'Médio completo',
  SuperiorIncompleto: 'Superior incompleto', SuperiorCompleto: 'Superior completo', PosGraduacao: 'Pós-graduação',
};
const SANGUE: Record<string, string> = { A: 'A', B: 'B', AB: 'AB', O: 'O' };
const RH: Record<string, string> = { Positivo: 'Positivo', Negativo: 'Negativo' };

const digitos = (v: string | null | undefined) => (v ?? '').replace(/\D/g, '');
const txt = (v: string | number | null | undefined) =>
  v === null || v === undefined ? '' : String(v).trim();
const data = (d: string | null | undefined) => (d ? d.split('T')[0].split('-').reverse().join('/') : '');

function enderecoTxt(p: Paciente): string {
  const e = p.endereco;
  if (!e || !(e.logradouro || e.bairro || e.cidade || e.cep)) return '';
  const partes = [
    [txt(e.logradouro), txt(e.numero)].filter(Boolean).join(', ') + (e.complemento ? ` - ${e.complemento}` : ''),
    txt(e.bairro),
    [txt(e.cidade), txt(e.uf)].filter(Boolean).join('/'),
    e.cep ? `CEP ${e.cep}` : '',
  ];
  return partes.filter(Boolean).join(', ');
}
function contatoEmergenciaTxt(p: Paciente): string {
  const c = p.contatoEmergencia;
  if (!c || !c.nome) return '';
  return `${c.nome}${c.parentesco ? ` (${c.parentesco})` : ''}${c.telefone ? ` ${c.telefone}` : ''}`;
}

/** Um item de uma linha itemizada (um telefone, uma alergia…). */
type Item = {
  /** Identidade do item entre os dois cadastros (mesmo número / mesmo texto = mesmo item). */
  chave: string;
  texto: string;
  /** O que vai ao servidor (dígitos do telefone; o texto, nas listas). */
  valor: string;
  /** Telefone verificado: fica sempre — não se desliga nem é sobrescrito. */
  verificado?: boolean;
  /** Telefone que alguém disse não ser do paciente. */
  negado?: boolean;
};

function telefones(p: Paciente): Item[] {
  // Mesmo número com e sem DDI/DDD é um só (os 8 últimos dígitos, como no servidor).
  const porChave = new Map<string, Item>();
  for (const t of [p.telefoneVerificado, p.telefonePrincipal, p.telefoneCelular, p.telefoneResidencial]) {
    const d = digitos(t);
    if (d.length < 8) continue;
    const chave = d.slice(-8);
    if (porChave.has(chave)) continue;
    porChave.set(chave, { chave, texto: formatarTelefoneBR(t ?? ''), valor: d });
  }
  const marcar = (num: string | null, f: (i: Item) => void) => {
    const d = digitos(num);
    const item = d.length >= 8 ? porChave.get(d.slice(-8)) : undefined;
    if (item) f(item);
  };
  marcar(p.telefoneVerificado, (i) => { i.verificado = true; });
  marcar(p.telefoneNegado, (i) => { i.negado = true; });
  return [...porChave.values()];
}

const itensDeLista = (f: (p: Paciente) => string[] | null | undefined) => (p: Paciente): Item[] => {
  const vistos = new Set<string>();
  return (f(p) ?? [])
    .map((x) => x.trim())
    .filter(Boolean)
    .filter((x) => {
      const k = x.toLowerCase();
      if (vistos.has(k)) return false;
      vistos.add(k);
      return true;
    })
    .map((x) => ({ chave: x.toLowerCase(), texto: x, valor: x }));
};

/**
 * Como cada dado se resolve:
 * - `um`: fica o valor de um dos dois (clique escolhe);
 * - `itens`: telefones e listas clínicas — cada item (um número, uma alergia) liga e desliga
 *   sozinho; o que está nos dois aparece uma vez; tudo começa ligado;
 * - `soma`: e-mail — os dos dois ficam, não se escolhe.
 */
type Tipo = 'um' | 'itens' | 'soma';
type Lado = 'a' | 'b';
type Linha = {
  /** Nome do campo em Paciente/AtualizarPacientePayload (ou chave própria, p/ telefones). */
  chave: string;
  rotulo: string;
  tipo: Tipo;
  /** Texto exibido; vazio = cadastro sem o dado. */
  texto: (p: Paciente) => string;
  /** Itens (tipo `itens`). */
  itens?: (p: Paciente) => Item[];
};

const LISTAS_CLINICAS = [
  ['alergias', 'Alergias', (p: Paciente) => p.alergias],
  ['medicamentosContinuos', 'Medicamentos contínuos', (p: Paciente) => p.medicamentosContinuos],
  ['comorbidades', 'Comorbidades', (p: Paciente) => p.comorbidades],
  ['deficiencias', 'Deficiências', (p: Paciente) => p.deficiencias],
] as const;

const GRUPOS: { titulo: string; linhas: Linha[] }[] = [
  {
    titulo: 'Identificação',
    linhas: [
      { chave: 'nomeCompleto', rotulo: 'Nome', tipo: 'um', texto: (p) => txt(p.nomeCompleto) },
      { chave: 'nomeSocial', rotulo: 'Nome social', tipo: 'um', texto: (p) => txt(p.nomeSocial) },
      { chave: 'cpf', rotulo: 'CPF', tipo: 'um', texto: (p) => (digitos(p.cpf) ? formatarCpf(p.cpf) : '') },
      { chave: 'cns', rotulo: 'CNS', tipo: 'um', texto: (p) => digitos(p.cns) },
      { chave: 'dataNascimento', rotulo: 'Nascimento', tipo: 'um', texto: (p) => data(p.dataNascimento) },
      { chave: 'rg', rotulo: 'RG', tipo: 'um', texto: (p) => txt(p.rg) },
      { chave: 'sexo', rotulo: 'Sexo', tipo: 'um', texto: (p) => SEXO[p.sexo] ?? '' },
      { chave: 'nomeDaMae', rotulo: 'Mãe', tipo: 'um', texto: (p) => txt(p.nomeDaMae) },
      { chave: 'nomeDoPai', rotulo: 'Pai', tipo: 'um', texto: (p) => txt(p.nomeDoPai) },
      { chave: 'responsavelLegal', rotulo: 'Responsável legal', tipo: 'um', texto: (p) => txt(p.responsavelLegal) },
    ],
  },
  {
    titulo: 'Dados pessoais',
    linhas: [
      { chave: 'estadoCivil', rotulo: 'Estado civil', tipo: 'um', texto: (p) => ESTADO_CIVIL[p.estadoCivil] ?? '' },
      { chave: 'racaCor', rotulo: 'Raça/cor', tipo: 'um', texto: (p) => RACA[p.racaCor] ?? '' },
      { chave: 'escolaridade', rotulo: 'Escolaridade', tipo: 'um', texto: (p) => ESCOLARIDADE[p.escolaridade] ?? '' },
      { chave: 'ocupacao', rotulo: 'Ocupação', tipo: 'um', texto: (p) => txt(p.ocupacao) },
      { chave: 'naturalidade', rotulo: 'Naturalidade', tipo: 'um', texto: (p) => txt(p.naturalidade) },
      { chave: 'nacionalidade', rotulo: 'Nacionalidade', tipo: 'um', texto: (p) => txt(p.nacionalidade) },
    ],
  },
  {
    titulo: 'Endereço e contato',
    linhas: [
      { chave: 'endereco', rotulo: 'Endereço', tipo: 'um', texto: enderecoTxt },
      {
        chave: 'telefones', rotulo: 'Telefones', tipo: 'itens',
        itens: telefones, texto: (p) => telefones(p).map((i) => i.texto).join('\n'),
      },
      { chave: 'email', rotulo: 'E-mail', tipo: 'soma', texto: (p) => txt(p.email) },
      { chave: 'contatoEmergencia', rotulo: 'Contato de emergência', tipo: 'um', texto: contatoEmergenciaTxt },
    ],
  },
  {
    titulo: 'Saúde',
    linhas: [
      { chave: 'alturaCm', rotulo: 'Altura (cm)', tipo: 'um', texto: (p) => txt(p.alturaCm) },
      { chave: 'pesoKg', rotulo: 'Peso (kg)', tipo: 'um', texto: (p) => txt(p.pesoKg) },
      { chave: 'tipoSanguineo', rotulo: 'Tipo sanguíneo', tipo: 'um', texto: (p) => SANGUE[p.tipoSanguineo] ?? '' },
      { chave: 'fatorRh', rotulo: 'Fator Rh', tipo: 'um', texto: (p) => RH[p.fatorRh] ?? '' },
      ...LISTAS_CLINICAS.map(([chave, rotulo, f]): Linha => {
        const itens = itensDeLista(f);
        return { chave, rotulo, tipo: 'itens', itens, texto: (p) => itens(p).map((i) => i.texto).join(', ') };
      }),
      { chave: 'planoSaude', rotulo: 'Plano de saúde', tipo: 'um', texto: (p) => txt(p.planoSaude) },
      { chave: 'observacoes', rotulo: 'Observações', tipo: 'um', texto: (p) => txt(p.observacoes) },
    ],
  },
];
const LINHAS = GRUPOS.flatMap((g) => g.linhas);

/** Os itens de uma linha `itens` dos dois cadastros juntos: os do 1º na ordem dele, depois os só do 2º. */
type ItemUnido = Item & { emA: boolean; emB: boolean };
function unir(l: Linha, a: Paciente, b: Paciente): ItemUnido[] {
  const daA = l.itens!(a);
  const daB = l.itens!(b);
  const chavesB = new Set(daB.map((i) => i.chave));
  const chavesA = new Set(daA.map((i) => i.chave));
  return [
    ...daA.map((i) => {
      const outro = daB.find((x) => x.chave === i.chave);
      return {
        ...i,
        verificado: i.verificado || outro?.verificado,
        negado: i.negado || outro?.negado,
        emA: true,
        emB: chavesB.has(i.chave),
      };
    }),
    ...daB.filter((i) => !chavesA.has(i.chave)).map((i) => ({ ...i, emA: false, emB: true })),
  ];
}

function toPayload(p: Paciente): AtualizarPacientePayload {
  return {
    nomeSocial: p.nomeSocial, cns: p.cns, rg: p.rg, sexo: p.sexo, estadoCivil: p.estadoCivil,
    racaCor: p.racaCor, escolaridade: p.escolaridade, ocupacao: p.ocupacao, naturalidade: p.naturalidade,
    nacionalidade: p.nacionalidade, nomeDaMae: p.nomeDaMae, nomeDoPai: p.nomeDoPai,
    responsavelLegal: p.responsavelLegal, endereco: p.endereco, telefonePrincipal: p.telefonePrincipal,
    telefoneCelular: p.telefoneCelular, telefoneResidencial: p.telefoneResidencial, email: p.email,
    contatoEmergencia: p.contatoEmergencia, alturaCm: p.alturaCm, pesoKg: p.pesoKg,
    tipoSanguineo: p.tipoSanguineo, fatorRh: p.fatorRh, alergias: p.alergias,
    medicamentosContinuos: p.medicamentosContinuos, comorbidades: p.comorbidades,
    deficiencias: p.deficiencias, planoSaude: p.planoSaude, observacoes: p.observacoes,
    fotoBase64: p.fotoBase64,
  };
}

/**
 * Uma célula de dado: verde com borda e sombra = fica; esmaecida = não fica. Exportada para a
 * simulação do Manual usar a MESMA peça da tela.
 */
export function CelulaUnificacao({
  texto, escolhido, clicavel, aoClicar, largo, selo, titulo,
}: {
  texto: string;
  escolhido: boolean;
  clicavel: boolean;
  aoClicar?: () => void;
  largo?: boolean;
  /** Marca à direita do texto (ex.: "verificado"). */
  selo?: ReactNode;
  titulo?: string;
}) {
  return (
    <button
      type="button"
      onClick={clicavel ? aoClicar : undefined}
      aria-pressed={escolhido}
      tabIndex={clicavel ? 0 : -1}
      title={titulo}
      className={cn(
        'flex min-h-[2.5rem] w-full items-center justify-between gap-2 rounded-lg border px-3 py-2 text-left text-sm transition',
        largo && 'col-span-2',
        escolhido
          ? 'border-green-500 bg-white text-gray-900 shadow-md shadow-green-500/30 ring-2 ring-green-500/30'
          : 'border-gray-200 bg-gray-50 text-gray-500 opacity-50',
        clicavel ? 'cursor-pointer hover:opacity-80' : 'cursor-default',
        clicavel && escolhido && 'hover:opacity-100',
      )}
    >
      <span className="block min-w-0 whitespace-pre-line break-words">{texto || '—'}</span>
      {selo}
    </button>
  );
}

function SeloItem({ item }: { item: Item }) {
  if (item.verificado) {
    return (
      <span className="inline-flex shrink-0 items-center gap-1 rounded bg-green-50 px-1.5 py-0.5 text-[11px] font-medium text-green-800">
        <BadgeCheck className="h-3.5 w-3.5" /> verificado
      </span>
    );
  }
  if (item.negado) {
    return (
      <span className="shrink-0 rounded bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-800">
        marcado como errado
      </span>
    );
  }
  return null;
}

function Cabecalho({ p }: { p: Paciente }) {
  return (
    <div className="flex min-w-0 items-center gap-2 rounded-lg bg-gray-50 px-3 py-2">
      <Avatar src={p.fotoBase64} nome={p.nomeCompleto} tamanho="sm" />
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-gray-900">{p.nomeCompleto}</p>
        <p className="truncate text-xs text-gray-500">
          {digitos(p.cpf) ? `CPF ${formatarCpf(p.cpf)}` : 'sem CPF'}
          {p.dataNascimento ? ` · ${data(p.dataNascimento)}` : ''}
          {p.ativo ? '' : ' · desativado'}
        </p>
      </div>
    </div>
  );
}

type Props = {
  /** Os dois cadastros marcados na lista, na ordem em que foram marcados. */
  ids: [string, string];
  aoFechar: () => void;
  /** Depois de unificar, quando a pessoa fecha o resultado. */
  aoConcluir: () => void;
};

/**
 * Modal de unificação: os dois cadastros lado a lado, dado por dado. Só dois estados visuais —
 * verde (fica) e esmaecido (não fica). O padrão já vem resolvido: dado que só um cadastro tem,
 * fica; telefones e itens das listas clínicas começam todos ligados, um a um; o operador só clica
 * onde discorda. Telefone verificado fica sempre (não se desliga nem é sobrescrito).
 *
 * <p>Qual cadastro "carrega" o resultado (o id que sobrevive) não aparece na tela: é o dono do
 * CPF escolhido (o CPF não se troca por aqui) e, sem CPF para decidir, o primeiro marcado. Tudo do
 * outro — laudos, agendamentos, solicitações, conversas, acervo, histórico clínico — vem junto.</p>
 */
export function UnificarPacientesModal({ ids, aoFechar, aoConcluir }: Props) {
  const navigate = useNavigate();
  const qA = usePacientePorId(ids[0]);
  const qB = usePacientePorId(ids[1]);
  const A = qA.data ?? null;
  const B = qB.data ?? null;

  /** Escolha de lado nas linhas `um` que o operador clicou. */
  const [toques, setToques] = useState<Record<string, Lado>>({});
  /** Itens desligados nas linhas `itens`, como `linha:item`. */
  const [desligados, setDesligados] = useState<Set<string>>(() => new Set());
  const [confirmaMesmaPessoa, setConfirmaMesmaPessoa] = useState(false);
  const [resultado, setResultado] = useState<ResultadoUnificacao | null>(null);
  const unificar = useUnificarPacientes();

  const de = (lado: Lado) => (lado === 'a' ? A : B)!;
  const preenchidos = (l: Linha): Lado[] =>
    A && B ? (['a', 'b'] as Lado[]).filter((x) => l.texto(de(x)) !== '') : [];
  const iguais = (l: Linha) => Boolean(A && B && l.texto(A) === l.texto(B));

  // O cadastro que carrega o resultado: o do CPF escolhido; sem CPF que decida, o 1º marcado.
  const linhaCpf = LINHAS.find((l) => l.chave === 'cpf')!;
  const principal: Lado = !iguais(linhaCpf)
    ? toques.cpf ?? preenchidos(linhaCpf)[0] ?? 'a'
    : 'a';
  const outro: Lado = principal === 'a' ? 'b' : 'a';

  /** Lado que fica numa linha `um`/`soma` (o que a tela pinta de verde). */
  function escolhidos(l: Linha): Lado[] {
    const cheios = preenchidos(l);
    if (l.tipo === 'soma') return cheios;
    if (iguais(l)) return [principal];
    if (toques[l.chave]) return [toques[l.chave]];
    return cheios.length === 1 ? cheios : [principal];
  }

  const ligado = (l: Linha, i: Item) => i.verificado || !desligados.has(`${l.chave}:${i.chave}`);

  function alternarItem(l: Linha, i: Item, todos: ItemUnido[]) {
    if (i.verificado) return;
    const k = `${l.chave}:${i.chave}`;
    // Telefone: sempre sobra pelo menos um — um número a menos é uma pessoa sem aviso.
    if (l.chave === 'telefones' && ligado(l, i) && todos.filter((x) => ligado(l, x)).length === 1) return;
    setDesligados((s) => {
      const novo = new Set(s);
      if (novo.has(k)) novo.delete(k);
      else novo.add(k);
      return novo;
    });
  }

  const principalId = A && B ? de(principal).id : null;
  const outroId = A && B ? de(outro).id : null;
  const previa = usePreverUnificacao(principalId, outroId);

  const cpfDiverge = Boolean(A && B && digitos(A.cpf) && digitos(B.cpf) && digitos(A.cpf) !== digitos(B.cpf));
  const cnsDiverge = Boolean(A && B && digitos(A.cns) && digitos(B.cns) && digitos(A.cns) !== digitos(B.cns));
  const chavesDivergem = cpfDiverge || cnsDiverge;

  const carregando = qA.isLoading || qB.isLoading;
  const erroCarga = qA.error ?? qB.error ?? previa.error;
  const podeAplicar = Boolean(
    A && B && !erroCarga && !previa.isLoading && (!chavesDivergem || confirmaMesmaPessoa) && !unificar.isPending,
  );
  const vemJunto = previa.data?.referencias ?? [];

  async function aplicar() {
    if (!A || !B) return;
    const P = de(principal);
    const O = de(outro);
    const valorDe = (chave: string) => de(escolhidos(LINHAS.find((x) => x.chave === chave)!)[0]);

    const dados = toPayload(P) as Record<string, unknown>;
    let telefonesDescartados: string[] = [];
    for (const l of LINHAS) {
      if (l.tipo === 'soma' || ['nomeCompleto', 'cpf', 'cns', 'dataNascimento'].includes(l.chave)) continue;
      if (l.tipo === 'itens') {
        const todos = unir(l, A, B);
        if (l.chave === 'telefones') {
          // Telefones vão como "desligados": o servidor soma o resto, inclusive o que a tela não mostra.
          telefonesDescartados = todos.filter((i) => !ligado(l, i)).map((i) => i.valor);
        } else {
          dados[l.chave] = todos.filter((i) => ligado(l, i)).map((i) => i.valor);
        }
        continue;
      }
      dados[l.chave] = (valorDe(l.chave) as unknown as Record<string, unknown>)[l.chave];
    }
    dados.fotoBase64 = P.fotoBase64 ?? O.fotoBase64;

    const nome = valorDe('nomeCompleto').nomeCompleto;
    const cns = digitos(valorDe('cns').cns);
    const nascimento = valorDe('dataNascimento').dataNascimento;
    try {
      const r = await unificar.mutateAsync({
        sobreviventeId: P.id,
        absorvidoId: O.id,
        dadosFinais: dados as AtualizarPacientePayload,
        nomeFinal: nome !== P.nomeCompleto ? nome : null,
        cnsFinal: cns || null,
        nascimentoFinal: nascimento ? nascimento.split('T')[0] : null,
        confirmaChavesDivergentes: confirmaMesmaPessoa,
        telefonesDescartados,
      });
      setResultado(r);
    } catch {
      // A mensagem aparece no rodapé (unificar.error).
    }
  }

  // ---- desfecho: fica no modal até a pessoa fechar ----
  if (resultado) {
    return (
      <Modal aberto aoFechar={aoConcluir} titulo="Unificar cadastros" largura="md">
        <div className="space-y-4 text-center">
          <CheckCircle2 className="mx-auto h-10 w-10 text-green-600" />
          <p className="text-lg font-semibold text-gray-900">Cadastros unificados</p>
          <ul className="space-y-1 text-sm text-gray-700">
            <li>
              <strong>{resultado.referenciasRepontadas}</strong> registros do painel vieram junto
              (laudos, agendamentos, solicitações, conversas, acervo…).
            </li>
            <li>
              <strong>{resultado.clinicoRepontado}</strong> registros do histórico clínico vieram junto.
            </li>
            {resultado.contatosSomados > 0 ? (
              <li>
                <strong>{resultado.contatosSomados}</strong> telefone(s)/e-mail(s) do outro cadastro entraram.
              </li>
            ) : null}
            {resultado.telefonesRetirados > 0 ? (
              <li>
                <strong>{resultado.telefonesRetirados}</strong> telefone(s) desligado(s) ficaram só no histórico.
              </li>
            ) : null}
          </ul>
          <p className="text-xs text-gray-500">
            O outro cadastro foi desativado e aponta para este. Fica registrado no Histórico de
            alterações dos dois.
          </p>
          <div className="flex justify-center gap-3 pt-2">
            <Button onClick={() => navigate(`/app/pacientes/${resultado.sobreviventeId}`)}>Ver cadastro</Button>
            <Button variante="outline" onClick={aoConcluir}>Fechar</Button>
          </div>
        </div>
      </Modal>
    );
  }

  return (
    <Modal
      aberto
      aoFechar={() => { if (!unificar.isPending) aoFechar(); }}
      titulo="Unificar cadastros"
      descricao="Clique no dado que deve ficar. O que está em verde vai para o cadastro unificado."
      largura="xl"
    >
      {carregando ? (
        <p className="py-8 text-center text-sm text-gray-500">Carregando os dois cadastros…</p>
      ) : !A || !B ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroCarga ? extrairMensagemDeErro(erroCarga) : 'Não foi possível carregar os cadastros.'}
        </div>
      ) : (
        <div className="space-y-5">
          {previa.isError ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {extrairMensagemDeErro(previa.error)}
            </div>
          ) : null}

          {chavesDivergem ? (
            <div className="rounded-lg border-2 border-red-300 bg-red-50 p-4">
              <div className="flex items-start gap-3">
                <ShieldAlert className="mt-0.5 h-5 w-5 shrink-0 text-red-600" />
                <div>
                  <p className="font-medium text-red-800">
                    Os dois cadastros têm {cpfDiverge ? 'CPF' : ''}
                    {cpfDiverge && cnsDiverge ? ' e ' : ''}
                    {cnsDiverge ? 'CNS' : ''} diferentes.
                  </p>
                  <p className="mt-1 text-sm text-red-700">
                    Podem ser <strong>pessoas diferentes</strong>. Unir mistura dois prontuários e não
                    se desfaz num clique.
                  </p>
                  <label className="mt-3 flex items-center gap-2 text-sm font-medium text-red-800">
                    <input
                      type="checkbox"
                      checked={confirmaMesmaPessoa}
                      onChange={(e) => setConfirmaMesmaPessoa(e.target.checked)}
                      className="h-4 w-4 rounded border-red-300"
                    />
                    Confirmo que é a mesma pessoa.
                  </label>
                </div>
              </div>
            </div>
          ) : null}

          <div className="grid grid-cols-[7.5rem_minmax(0,1fr)_minmax(0,1fr)] items-start gap-x-3 gap-y-2">
            <div />
            <Cabecalho p={A} />
            <Cabecalho p={B} />

            {GRUPOS.map((g) => {
              const visiveis = g.linhas.filter((l) => preenchidos(l).length > 0);
              if (visiveis.length === 0) return null;
              return (
                <div key={g.titulo} className="contents">
                  <p className="col-span-3 mt-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
                    {g.titulo}
                  </p>
                  {visiveis.map((l) => {
                    if (l.tipo === 'itens') {
                      const todos = unir(l, A, B);
                      return todos.map((i, n) => {
                        const celula = (
                          <CelulaUnificacao
                            texto={i.texto}
                            escolhido={ligado(l, i)}
                            clicavel={!i.verificado}
                            aoClicar={() => alternarItem(l, i, todos)}
                            largo={i.emA && i.emB}
                            selo={<SeloItem item={i} />}
                            titulo={i.verificado ? 'Telefone verificado: fica sempre.' : undefined}
                          />
                        );
                        return (
                          <div key={`${l.chave}:${i.chave}`} className="contents">
                            <span className="pt-2.5 text-xs text-gray-500">{n === 0 ? l.rotulo : ''}</span>
                            {i.emA && i.emB ? celula : (
                              <>
                                {i.emA ? celula : <div />}
                                {i.emB ? celula : <div />}
                              </>
                            )}
                          </div>
                        );
                      });
                    }
                    const ficam = escolhidos(l);
                    const cheios = preenchidos(l);
                    return (
                      <div key={l.chave} className="contents">
                        <span className="pt-2.5 text-xs text-gray-500">{l.rotulo}</span>
                        {iguais(l) ? (
                          <CelulaUnificacao texto={l.texto(A)} escolhido clicavel={false} largo />
                        ) : (
                          (['a', 'b'] as Lado[]).map((lado) => (
                            <CelulaUnificacao
                              key={lado}
                              texto={l.texto(de(lado))}
                              escolhido={ficam.includes(lado)}
                              clicavel={l.tipo === 'um' && cheios.includes(lado)}
                              aoClicar={() => setToques((t) => ({ ...t, [l.chave]: lado }))}
                            />
                          ))
                        )}
                      </div>
                    );
                  })}
                </div>
              );
            })}
          </div>

          <div className="space-y-3 border-t border-gray-200 pt-4">
            <p className="text-sm text-gray-600">
              {previa.isLoading ? (
                'Conferindo o que vem junto…'
              ) : vemJunto.length > 0 ? (
                <>
                  Vem junto:{' '}
                  {vemJunto.map((r, i) => (
                    <span key={r.modulo}>
                      {i > 0 ? ' · ' : ''}
                      {r.modulo} <strong>{r.quantidade}</strong>
                    </span>
                  ))}
                  {' '}— e todo o histórico clínico.
                </>
              ) : (
                'Vem junto todo o histórico clínico.'
              )}
            </p>
            {unificar.isError ? (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {extrairMensagemDeErro(unificar.error)}
              </div>
            ) : null}
            <div className="flex justify-end gap-3">
              <Button variante="outline" onClick={aoFechar} disabled={unificar.isPending}>
                Cancelar
              </Button>
              <Button onClick={aplicar} disabled={!podeAplicar}>
                {unificar.isPending ? 'Unificando…' : 'Aplicar'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}
