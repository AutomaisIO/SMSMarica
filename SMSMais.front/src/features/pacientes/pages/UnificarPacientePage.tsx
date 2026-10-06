import { useMemo, useState } from 'react';
import { ArrowLeftRight, CheckCircle2, Merge, ShieldAlert, X } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Avatar } from '@/shared/ui/Avatar';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import {
  usePacientePorId,
  usePreverUnificacao,
  useUnificarPacientes,
} from '@/features/pacientes/api/queries';
import type {
  AtualizarPacientePayload,
  Paciente,
  PacienteListItem,
  ResultadoUnificacao,
} from '@/features/pacientes/types';

// ---- rótulos amigáveis dos enums (só os visíveis nesta tela) ----
const SEXO: Record<string, string> = {
  NaoInformado: '—', Masculino: 'Masculino', Feminino: 'Feminino', Outro: 'Outro',
};
const ESTADO_CIVIL: Record<string, string> = {
  NaoInformado: '—', Solteiro: 'Solteiro(a)', Casado: 'Casado(a)', UniaoEstavel: 'União estável',
  Divorciado: 'Divorciado(a)', Viuvo: 'Viúvo(a)', Separado: 'Separado(a)',
};
const RACA: Record<string, string> = {
  NaoInformado: '—', Branca: 'Branca', Preta: 'Preta', Parda: 'Parda', Amarela: 'Amarela', Indigena: 'Indígena',
};
const ESCOLARIDADE: Record<string, string> = {
  NaoInformado: '—', Analfabeto: 'Analfabeto', SemEscolaridade: 'Sem escolaridade',
  FundamentalIncompleto: 'Fundamental incompleto', FundamentalCompleto: 'Fundamental completo',
  MedioIncompleto: 'Médio incompleto', MedioCompleto: 'Médio completo',
  SuperiorIncompleto: 'Superior incompleto', SuperiorCompleto: 'Superior completo', PosGraduacao: 'Pós-graduação',
};
const SANGUE: Record<string, string> = {
  NaoInformado: '—', A: 'A', B: 'B', AB: 'AB', O: 'O',
};
const RH: Record<string, string> = { NaoInformado: '—', Positivo: 'positivo', Negativo: 'negativo' };

function cpfFmt(cpf: string | null | undefined): string {
  const d = (cpf ?? '').replace(/\D/g, '');
  if (d.length !== 11) return cpf || '—';
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}
function dataFmt(d: string | null | undefined): string {
  if (!d) return '—';
  return d.split('T')[0].split('-').reverse().join('/');
}
function enderecoFmt(p: Paciente): string {
  const e = p.endereco;
  if (!e) return '—';
  const n = (v?: string | null) => (v ?? '').trim();
  const num = e.numero ? `, ${e.numero}` : '';
  const comp = e.complemento ? ` - ${e.complemento}` : '';
  const cep = e.cep ? ` CEP ${e.cep}` : '';
  return `${n(e.logradouro)}${num}${comp}, ${n(e.bairro)}, ${n(e.cidade)}/${n(e.uf)}${cep}`.trim();
}
function contatoFmt(p: Paciente): string {
  const c = p.contatoEmergencia;
  if (!c) return '—';
  const par = c.parentesco ? ` (${c.parentesco})` : '';
  return `${c.nome}${par} ${c.telefone ?? ''}`.trim();
}
function lista(l: string[] | null | undefined): string {
  return l && l.length > 0 ? l.join(', ') : '—';
}
function texto(v: string | number | null | undefined): string {
  return v === null || v === undefined || v === '' ? '—' : String(v);
}

type CampoChave = keyof AtualizarPacientePayload | 'nomeCompleto';
type Campo = { chave: CampoChave; rotulo: string; fmt: (p: Paciente) => string };

/**
 * Campos resolvíveis na tela (o operador escolhe qual valor "fica"). CPF, CNS e nascimento NÃO
 * entram: são identidade — o sobrevivente mantém as suas chaves nacionais como primárias e o
 * $merge do hub absorve as do outro como secundárias (nada se perde).
 */
