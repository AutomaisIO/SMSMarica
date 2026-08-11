import { useMemo, useState, type ReactNode } from 'react';
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CalendarClock,
  Download,
  FileText,
  HeartPulse,
  ListChecks,
  Pencil,
  Pill,
  Printer,
  Smartphone,
  Stethoscope,
} from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Avatar } from '@/shared/ui/Avatar';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { nomeBaseOrigem, nomeSistemaOrigem, rotuloOrigem } from '@/shared/lib/origemClinica';
import {
  useAcessosPaciente,
  useAtendimentosPaciente,
  useAuditoriaPaciente,
  usePacientePorId,
} from '@/features/pacientes/api/queries';
import type { AcessoCidadao } from '@/features/pacientes/api/pacientesApi';
import type { RegistroAuditoria } from '@/features/auditoria/types';
import { NomeCompletoVerificavel } from '@/features/pacientes/components/NomeCompletoVerificavel';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { SecaoExamesAnexados } from '@/features/pacientes/components/SecaoExamesAnexados';
import { SinaisVitaisTendencia } from '@/features/pacientes/components/SinaisVitaisTendencia';
import { abrirImpressaoDocumento, EDOC_CSS } from '@/features/pacientes/lib/imprimirDocumento';
import type { Atendimento, Documento, Paciente } from '@/features/pacientes/types';
import { useListarTratamentos } from '@/features/tratamentos/api/queries';
import { formatarDataBr } from '@/features/tratamentos/lib/expansor';
import type { TratamentoListItem } from '@/features/tratamentos/types';

function campo(label: string, valor?: ReactNode) {
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

function rotuloIdentificador(sistema: string): string {
  const s = sistema.toLowerCase();
  if (s.includes('/cpf')) return 'CPF';
  if (s.includes('/cns')) return 'CNS (Cartão SUS)';
  if (s.includes('rg')) return 'RG';
  if (s.includes('pis')) return 'PIS/PASEP';
  if (s.includes('passport')) return 'Passaporte';
  if (s.includes('rne')) return 'RNE';
  if (s.includes('certidao')) return 'Certidão de nascimento';
  if (s.includes('crm')) return 'CRM';
  if (s.includes('salux')) return 'Prontuário (Salux)';
  if (s.includes('sgh')) return 'Prontuário (SGH)';
  if (s.includes('cem')) return 'Prontuário (CEM)';
  return sistema;
}

/**
 * Etiqueta "Origem: Salux - HMCML" do atendimento. Antes daqui só existia o caso do Salux,
 * codificado num `if`, então tudo que vinha do Klinikos aparecia sem etiqueta nenhuma — e a
 * lista dava a impressão de que o hub só tinha Salux.
 */
function EtiquetaOrigem({ atendimento }: { atendimento: Atendimento }) {
  const origem = rotuloOrigem(atendimento.fonte, atendimento.unidadeCnes, atendimento.unidadeNome);
  if (!origem) return null;
  return (
    <span
      className="text-[10px] uppercase tracking-wide text-gray-400"
      title={atendimento.unidadeNome ?? undefined}
    >
      Origem: {origem}
    </span>
  );
}

const EXTRA_LABEL: Record<string, string> = {
  estado_civil: 'Estado civil', escolaridade: 'Escolaridade', religiao: 'Religião',
  barreira_comunicacao: 'Barreira de comunicação', cd_cor: 'Raça/Cor (código)',
  cd_nacionalidade: 'Nacionalidade (código)', pais: 'País', etnia: 'Etnia (código)',
  profissao: 'Profissão', ocupacao: 'Ocupação', peso: 'Peso', altura: 'Altura',
  sangue: 'Tipo sanguíneo', rh: 'Fator Rh', grau_parentesco: 'Grau de parentesco',
  entrada_pais: 'Entrada no país',
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
      <div className="md:col-span-2"><NomeCompletoVerificavel paciente={p} /></div>
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
      <div className="md:col-span-2">{campo('Cônjuge', p.nomeConjuge)}</div>
      <div className="md:col-span-2">{campo('Responsável legal', p.responsavelLegal)}</div>
    </div>
  );
}

