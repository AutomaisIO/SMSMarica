import { ArrowLeft, Pencil } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { usePacientePorId } from '@/features/pacientes/api/queries';
import type { Paciente } from '@/features/pacientes/types';

function campo(label: string, valor?: string | number | null) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{label}</span>
      <span className="text-sm text-gray-900">{valor || <span className="text-gray-400">—</span>}</span>
    </div>
  );
}

function formatarCpf(cpf: string) {
  const d = cpf.replace(/\D/g, '');
  return d.length === 11 ? `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}` : cpf;
}

function formatarData(iso?: string | null) {
  if (!iso) return undefined;
  const m = String(iso).match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

const SEXO_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado', Masculino: 'Masculino', Feminino: 'Feminino', Outro: 'Outro',
};
const ESTADO_CIVIL_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado', Solteiro: 'Solteiro(a)', Casado: 'Casado(a)',
  UniaoEstavel: 'União estável', Divorciado: 'Divorciado(a)', Viuvo: 'Viúvo(a)', Separado: 'Separado(a)',
};
const RACA_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado', Branca: 'Branca', Preta: 'Preta',
  Parda: 'Parda', Amarela: 'Amarela', Indigena: 'Indígena',
};
const ESCOLARIDADE_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado', Analfabeto: 'Analfabeto', SemEscolaridade: 'Sem escolaridade',
  FundamentalIncompleto: 'Fundamental incompleto', FundamentalCompleto: 'Fundamental completo',
  MedioIncompleto: 'Médio incompleto', MedioCompleto: 'Médio completo',
  SuperiorIncompleto: 'Superior incompleto', SuperiorCompleto: 'Superior completo',
  PosGraduacao: 'Pós-graduação',
};
const TIPO_SANG_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado', A: 'A', B: 'B', AB: 'AB', O: 'O',
};
const FATOR_RH_LABEL: Record<string, string> = {
  NaoInformado: 'Não informado', Positivo: 'Positivo (+)', Negativo: 'Negativo (−)',
};

function Chips({ itens }: { itens: string[] }) {
  if (!itens.length) return <span className="text-sm text-gray-400">—</span>;
  return (
    <div className="flex flex-wrap gap-1.5">
      {itens.map((item, i) => (
        <span key={i} className="rounded-full border border-gray-200 bg-gray-50 px-2.5 py-0.5 text-xs text-gray-700">
          {item}
        </span>
      ))}
    </div>
  );
}

function SecaoIdentificacao({ p }: { p: Paciente }) {
  return (
    <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
      <div className="md:col-span-2">{campo('Nome completo', p.nomeCompleto)}</div>
      {campo('CPF', formatarCpf(p.cpf))}
      {campo('Data de nascimento', formatarData(p.dataNascimento?.toString()))}
      {campo('CNS (Cartão SUS)', p.cns)}
      {campo('RG', p.rg)}
      {campo('Sexo', SEXO_LABEL[String(p.sexo)] ?? String(p.sexo))}
      {campo('Estado civil', ESTADO_CIVIL_LABEL[String(p.estadoCivil)] ?? String(p.estadoCivil))}
      {campo('Raça/Cor', RACA_LABEL[String(p.racaCor)] ?? String(p.racaCor))}
      {campo('Escolaridade', ESCOLARIDADE_LABEL[String(p.escolaridade)] ?? String(p.escolaridade))}
      {campo('Ocupação', p.ocupacao)}
      {campo('Naturalidade', p.naturalidade)}
      {campo('Nacionalidade', p.nacionalidade)}
    </div>
  );
}

function SecaoFiliacao({ p }: { p: Paciente }) {
  return (
    <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
      <div className="md:col-span-2">{campo('Nome da mãe', p.nomeDaMae)}</div>
      <div className="md:col-span-2">{campo('Nome do pai', p.nomeDoPai)}</div>
      <div className="md:col-span-2">{campo('Responsável legal', p.responsavelLegal)}</div>
    </div>
  );
}