const CAMPOS: Campo[] = [
  { chave: 'nomeCompleto', rotulo: 'Nome completo', fmt: (p) => texto(p.nomeCompleto) },
  { chave: 'nomeSocial', rotulo: 'Nome social', fmt: (p) => texto(p.nomeSocial) },
  { chave: 'rg', rotulo: 'RG', fmt: (p) => texto(p.rg) },
  { chave: 'sexo', rotulo: 'Sexo', fmt: (p) => SEXO[p.sexo] ?? p.sexo },
  { chave: 'estadoCivil', rotulo: 'Estado civil', fmt: (p) => ESTADO_CIVIL[p.estadoCivil] ?? p.estadoCivil },
  { chave: 'racaCor', rotulo: 'Raça/cor', fmt: (p) => RACA[p.racaCor] ?? p.racaCor },
  { chave: 'escolaridade', rotulo: 'Escolaridade', fmt: (p) => ESCOLARIDADE[p.escolaridade] ?? p.escolaridade },
  { chave: 'ocupacao', rotulo: 'Ocupação', fmt: (p) => texto(p.ocupacao) },
  { chave: 'naturalidade', rotulo: 'Naturalidade', fmt: (p) => texto(p.naturalidade) },
  { chave: 'nacionalidade', rotulo: 'Nacionalidade', fmt: (p) => texto(p.nacionalidade) },
  { chave: 'nomeDaMae', rotulo: 'Nome da mãe', fmt: (p) => texto(p.nomeDaMae) },
  { chave: 'nomeDoPai', rotulo: 'Nome do pai', fmt: (p) => texto(p.nomeDoPai) },
  { chave: 'responsavelLegal', rotulo: 'Responsável legal', fmt: (p) => texto(p.responsavelLegal) },
  { chave: 'endereco', rotulo: 'Endereço', fmt: enderecoFmt },
  { chave: 'telefonePrincipal', rotulo: 'Telefone principal', fmt: (p) => texto(p.telefonePrincipal) },
  { chave: 'telefoneCelular', rotulo: 'Telefone celular', fmt: (p) => texto(p.telefoneCelular) },
  { chave: 'telefoneResidencial', rotulo: 'Telefone residencial', fmt: (p) => texto(p.telefoneResidencial) },
  { chave: 'email', rotulo: 'E-mail', fmt: (p) => texto(p.email) },
  { chave: 'contatoEmergencia', rotulo: 'Contato de emergência', fmt: contatoFmt },
  { chave: 'alturaCm', rotulo: 'Altura (cm)', fmt: (p) => texto(p.alturaCm) },
  { chave: 'pesoKg', rotulo: 'Peso (kg)', fmt: (p) => texto(p.pesoKg) },
  { chave: 'tipoSanguineo', rotulo: 'Tipo sanguíneo', fmt: (p) => SANGUE[p.tipoSanguineo] ?? p.tipoSanguineo },
  { chave: 'fatorRh', rotulo: 'Fator Rh', fmt: (p) => RH[p.fatorRh] ?? p.fatorRh },
  { chave: 'alergias', rotulo: 'Alergias', fmt: (p) => lista(p.alergias) },
  { chave: 'medicamentosContinuos', rotulo: 'Medicamentos contínuos', fmt: (p) => lista(p.medicamentosContinuos) },
  { chave: 'comorbidades', rotulo: 'Comorbidades', fmt: (p) => lista(p.comorbidades) },
  { chave: 'deficiencias', rotulo: 'Deficiências', fmt: (p) => lista(p.deficiencias) },
  { chave: 'planoSaude', rotulo: 'Plano de saúde', fmt: (p) => texto(p.planoSaude) },
  { chave: 'observacoes', rotulo: 'Observações', fmt: (p) => texto(p.observacoes) },
];

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

type Lado = 'sobrevivente' | 'absorvido';

function CartaoSelecionado({
  p, rotulo, cor, aoRemover,
}: {
  p: Paciente; rotulo: string; cor: string; aoRemover: () => void;
}) {
  return (
    <div className={`rounded-lg border-2 ${cor} bg-white p-4`}>
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-gray-500">{rotulo}</span>
        <button type="button" onClick={aoRemover} className="text-gray-400 hover:text-gray-700" aria-label="Remover">
          <X className="h-4 w-4" />
        </button>
      </div>
      <div className="flex items-center gap-3">
        <Avatar src={p.fotoBase64} nome={p.nomeCompleto} tamanho="md" />
        <div className="min-w-0">
          <p className="truncate font-medium text-gray-900">{p.nomeCompleto}</p>
          <p className="truncate text-xs text-gray-500">
            CPF {cpfFmt(p.cpf)}{p.cns ? ` · CNS ${p.cns}` : ''} · nasc. {dataFmt(p.dataNascimento)}
          </p>
        </div>
      </div>
    </div>
  );
}