function SecaoDocumentos({ p }: { p: Paciente }) {
  const idents = p.identificadores ?? [];
  const extras = Object.entries(p.dadosFonte ?? {});
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
        {campo(
          'Origem (fonte)',
          p.fonte ? (
            <span title={p.fonte}>
              {nomeSistemaOrigem(p.fonte)}
              {nomeBaseOrigem(p.fonte) ? (
                <span className="text-gray-500"> · {nomeBaseOrigem(p.fonte)}</span>
              ) : null}
            </span>
          ) : null,
        )}
        {campo('Data de óbito', formatarData(p.dataObito))}
      </div>
      <div>
        <h3 className="mb-3 text-sm font-semibold text-gray-900">Identificadores</h3>
        {idents.length === 0 ? (
          <span className="text-sm text-gray-400">Nenhum identificador.</span>
        ) : (
          <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
            {idents.map((i, idx) => (
              <div key={idx}>{campo(rotuloIdentificador(i.sistema), i.valor)}</div>
            ))}
          </div>
        )}
      </div>
      {extras.length > 0 ? (
        <div>
          <h3 className="mb-3 text-sm font-semibold text-gray-900">Dados da fonte</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
            {extras.map(([k, v]) => (
              <div key={k}>{campo(EXTRA_LABEL[k] ?? k, v)}</div>
            ))}
          </div>
        </div>
      ) : null}
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
        {campo('Telefone principal', p.telefonePrincipal ? <TelefoneCopiavel numero={p.telefonePrincipal} /> : null)}
        {campo('Celular', p.telefoneCelular ? <TelefoneCopiavel numero={p.telefoneCelular} /> : null)}
        {campo('Telefone residencial', p.telefoneResidencial ? <TelefoneCopiavel numero={p.telefoneResidencial} /> : null)}
        {campo('E-mail', p.email)}
      </div>
      {p.contatoEmergencia ? (
        <div>
          <h3 className="mb-3 text-sm font-semibold text-gray-900">Contato de emergência</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
            {campo('Nome', p.contatoEmergencia.nome)}
            {campo('Parentesco', p.contatoEmergencia.parentesco)}
            {campo('Telefone', p.contatoEmergencia.telefone ? <TelefoneCopiavel numero={p.contatoEmergencia.telefone} /> : null)}
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

function formatarDataHora(iso?: string | null): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

function corTipoAtendimento(tipo: string): string {
  if (tipo === 'Urgência') return 'bg-red-100 text-red-700';
  if (tipo === 'Internação') return 'bg-amber-100 text-amber-800';
  return 'bg-blue-100 text-blue-700';
}

function acharVital(a: Atendimento, codigo: string) {
  return a.sinaisVitais.find((v) => v.codigo === codigo);
}

/** Resumo curto dos sinais vitais de um atendimento (PA destaque + FC/Tª/SpO₂). */
function resumoVitais(a: Atendimento): { principal: string; detalhe: string } {
  const pa = acharVital(a, '85354-9');
  const fc = acharVital(a, '8867-4');
  const temp = acharVital(a, '8310-5');
  const spo2 = acharVital(a, '2708-6');

  const principal =
    pa && pa.valor != null
      ? `${pa.valor}/${pa.valor2 ?? '—'}`
      : fc?.valor != null
        ? `${fc.valor} bpm`
        : '—';

  const partes: string[] = [];
  if (pa && fc?.valor != null) partes.push(`FC ${fc.valor}`);
  if (temp?.valor != null) partes.push(`${temp.valor}°C`);
  if (spo2?.valor != null) partes.push(`SpO₂ ${spo2.valor}%`);
  return { principal, detalhe: partes.join(' · ') };
}

/** Cor da triagem (Manchester) → classes Tailwind + se é nível de alerta. */
function corRisco(cor: string): { classe: string; alerta: boolean } {
  const c = cor.toLowerCase();
  if (c.includes('vermelh')) return { classe: 'bg-red-100 text-red-700', alerta: true };
  if (c.includes('laranja')) return { classe: 'bg-orange-100 text-orange-700', alerta: true };
  if (c.includes('amarel')) return { classe: 'bg-yellow-100 text-yellow-800', alerta: true };
  if (c.includes('verde')) return { classe: 'bg-green-100 text-green-700', alerta: false };
  if (c.includes('azul')) return { classe: 'bg-blue-100 text-blue-700', alerta: false };
  return { classe: 'bg-gray-100 text-gray-700', alerta: false };
}

type DocAberto = { atendimento: Atendimento; doc: Documento };

/** Bloco de sinais vitais (triagem/classificação de risco) dentro do card do atendimento. */
function SecaoSinaisVitais({ atendimento }: { atendimento: Atendimento }) {
  if (atendimento.sinaisVitais.length === 0 && !atendimento.risco) return null;
  return (
    <div className="mt-3 border-t border-gray-100 pt-2">
      <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-gray-500">
        <HeartPulse className="h-3.5 w-3.5" /> Sinais vitais (triagem)
      </div>
      <div className="flex flex-wrap items-center gap-2 text-sm">
        {atendimento.risco ? (
          <span className={`rounded px-2 py-0.5 text-xs font-medium ${corRisco(atendimento.risco.cor).classe}`}>
            {atendimento.risco.cor}
          </span>
        ) : null}
        {atendimento.sinaisVitais.map((v) => (
          <span key={v.codigo} className="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
            {v.nome}:{' '}
            {v.codigo === '85354-9'
              ? `${v.valor ?? '—'}/${v.valor2 ?? '—'}`
              : v.valor ?? '—'}
            {v.unidade ? ` ${v.unidade}` : ''}
          </span>
        ))}
      </div>
    </div>
  );
}

function SecaoMedicamentos({ atendimento }: { atendimento: Atendimento }) {
  if (atendimento.medicamentos.length === 0) return null;
  return (
    <div className="mt-3 border-t border-gray-100 pt-2">
      <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-gray-500">
        <Pill className="h-3.5 w-3.5" /> Medicamentos
      </div>
      <ul className="space-y-1">
        {atendimento.medicamentos.map((m) => (
          <li key={m.id} className="flex flex-wrap items-baseline gap-x-2 text-sm">
            {m.urgente ? (
              <span className="rounded border border-red-200 px-1 text-[10px] font-bold uppercase text-red-700">
                Urgente
              </span>
            ) : null}
            <span className="font-medium text-gray-900">{m.descricao}</span>
            {m.posologia ? <span className="text-xs text-gray-500">{m.posologia}</span> : null}
          </li>
        ))}
      </ul>
    </div>
  );
}

function SecaoAtendimentos({ pacienteId, paciente }: { pacienteId: string; paciente: Paciente }) {
  const q = useAtendimentosPaciente(pacienteId);
  const lista: Atendimento[] = q.data ?? [];
  const [docAberto, setDocAberto] = useState<DocAberto | null>(null);

  function imprimir({ atendimento, doc }: DocAberto) {
    abrirImpressaoDocumento({
      pacienteNome: paciente.nomeCompleto,
      pacienteCpf: paciente.cpf,
      atendimentoTipo: atendimento.tipo,
      atendimentoData: formatarDataHora(atendimento.inicio),
      medicoNome: atendimento.medicoNome,
      documentoTitulo: doc.tipo,
      conteudoHtml: doc.conteudoHtml,
      medicamentos: atendimento.medicamentos.map((m) => ({
        descricao: m.descricao,
        posologia: m.posologia,
        urgente: m.urgente,
      })),
    });
  }

  if (q.isLoading) {
    return <div className="text-sm text-gray-500">Carregando atendimentos…</div>;
  }
  if (q.isError) {
    return (
      <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        Não foi possível carregar o histórico de atendimentos.
      </div>
    );
  }
  if (lista.length === 0) {
    return (
      <div className="text-sm text-gray-400">
        Nenhum atendimento encontrado para este paciente no hub clínico.
      </div>
    );
  }
  return (
    <>
      <ol className="space-y-3">
      {lista.map((a) => (
        <li key={a.id} className="rounded-lg border border-gray-200 p-4">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${corTipoAtendimento(a.tipo)}`}>
                {a.tipo}
              </span>
              <span className="text-sm font-medium text-gray-900">
                {formatarDataHora(a.inicio) ?? '—'}
              </span>
            </div>
            <EtiquetaOrigem atendimento={a} />
          </div>
          {a.medicoNome ? (
            <div className="mt-1 flex items-center gap-1.5 text-sm text-gray-600">
              <Stethoscope className="h-3.5 w-3.5" /> {a.medicoNome}
            </div>
          ) : null}
          {a.diagnosticos.length > 0 ? (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {a.diagnosticos.map((d, i) => (
                <span
                  key={`${d.codigo}-${i}`}
                  className="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-700"
                  title={d.descricao ?? undefined}
                >
                  {d.codigo}
                  {d.descricao ? ` · ${d.descricao}` : ''}
                </span>
              ))}
            </div>
          ) : null}
          <SecaoSinaisVitais atendimento={a} />
          <SecaoMedicamentos atendimento={a} />
          {a.documentos.length > 0 ? (
            <div className="mt-3 flex flex-wrap gap-1.5 border-t border-gray-100 pt-2">
              {a.documentos.map((doc) => (
                <button
                  key={doc.id}
                  type="button"
                  onClick={() => setDocAberto({ atendimento: a, doc })}
                  className="inline-flex items-center gap-1 rounded border border-gray-200 px-2 py-1 text-xs text-red-700 hover:bg-red-50"
                >
                  <FileText className="h-3.5 w-3.5" /> {doc.tipo}
                </button>
              ))}
            </div>
          ) : null}
        </li>
      ))}
      </ol>
      <Modal
        aberto={docAberto !== null}
        aoFechar={() => setDocAberto(null)}
        titulo={docAberto?.doc.tipo ?? 'Documento'}
        largura="lg"
      >
        {docAberto ? (
          <>
            <style>{EDOC_CSS}</style>
            <div className="mb-4 flex flex-wrap gap-2 border-b border-gray-100 pb-3">
              <Button variante="outline" tamanho="sm" onClick={() => imprimir(docAberto)}>
                <Printer className="h-4 w-4" /> Imprimir
              </Button>
              <Button variante="outline" tamanho="sm" onClick={() => imprimir(docAberto)}>
                <Download className="h-4 w-4" /> Baixar PDF
              </Button>
            </div>
            <div
              className="edoc-render"
              // Conteúdo remontado no importador, com valores já escapados (montar_html).
              dangerouslySetInnerHTML={{ __html: docAberto.doc.conteudoHtml }}
            />
          </>
        ) : null}
      </Modal>
    </>
  );
}

const CANAL_ACESSO_LABEL: Record<string, string> = {
  'otp-whatsapp': 'WhatsApp (código)',
  senha: 'Senha',
  google: 'Google',
  microsoft: 'Microsoft',
  facebook: 'Facebook',
};

function statusAcesso(a: AcessoCidadao): { texto: string; classe: string } {
  if (a.ativa) return { texto: 'Ativa', classe: 'bg-green-100 text-green-700' };
  if (a.revogadaEm) return { texto: 'Encerrada', classe: 'bg-gray-100 text-gray-600' };
  return { texto: 'Expirada', classe: 'bg-amber-100 text-amber-700' };
}

/** Histórico de acessos (sessões de login) do paciente ao app. */
function SecaoAcessos({ pacienteId }: { pacienteId: string }) {
  const q = useAcessosPaciente(pacienteId);
  const colunas: Coluna<AcessoCidadao>[] = [
    { chave: 'criadaEm', cabecalho: 'Data/hora', render: (a) => formatarDataHora(a.criadaEm) ?? '—' },
    { chave: 'canal', cabecalho: 'Forma de acesso', render: (a) => CANAL_ACESSO_LABEL[a.canal] ?? a.canal },
    {
      chave: 'dispositivo',
      cabecalho: 'Dispositivo',
      render: (a) => (
        <span className="block max-w-xs truncate text-gray-600" title={a.dispositivo ?? ''}>
          {a.dispositivo || '—'}
        </span>
      ),
    },
    { chave: 'ip', cabecalho: 'IP', render: (a) => a.ip || '—' },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (a) => {
        const s = statusAcesso(a);
        return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.classe}`}>{s.texto}</span>;
      },
    },
  ];
  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <Smartphone className="h-4 w-4" /> Histórico de acesso ao app
      </div>
      <Tabela
        colunas={colunas}
        dados={q.data ?? []}
        chaveLinha={(a) => a.id}
        carregando={q.isLoading}
        vazio={
          !q.isLoading && (q.data?.length ?? 0) === 0
            ? 'Este paciente ainda não acessou o app.'
            : undefined
        }
      />
    </>
  );
}

function calcularIdade(iso?: string | null): number | null {
  if (!iso) return null;
  const m = String(iso).match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (!m) return null;
  const nasc = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  const hoje = new Date();
  let idade = hoje.getFullYear() - nasc.getFullYear();
  const diffMes = hoje.getMonth() - nasc.getMonth();
  if (diffMes < 0 || (diffMes === 0 && hoje.getDate() < nasc.getDate())) idade--;
  return idade >= 0 && idade < 150 ? idade : null;
}

function calcularImc(pesoKg?: number | null, alturaCm?: number | null) {
  if (!pesoKg || !alturaCm) return null;
  const metros = alturaCm / 100;
  const imc = pesoKg / (metros * metros);
  if (!Number.isFinite(imc) || imc <= 0) return null;
  const classe =
    imc < 18.5 ? 'Abaixo do peso' : imc < 25 ? 'Peso normal' : imc < 30 ? 'Sobrepeso' : 'Obesidade';
  return { valor: Math.round(imc * 10) / 10, classe };
}

/** Cartão-métrica do raio-x. Clicável quando recebe `onClick`. */
function CartaoKpi({
  icone,
  rotulo,
  valor,
  sub,
  tom = 'neutro',
  onClick,
}: {
  icone: ReactNode;
  rotulo: string;
  valor: ReactNode;
  sub?: ReactNode;
  tom?: 'neutro' | 'alerta';
  onClick?: () => void;
}) {
  const base = 'rounded-lg border bg-white p-4 text-left shadow-sm transition-colors';
  const borda = tom === 'alerta' ? 'border-amber-200' : 'border-gray-200';
  const hover = onClick ? 'hover:border-red-300 hover:shadow' : '';
  const corIcone = tom === 'alerta' ? 'text-amber-600' : 'text-red-600';
  const conteudo = (
    <>
      <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-gray-500">
        <span className={corIcone}>{icone}</span>
        {rotulo}
        {onClick ? <ArrowRight className="ml-auto h-3.5 w-3.5 text-gray-300" /> : null}
      </div>
      <div className="mt-2 text-2xl font-semibold text-gray-900">{valor}</div>
      {sub ? <div className="mt-0.5 text-xs text-gray-500">{sub}</div> : null}
    </>
  );
  return onClick ? (
    <button type="button" onClick={onClick} className={`${base} ${borda} ${hover} w-full`}>
      {conteudo}
    </button>
  ) : (
    <div className={`${base} ${borda}`}>{conteudo}</div>
  );
}

/** Bloco branco com título — usado nos cartões de saúde do resumo. */
function CartaoBloco({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
      <h3 className="mb-3 text-sm font-semibold text-gray-900">{titulo}</h3>
      {children}
    </div>
  );
}

type EstatisticasAtendimentos = {
  total: number;
  ultimo: Atendimento | null;
  ultimoComVitais: Atendimento | null;
  porTipo: { tipo: string; n: number }[];
  topCid: { codigo: string; descricao?: string | null; n: number }[];
};

function resumirAtendimentos(lista: Atendimento[]): EstatisticasAtendimentos {
  const porTipo = new Map<string, number>();
  const porCid = new Map<string, { codigo: string; descricao?: string | null; n: number }>();
  for (const a of lista) {
    porTipo.set(a.tipo, (porTipo.get(a.tipo) ?? 0) + 1);
    for (const d of a.diagnosticos) {
      const atual = porCid.get(d.codigo);
      if (atual) atual.n++;
      else porCid.set(d.codigo, { codigo: d.codigo, descricao: d.descricao, n: 1 });
    }
  }
  return {
    total: lista.length,
    ultimo: lista[0] ?? null,
    // Lista vem mais recente primeiro → o 1º com vitais é a triagem mais recente.
    ultimoComVitais: lista.find((a) => a.sinaisVitais.length > 0 || a.risco) ?? null,
    porTipo: [...porTipo.entries()].map(([tipo, n]) => ({ tipo, n })).sort((a, b) => b.n - a.n),
    topCid: [...porCid.values()].sort((a, b) => b.n - a.n).slice(0, 5),
  };
}

type Vista = 'resumo' | 'atendimentos' | 'tratamentos' | 'exames' | 'acessos' | 'auditoria' | 'dados';

/** Histórico de alterações auditadas do paciente (ex.: correções de nome). */
function SecaoAuditoriaPaciente({ pacienteId }: { pacienteId: string }) {
  const q = useAuditoriaPaciente(pacienteId);
  const colunas: Coluna<RegistroAuditoria>[] = [
    { chave: 'data', cabecalho: 'Data/hora', render: (r) => formatarDataHora(r.criadoEm) ?? '—' },
    { chave: 'usuario', cabecalho: 'Usuário', render: (r) => r.usuarioNome ?? '—' },
    { chave: 'acao', cabecalho: 'Ação', render: (r) => (r.acao === 'AlteracaoNome' ? 'Alteração de nome' : r.acao) },
    { chave: 'de', cabecalho: 'De', render: (r) => <span className="text-gray-500">{r.valorAnterior ?? '—'}</span> },
    { chave: 'para', cabecalho: 'Para', render: (r) => <span className="font-medium text-gray-900">{r.valorNovo ?? '—'}</span> },
  ];
  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <ListChecks className="h-4 w-4" /> Histórico de alterações
      </div>
      <Tabela
        colunas={colunas}
        dados={q.data?.itens ?? []}
        chaveLinha={(r) => r.id}
        carregando={q.isLoading}
        vazio={
          !q.isLoading && (q.data?.itens.length ?? 0) === 0
            ? 'Nenhuma alteração registrada para este paciente.'
            : undefined
        }
      />
    </>
  );
}

function ResumoPaciente({
  p,
  stats,
  atendimentos,
  carregandoAtend,
  qtdTratamentos,
  tratamentosAtivos,
  irPara,
}: {
  p: Paciente;
  stats: EstatisticasAtendimentos;
  atendimentos: Atendimento[];
  carregandoAtend: boolean;
  qtdTratamentos: number;
  tratamentosAtivos: number;
  irPara: (v: Vista) => void;
}) {
  const imc = calcularImc(p.pesoKg, p.alturaCm);
  const nAlertas = p.alergias.length + p.comorbidades.length;
  const maxTipo = Math.max(1, ...stats.porTipo.map((t) => t.n));

  // Sinais vitais da triagem mais recente (para o cartão KPI).
  const av = stats.ultimoComVitais;
  const vit = av ? resumoVitais(av) : null;
  const risco = av?.risco ? corRisco(av.risco.cor) : null;

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <CartaoKpi
          icone={<CalendarClock className="h-4 w-4" />}
          rotulo="Atendimentos"
          valor={carregandoAtend ? '…' : stats.total}
          sub={
            stats.ultimo
              ? `Último: ${formatarData(stats.ultimo.inicio) ?? '—'} · ${stats.ultimo.tipo}`
              : carregandoAtend
                ? 'Carregando…'
                : 'Nenhum atendimento'
          }
          onClick={() => irPara('atendimentos')}
        />
        <CartaoKpi
          icone={<ListChecks className="h-4 w-4" />}
          rotulo="Tratamentos"
          valor={tratamentosAtivos}
          sub={qtdTratamentos > 0 ? `${qtdTratamentos} no total` : 'Nenhum cadastrado'}
          onClick={() => irPara('tratamentos')}
        />
        <CartaoKpi
          icone={<AlertTriangle className="h-4 w-4" />}
          rotulo="Alertas clínicos"
          valor={nAlertas}
          tom={nAlertas > 0 ? 'alerta' : 'neutro'}
          sub={`${p.alergias.length} alergia(s) · ${p.comorbidades.length} comorbidade(s)`}
        />
        <CartaoKpi
          icone={<HeartPulse className="h-4 w-4" />}
          rotulo="Sinais vitais"
          valor={
            carregandoAtend ? (
              '…'
            ) : vit && vit.principal !== '—' ? (
              vit.principal
            ) : (
              <span className="text-base font-medium text-gray-400">Sem registro</span>
            )
          }
          tom={risco?.alerta ? 'alerta' : 'neutro'}
          sub={
            av
              ? [vit?.detalhe, av.risco ? `Risco: ${av.risco.cor}` : null, formatarData(av.inicio)]
                  .filter(Boolean)
                  .join(' · ')
              : 'Pressão, FC e temperatura da triagem — sem dados ainda'
          }
        />
      </div>

      <SinaisVitaisTendencia atendimentos={atendimentos} />

      <div className="grid gap-4 lg:grid-cols-3">
        <CartaoBloco titulo="Dados gerais">
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
            <div>
              <dt className="text-xs uppercase tracking-wide text-gray-500">Idade</dt>
              <dd className="text-sm text-gray-900">
                {calcularIdade(p.dataNascimento) != null ? `${calcularIdade(p.dataNascimento)} anos` : '—'}
              </dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-gray-500">Sexo</dt>
              <dd className="text-sm text-gray-900">{SEXO_LABEL[String(p.sexo)] ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-gray-500">Tipo sanguíneo</dt>
              <dd className="text-sm text-gray-900">
                {p.tipoSanguineo && p.tipoSanguineo !== 'NaoInformado'
                  ? `${TIPO_SANG_LABEL[String(p.tipoSanguineo)]} ${
                      p.fatorRh === 'Positivo' ? '+' : p.fatorRh === 'Negativo' ? '−' : ''
                    }`.trim()
                  : '—'}
              </dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-gray-500">IMC</dt>
              <dd className="text-sm text-gray-900">
                {imc ? (
                  <>
                    {imc.valor} <span className="text-xs text-gray-500">· {imc.classe}</span>
                  </>
                ) : (
                  '—'
                )}
              </dd>
            </div>
          </dl>
        </CartaoBloco>
        <CartaoBloco titulo="Alergias">
          <Chips itens={p.alergias} />
        </CartaoBloco>
        <CartaoBloco titulo="Medicamentos contínuos">
          <Chips itens={p.medicamentosContinuos} />
        </CartaoBloco>
        <CartaoBloco titulo="Comorbidades">
          <Chips itens={p.comorbidades} />
        </CartaoBloco>
        <CartaoBloco titulo="Deficiências">
          <Chips itens={p.deficiencias} />
        </CartaoBloco>
        <CartaoBloco titulo="Plano de saúde">
          <span className="text-sm text-gray-900">
            {p.planoSaude || <span className="text-gray-400">—</span>}
          </span>
        </CartaoBloco>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <CartaoBloco titulo="Atendimentos por tipo">
          {stats.porTipo.length === 0 ? (
            <span className="text-sm text-gray-400">{carregandoAtend ? 'Carregando…' : 'Sem dados.'}</span>
          ) : (
            <ul className="space-y-2">
              {stats.porTipo.map((t) => (
                <li key={t.tipo} className="flex items-center gap-3 text-sm">
                  <span className="w-28 shrink-0 text-gray-700">{t.tipo}</span>
                  <div className="h-2 flex-1 rounded-full bg-gray-100">
                    <div
                      className="h-2 rounded-full bg-red-500"
                      style={{ width: `${(t.n / maxTipo) * 100}%` }}
                    />
                  </div>
                  <span className="w-6 text-right tabular-nums text-gray-600">{t.n}</span>
                </li>
              ))}
            </ul>
          )}
        </CartaoBloco>
        <CartaoBloco titulo="Diagnósticos mais frequentes">
          {stats.topCid.length === 0 ? (
            <span className="text-sm text-gray-400">{carregandoAtend ? 'Carregando…' : 'Sem diagnósticos.'}</span>
          ) : (
            <ul className="space-y-1.5">
              {stats.topCid.map((d) => (
                <li key={d.codigo} className="flex items-baseline gap-2 text-sm">
                  <span className="rounded bg-gray-100 px-1.5 py-0.5 text-xs font-medium text-gray-700">
                    {d.codigo}
                  </span>
                  <span className="flex-1 truncate text-gray-700" title={d.descricao ?? undefined}>
                    {d.descricao ?? '—'}
                  </span>
                  <span className="tabular-nums text-xs text-gray-500">{d.n}×</span>
                </li>
              ))}
            </ul>
          )}
        </CartaoBloco>
      </div>
    </div>
  );
}

export function PacienteDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const id = params.id ?? '';
  const detalhe = usePacientePorId(id || null);
  const tratamentos = useListarTratamentos({ pacienteId: id });
  const atendimentos = useAtendimentosPaciente(id || null);
  const [vista, setVista] = useState<Vista>('resumo');

  const p = detalhe.data;
  const listaTratamentos = tratamentos.data ?? [];
  const tratamentosAtivos = listaTratamentos.filter((t) => t.ativo).length;
  const stats = useMemo(() => resumirAtendimentos(atendimentos.data ?? []), [atendimentos.data]);

  const colunasTratamentos: Coluna<TratamentoListItem>[] = [
    {
      chave: 'tipo',
      cabecalho: 'Tipo',
      render: (t) => (
        <button
          type="button"
          onClick={() => navigate(`/app/tratamentos/${t.id}`)}
          className="text-left font-medium text-red-700 hover:underline"
        >
          {t.tipoTratamentoNome ?? t.descricao}
        </button>
      ),
    },
    { chave: 'unidade', cabecalho: 'Unidade', render: (t) => t.unidadeNome },
    {
      chave: 'proxima',
      cabecalho: 'Próxima sessão',
      render: (t) => (t.proximaSessao ? formatarDataBr(t.proximaSessao) : '—'),
    },
    {
      chave: 'progresso',
      cabecalho: 'Progresso',
      render: (t) => (
        <span className="text-xs text-gray-600">
          {t.sessoesRealizadas}/{t.totalSessoes}
        </span>
      ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (t) => <StatusBadge ativo={t.ativo} />,
    },
  ];

  const abas: Aba[] = p ? [
    { id: 'identificacao', rotulo: 'Identificação', conteudo: <SecaoIdentificacao p={p} /> },
    { id: 'filiacao', rotulo: 'Filiação', conteudo: <SecaoFiliacao p={p} /> },
    { id: 'endereco', rotulo: 'Endereço', conteudo: <SecaoEndereco p={p} /> },
    { id: 'contatos', rotulo: 'Contatos', conteudo: <SecaoContatos p={p} /> },
    { id: 'saude', rotulo: 'Saúde', conteudo: <SecaoSaude p={p} /> },
    { id: 'documentos', rotulo: 'Documentos & Origem', conteudo: <SecaoDocumentos p={p} /> },
    {
      id: 'observacoes', rotulo: 'Observações',
      conteudo: (
        <p className="whitespace-pre-wrap text-sm text-gray-700">
          {p.observacoes || <span className="text-gray-400">Nenhuma observação registrada.</span>}
        </p>
      ),
    },
  ] : [];

  const navItens: { id: Vista; rotulo: string; badge?: number }[] = [
    { id: 'resumo', rotulo: 'Resumo' },
    { id: 'atendimentos', rotulo: 'Atendimentos', badge: stats.total },
    { id: 'tratamentos', rotulo: 'Tratamentos', badge: listaTratamentos.length },
    { id: 'exames', rotulo: 'Exames anexados' },
    { id: 'acessos', rotulo: 'Histórico de Acesso' },
    { id: 'auditoria', rotulo: 'Histórico de alterações' },
    { id: 'dados', rotulo: 'Dados pessoais' },
  ];

  const idade = calcularIdade(p?.dataNascimento);

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/app/pacientes')}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <Avatar src={p?.fotoBase64} nome={p?.nomeCompleto} tamanho="lg" />
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                {p?.nomeCompleto ?? 'Carregando…'}
              </h1>
              {p ? <NomePacienteComResumo pacienteId={p.id} /> : null}
              {p ? <StatusBadge ativo={p.ativo} /> : null}
            </div>
            {p ? (
              <p className="text-sm text-gray-500">
                CPF {formatarCpf(p.cpf)}
                {p.dataNascimento ? ` · Nasc. ${formatarData(p.dataNascimento.toString())}` : ''}
                {idade != null ? ` · ${idade} anos` : ''}
                {' · '}
                {SEXO_LABEL[String(p.sexo)] ?? String(p.sexo)}
              </p>
            ) : null}
          </div>
        </div>
        <Button onClick={() => navigate(`/app/pacientes/${id}/editar`)}>
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
        <>
          <div className="border-b border-gray-200">
            <nav className="-mb-px flex flex-wrap gap-1" role="tablist">
              {navItens.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  role="tab"
                  aria-selected={vista === item.id}
                  onClick={() => setVista(item.id)}
                  className={
                    '-mb-px whitespace-nowrap border-b-2 px-4 py-2 text-sm font-medium transition-colors ' +
                    (vista === item.id
                      ? 'border-red-600 text-red-700'
                      : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700')
                  }
                >
                  {item.rotulo}
                  {item.badge !== undefined ? (
                    <span className="ml-2 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                      {item.badge}
                    </span>
                  ) : null}
                </button>
              ))}
            </nav>
          </div>

          {vista === 'resumo' ? (
            <ResumoPaciente
              p={p}
              stats={stats}
              atendimentos={atendimentos.data ?? []}
              carregandoAtend={atendimentos.isLoading}
              qtdTratamentos={listaTratamentos.length}
              tratamentosAtivos={tratamentosAtivos}
              irPara={setVista}
            />
          ) : null}

          {vista === 'atendimentos' ? (
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <SecaoAtendimentos pacienteId={id} paciente={p} />
            </div>
          ) : null}

          {vista === 'tratamentos' ? (
            <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <div className="mb-3 flex items-center justify-between gap-2">
                <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
                  <ListChecks className="h-4 w-4" /> Tratamentos do paciente
                  <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-600">
                    {listaTratamentos.length}
                  </span>
                </h2>
                <Button variante="outline" tamanho="sm" onClick={() => navigate('/app/tratamentos/novo')}>
                  Novo tratamento
                </Button>
              </div>
              <Tabela
                colunas={colunasTratamentos}
                dados={listaTratamentos}
                chaveLinha={(t) => t.id}
                carregando={tratamentos.isLoading}
                vazio={
                  !tratamentos.isLoading && listaTratamentos.length === 0
                    ? 'Nenhum tratamento cadastrado para este paciente.'
                    : undefined
                }
              />
            </section>
          ) : null}

          {vista === 'exames' ? (
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <SecaoExamesAnexados pacienteId={id} />
            </div>
          ) : null}

          {vista === 'acessos' ? (
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <SecaoAcessos pacienteId={id} />
            </div>
          ) : null}

          {vista === 'auditoria' ? (
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <SecaoAuditoriaPaciente pacienteId={id} />
            </div>
          ) : null}

          {vista === 'dados' ? (
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
              <Tabs abas={abas} />
            </div>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