function SecaoEndereco({ p }: { p: Paciente }) {
  if (!p.endereco) return <p className="text-sm text-gray-400">Endereço não informado.</p>;
  const e = p.endereco;
  return (
    <div className="grid grid-cols-1 gap-5 md:grid-cols-6">
      <div className="md:col-span-2">{campo('CEP', e.cep)}</div>
      <div className="md:col-span-4">{campo('Logradouro', e.logradouro)}</div>
      <div className="md:col-span-1">{campo('Número', e.numero)}</div>
      <div className="md:col-span-3">{campo('Complemento', e.complemento)}</div>
      <div className="md:col-span-2">{campo('Bairro', e.bairro)}</div>
      <div className="md:col-span-3">{campo('Cidade', e.cidade)}</div>
      <div className="md:col-span-1">{campo('UF', e.uf)}</div>
      <div className="md:col-span-6">{campo('Ponto de referência', e.pontoReferencia)}</div>
    </div>
  );
}

function SecaoContatos({ p }: { p: Paciente }) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
        {campo('Telefone principal', p.telefonePrincipal)}
        {campo('Celular', p.telefoneCelular)}
        {campo('Telefone residencial', p.telefoneResidencial)}
        {campo('E-mail', p.email)}
      </div>
      {p.contatoEmergencia ? (
        <div>
          <h3 className="mb-3 text-sm font-semibold text-gray-900">Contato de emergência</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
            {campo('Nome', p.contatoEmergencia.nome)}
            {campo('Parentesco', p.contatoEmergencia.parentesco)}
            {campo('Telefone', p.contatoEmergencia.telefone)}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function SecaoSaude({ p }: { p: Paciente }) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-5 md:grid-cols-4">
        {campo('Altura (cm)', p.alturaCm)}
        {campo('Peso (kg)', p.pesoKg != null ? String(p.pesoKg) : undefined)}
        {campo('Tipo sanguíneo', TIPO_SANG_LABEL[String(p.tipoSanguineo)] ?? String(p.tipoSanguineo))}
        {campo('Fator Rh', FATOR_RH_LABEL[String(p.fatorRh)] ?? String(p.fatorRh))}
      </div>
      <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
        <div>
          <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Alergias</span>
          <div className="mt-1.5"><Chips itens={p.alergias} /></div>
        </div>
        <div>
          <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Medicamentos contínuos</span>
          <div className="mt-1.5"><Chips itens={p.medicamentosContinuos} /></div>
        </div>
        <div>
          <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Comorbidades</span>
          <div className="mt-1.5"><Chips itens={p.comorbidades} /></div>
        </div>
        <div>
          <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Deficiências</span>
          <div className="mt-1.5"><Chips itens={p.deficiencias} /></div>
        </div>
      </div>
      {campo('Plano de saúde', p.planoSaude)}
    </div>
  );
}

export function PacienteDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const id = params.id ?? '';
  const detalhe = usePacientePorId(id || null);

  const p = detalhe.data;

  const abas: Aba[] = p ? [
    { id: 'identificacao', rotulo: 'Identificação', conteudo: <SecaoIdentificacao p={p} /> },
    { id: 'filiacao', rotulo: 'Filiação', conteudo: <SecaoFiliacao p={p} /> },
    { id: 'endereco', rotulo: 'Endereço', conteudo: <SecaoEndereco p={p} /> },
    { id: 'contatos', rotulo: 'Contatos', conteudo: <SecaoContatos p={p} /> },
    { id: 'saude', rotulo: 'Saúde', conteudo: <SecaoSaude p={p} /> },
    {
      id: 'observacoes', rotulo: 'Observações',
      conteudo: (
        <p className="whitespace-pre-wrap text-sm text-gray-700">
          {p.observacoes || <span className="text-gray-400">Nenhuma observação registrada.</span>}
        </p>
      ),
    },
  ] : [];

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/operador/pacientes')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                {p?.nomeCompleto ?? 'Carregando…'}
              </h1>
              {p ? <StatusBadge ativo={p.ativo} /> : null}
            </div>
            {p ? (
              <p className="text-sm text-gray-500">
                CPF {formatarCpf(p.cpf)}
                {p.dataNascimento ? ` · Nasc. ${formatarData(p.dataNascimento.toString())}` : ''}
              </p>
            ) : null}
          </div>
        </div>
        <Button onClick={() => navigate(`/operador/pacientes/${id}/editar`)}>
          <Pencil className="h-4 w-4" />
          Editar
        </Button>
      </header>

      {detalhe.isLoading ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : detalhe.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível carregar os dados do paciente.
        </div>
      ) : p ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <Tabs abas={abas} />
        </div>
      ) : null}
    </div>
  );
}