export function UnificarPacientePage() {
  const navigate = useNavigate();
  const [sobreviventeId, setSobreviventeId] = useState<string | null>(null);
  const [absorvidoId, setAbsorvidoId] = useState<string | null>(null);
  const [resolucao, setResolucao] = useState<Record<string, Lado>>({});
  const [confirmaChaves, setConfirmaChaves] = useState(false);
  const [dialogAberto, setDialogAberto] = useState(false);
  const [resultado, setResultado] = useState<ResultadoUnificacao | null>(null);

  const qA = usePacientePorId(sobreviventeId);
  const qB = usePacientePorId(absorvidoId);
  const previa = usePreverUnificacao(sobreviventeId, absorvidoId);
  const unificar = useUnificarPacientes();

  const a = qA.data ?? null;
  const b = qB.data ?? null;
  const mesmoCadastro = Boolean(sobreviventeId && sobreviventeId === absorvidoId);

  const divergentes = useMemo(() => {
    if (!a || !b) return [];
    return CAMPOS.filter((c) => c.fmt(a) !== c.fmt(b));
  }, [a, b]);

  const chavesDivergem = Boolean(previa.data?.cpfDivergente || previa.data?.cnsDivergente);
  const prontoParaConfirmar = Boolean(a && b && !mesmoCadastro && (!chavesDivergem || confirmaChaves));

  function escolher(chave: string, lado: Lado) {
    setResolucao((r) => ({ ...r, [chave]: lado }));
  }
  function ladoDe(chave: string): Lado {
    return resolucao[chave] ?? 'sobrevivente';
  }

  function trocarLados() {
    setSobreviventeId(absorvidoId);
    setAbsorvidoId(sobreviventeId);
    setResolucao({});
    setConfirmaChaves(false);
  }

  function reiniciar() {
    setSobreviventeId(null);
    setAbsorvidoId(null);
    setResolucao({});
    setConfirmaChaves(false);
    setResultado(null);
    unificar.reset();
  }

  async function confirmar() {
    if (!a || !b) return;
    const dados = toPayload(a);
    let nomeFinal: string | undefined;
    for (const c of divergentes) {
      if (ladoDe(c.chave) !== 'absorvido') continue;
      if (c.chave === 'nomeCompleto') nomeFinal = b.nomeCompleto;
      else (dados as Record<string, unknown>)[c.chave] = (b as Record<string, unknown>)[c.chave];
    }
    const r = await unificar.mutateAsync({
      sobreviventeId: a.id,
      absorvidoId: b.id,
      dadosFinais: dados,
      nomeFinal,
      confirmaChavesDivergentes: confirmaChaves,
    });
    setResultado(r);
    setDialogAberto(false);
  }

  // ---- sucesso ----
  if (resultado) {
    return (
      <div className="mx-auto max-w-2xl space-y-6">
        <div className="rounded-lg border border-green-200 bg-green-50 p-6 text-center">
          <CheckCircle2 className="mx-auto mb-3 h-10 w-10 text-green-600" />
          <h1 className="text-xl font-semibold text-gray-900">Cadastros unificados</h1>
          <p className="mt-2 text-sm text-gray-700">
            Foram movidos <strong>{resultado.referenciasRepontadas}</strong> registros do painel e{' '}
            <strong>{resultado.clinicoRepontado}</strong> registros clínicos para o cadastro que ficou.
            O cadastro absorvido foi desativado e aponta para o definitivo. A ação ficou registrada no
            Histórico de alterações dos dois.
          </p>
        </div>
        <div className="flex justify-center gap-3">
          <Button onClick={() => navigate(`/app/pacientes/${resultado.sobreviventeId}`)}>
            Ver cadastro que ficou
          </Button>
          <Button variante="outline" onClick={reiniciar}>
            Unificar outros
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold text-gray-900">Unificar paciente</h1>
            <AjudaManual artigo="pacientes-unificar" />
          </div>
          <p className="mt-1 max-w-3xl text-sm text-gray-600">
            Junta <strong>dois cadastros da mesma pessoa</strong> num só. Escolha o cadastro que vai{' '}
            <strong>ficar</strong> e o que será <strong>absorvido</strong>; para cada dado diferente,
            decida qual valor prevalece. Laudos, solicitações, conversas e o histórico clínico passam
            todos para o cadastro que fica.
          </p>
        </div>
        <Button variante="outline" onClick={() => navigate('/app/pacientes')}>
          Voltar
        </Button>
      </header>

      {/* Seleção dos dois cadastros */}
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          {a ? (
            <CartaoSelecionado
              p={a} rotulo="Cadastro que vai ficar" cor="border-primary-300"
              aoRemover={() => { setSobreviventeId(null); setResolucao({}); setConfirmaChaves(false); }}
            />
          ) : (
            <div className="rounded-lg border-2 border-dashed border-gray-200 p-4">
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Cadastro que vai ficar
              </p>
              <BuscaPaciente
                placeholder="Buscar o cadastro que fica…"
                aoSelecionar={(p: PacienteListItem) => setSobreviventeId(p.id)}
              />
            </div>
          )}
        </div>
        <div className="space-y-2">
          {b ? (
            <CartaoSelecionado
              p={b} rotulo="Cadastro que será absorvido" cor="border-amber-300"
              aoRemover={() => { setAbsorvidoId(null); setResolucao({}); setConfirmaChaves(false); }}
            />
          ) : (
            <div className="rounded-lg border-2 border-dashed border-gray-200 p-4">
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Cadastro que será absorvido
              </p>
              <BuscaPaciente
                placeholder="Buscar o cadastro duplicado…"
                aoSelecionar={(p: PacienteListItem) => setAbsorvidoId(p.id)}
              />
            </div>
          )}
        </div>
      </div>

      {a && b ? (
        <div className="flex justify-center">
          <Button variante="ghost" tamanho="sm" onClick={trocarLados}>
            <ArrowLeftRight className="h-4 w-4" />
            Trocar lados
          </Button>
        </div>
      ) : null}

      {mesmoCadastro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Escolha dois cadastros diferentes.
        </div>
      ) : null}

      {/* Alerta de chave nacional divergente */}
      {a && b && !mesmoCadastro && chavesDivergem ? (
        <div className="rounded-lg border-2 border-red-300 bg-red-50 p-4">
          <div className="flex items-start gap-3">
            <ShieldAlert className="mt-0.5 h-5 w-5 shrink-0 text-red-600" />
            <div>
              <p className="font-medium text-red-800">
                Os dois cadastros têm {previa.data?.cpfDivergente ? 'CPF' : ''}
                {previa.data?.cpfDivergente && previa.data?.cnsDivergente ? ' e ' : ''}
                {previa.data?.cnsDivergente ? 'CNS' : ''} diferentes.
              </p>
              <p className="mt-1 text-sm text-red-700">
                Isso pode significar que são <strong>pessoas diferentes</strong>. Unir mistura dois
                prontuários e não se desfaz num clique. Confirme apenas se tiver certeza de que é a
                mesma pessoa.
              </p>
              <label className="mt-3 flex items-center gap-2 text-sm font-medium text-red-800">
                <input
                  type="checkbox"
                  checked={confirmaChaves}
                  onChange={(e) => setConfirmaChaves(e.target.checked)}
                  className="h-4 w-4 rounded border-red-300"
                />
                Confirmo que é a mesma pessoa.
              </label>
            </div>
          </div>
        </div>
      ) : null}

      {/* Identidade (sempre do cadastro que fica) */}
      {a && b && !mesmoCadastro ? (
        <section className="rounded-lg border border-gray-200 bg-white p-4">
          <h2 className="mb-3 text-sm font-semibold text-gray-900">Identidade (mantida do cadastro que fica)</h2>
          <div className="grid grid-cols-1 gap-x-8 gap-y-2 text-sm sm:grid-cols-2">
            {[
              ['CPF', cpfFmt(a.cpf), cpfFmt(b.cpf)],
              ['CNS', texto(a.cns), texto(b.cns)],
              ['Nascimento', dataFmt(a.dataNascimento), dataFmt(b.dataNascimento)],
            ].map(([rot, va, vb]) => (
              <div key={rot} className="flex justify-between gap-4 border-b border-gray-100 py-1">
                <span className="text-gray-500">{rot}</span>
                <span className="text-right">
                  <span className="font-medium text-gray-900">{va}</span>
                  {va !== vb ? <span className="text-gray-400"> (outro: {vb})</span> : null}
                </span>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      {/* Resolução campo a campo */}
      {a && b && !mesmoCadastro ? (
        <section className="rounded-lg border border-gray-200 bg-white p-4">
          <h2 className="mb-1 text-sm font-semibold text-gray-900">
            Dados divergentes — escolha o que fica
          </h2>
          {divergentes.length === 0 ? (
            <p className="text-sm text-gray-500">
              Os demais dados são iguais nos dois cadastros. Nada a decidir.
            </p>
          ) : (
            <ul className="divide-y divide-gray-100">
              {divergentes.map((c) => {
                const lado = ladoDe(c.chave);
                return (
                  <li key={c.chave} className="py-3">
                    <p className="mb-2 text-sm font-medium text-gray-700">{c.rotulo}</p>
                    <div className="grid gap-2 sm:grid-cols-2">
                      <button
                        type="button"
                        onClick={() => escolher(c.chave, 'sobrevivente')}
                        className={`rounded-md border px-3 py-2 text-left text-sm ${
                          lado === 'sobrevivente'
                            ? 'border-primary-500 bg-primary-50 text-primary-900'
                            : 'border-gray-200 text-gray-700 hover:bg-gray-50'
                        }`}
                      >
                        <span className="block text-xs text-gray-500">Cadastro que fica</span>
                        {c.fmt(a)}
                      </button>
                      <button
                        type="button"
                        onClick={() => escolher(c.chave, 'absorvido')}
                        className={`rounded-md border px-3 py-2 text-left text-sm ${
                          lado === 'absorvido'
                            ? 'border-amber-500 bg-amber-50 text-amber-900'
                            : 'border-gray-200 text-gray-700 hover:bg-gray-50'
                        }`}
                      >
                        <span className="block text-xs text-gray-500">Cadastro absorvido</span>
                        {c.fmt(b)}
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      ) : null}

      {/* O que será movido */}
      {a && b && !mesmoCadastro ? (
        <section className="rounded-lg border border-gray-200 bg-white p-4">
          <h2 className="mb-2 text-sm font-semibold text-gray-900">O que será movido para o cadastro que fica</h2>
          {previa.isLoading ? (
            <p className="text-sm text-gray-500">Calculando…</p>
          ) : previa.isError ? (
            <p className="text-sm text-red-600">{extrairMensagemDeErro(previa.error)}</p>
          ) : (previa.data?.referencias.length ?? 0) === 0 ? (
            <p className="text-sm text-gray-500">
              Nenhum registro do painel vinculado ao cadastro absorvido. O histórico clínico é movido
              pelo hub.
            </p>
          ) : (
            <ul className="flex flex-wrap gap-2">
              {previa.data!.referencias.map((r) => (
                <li key={r.modulo} className="rounded-full bg-gray-100 px-3 py-1 text-xs text-gray-700">
                  {r.modulo}: <strong>{r.quantidade}</strong>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}

      {unificar.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(unificar.error)}
        </div>
      ) : null}

      {a && b && !mesmoCadastro ? (
        <div className="flex justify-end">
          <Button
            onClick={() => setDialogAberto(true)}
            disabled={!prontoParaConfirmar || unificar.isPending}
          >
            <Merge className="h-4 w-4" />
            Unificar cadastros
          </Button>
        </div>
      ) : null}

      <ConfirmDialog
        aberto={dialogAberto}
        titulo="Unificar cadastros?"
        mensagem={
          `O cadastro de ${b?.nomeCompleto ?? ''} será absorvido por ${a?.nomeCompleto ?? ''}. ` +
          'Todos os registros (laudos, solicitações, conversas e histórico clínico) passam para o ' +
          'cadastro que fica, e o absorvido é desativado. Esta ação não se desfaz num clique.'
        }
        rotuloConfirmar="Unificar"
        destrutivo
        carregando={unificar.isPending}
        erro={unificar.isError ? extrairMensagemDeErro(unificar.error) : null}
        aoConfirmar={confirmar}
        aoCancelar={() => setDialogAberto(false)}
      />
    </div>
  );
}
