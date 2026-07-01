import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { ArrowLeft, Loader2, Search } from 'lucide-react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { VerificarNomeBotao } from '@/features/pacientes/components/NomeCompletoVerificavel';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { BotaoValidarTelefone } from '@/features/telefone-validacao/components/BotaoValidarTelefone';
import { Select } from '@/shared/ui/Select';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { ListaChips } from '@/shared/ui/ListaChips';
import { UploadFoto } from '@/shared/ui/UploadFoto';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import {
  consultarPacientePorCpf,
  useAtualizarPaciente,
  useCadastrarPaciente,
  usePacientePorId,
  useReativarPaciente,
} from '@/features/pacientes/api/queries';
import {
  consultarCep,
  consultarCns,
  consultarCpf,
  type ConsultaCnsResposta,
  type ConsultaCpfResposta,
} from '@/shared/api/integracoes';
import { cpfValido, pacienteFormSchema } from '@/features/pacientes/schemas/pacienteSchema';
import { apenasDigitosCpf } from '@/shared/lib/cpf';
import {
  ESCOLARIDADES,
  ESTADOS_CIVIS,
  FATORES_RH,
  RACAS,
  SEXOS,
  TIPOS_SANGUINEOS,
  type Escolaridade,
  type EstadoCivil,
  type FatorRh,
  type Paciente,
  type RacaCor,
  type Sexo,
  type TipoSanguineo,
} from '@/features/pacientes/types';

type Modo = 'criar' | 'editar';

type Endereco = {
  cep: string;
  logradouro: string;
  numero: string;
  complemento: string;
  bairro: string;
  cidade: string;
  uf: string;
  pontoReferencia: string;
};

type ContatoEmergencia = {
  nome: string;
  parentesco: string;
  telefone: string;
};

type Estado = {
  nomeCompleto: string;
  nomeSocial: string;
  cpf: string;
  dataNascimento: string;
  cns: string;
  rg: string;
  sexo: Sexo;
  estadoCivil: EstadoCivil;
  racaCor: RacaCor;
  escolaridade: Escolaridade;
  ocupacao: string;
  naturalidade: string;
  nacionalidade: string;
  nomeDaMae: string;
  nomeDoPai: string;
  responsavelLegal: string;
  endereco: Endereco;
  telefonePrincipal: string;
  telefoneCelular: string;
  telefoneResidencial: string;
  email: string;
  contatoEmergencia: ContatoEmergencia;
  alturaCm: string;
  pesoKg: string;
  tipoSanguineo: TipoSanguineo;
  fatorRh: FatorRh;
  alergias: string[];
  medicamentosContinuos: string[];
  comorbidades: string[];
  deficiencias: string[];
  planoSaude: string;
  observacoes: string;
  fotoBase64: string | null;
};

const ENDERECO_VAZIO: Endereco = {
  cep: '', logradouro: '', numero: '', complemento: '',
  bairro: '', cidade: '', uf: '', pontoReferencia: '',
};
const CONTATO_VAZIO: ContatoEmergencia = { nome: '', parentesco: '', telefone: '' };

/** Tradução dos paths do Zod para rótulos exibíveis no erro global. */
const LABELS_CAMPOS: Record<string, string> = {
  nomeCompleto: 'Nome completo',
  nomeSocial: 'Nome social',
  cpf: 'CPF',
  dataNascimento: 'Data de nascimento',
  cns: 'CNS',
  rg: 'RG',
  sexo: 'Sexo',
  estadoCivil: 'Estado civil',
  racaCor: 'Raça/cor',
  escolaridade: 'Escolaridade',
  ocupacao: 'Ocupação',
  naturalidade: 'Naturalidade',
  nacionalidade: 'Nacionalidade',
  nomeDaMae: 'Nome da mãe',
  nomeDoPai: 'Nome do pai',
  responsavelLegal: 'Responsável legal',
  telefonePrincipal: 'Telefone principal',
  telefoneCelular: 'Celular',
  telefoneResidencial: 'Telefone residencial',
  email: 'E-mail',
  alturaCm: 'Altura (cm)',
  pesoKg: 'Peso (kg)',
  tipoSanguineo: 'Tipo sanguíneo',
  fatorRh: 'Fator Rh',
  alergias: 'Alergias',
  medicamentosContinuos: 'Medicamentos contínuos',
  comorbidades: 'Comorbidades',
  deficiencias: 'Deficiências',
  planoSaude: 'Plano de saúde',
  observacoes: 'Observações',
  'endereco.cep': 'CEP',
  'endereco.logradouro': 'Logradouro',
  'endereco.numero': 'Número',
  'endereco.complemento': 'Complemento',
  'endereco.bairro': 'Bairro',
  'endereco.cidade': 'Cidade',
  'endereco.uf': 'UF',
  'endereco.pontoReferencia': 'Ponto de referência',
  'contatoEmergencia.nome': 'Nome do contato de emergência',
  'contatoEmergencia.parentesco': 'Parentesco do contato de emergência',
  'contatoEmergencia.telefone': 'Telefone do contato de emergência',
};

const ESTADO_INICIAL: Estado = {
  nomeCompleto: '', nomeSocial: '', cpf: '', dataNascimento: '', cns: '', rg: '',
  sexo: 'NaoInformado', estadoCivil: 'NaoInformado', racaCor: 'NaoInformado',
  escolaridade: 'NaoInformado', ocupacao: '', naturalidade: '', nacionalidade: 'Brasileira',
  nomeDaMae: '', nomeDoPai: '', responsavelLegal: '',
  endereco: ENDERECO_VAZIO,
  telefonePrincipal: '', telefoneCelular: '', telefoneResidencial: '', email: '',
  contatoEmergencia: CONTATO_VAZIO,
  alturaCm: '', pesoKg: '',
  tipoSanguineo: 'NaoInformado', fatorRh: 'NaoInformado',
  alergias: [], medicamentosContinuos: [], comorbidades: [], deficiencias: [],
  planoSaude: '', observacoes: '', fotoBase64: null,
};

const RIOLABEL: Record<Sexo, string> = {
  NaoInformado: 'Não informado', Masculino: 'Masculino', Feminino: 'Feminino', Outro: 'Outro',
};
const ESTADO_CIVIL_LABEL: Record<EstadoCivil, string> = {
  NaoInformado: 'Não informado', Solteiro: 'Solteiro(a)', Casado: 'Casado(a)',
  UniaoEstavel: 'União estável', Divorciado: 'Divorciado(a)', Viuvo: 'Viúvo(a)', Separado: 'Separado(a)',
};
const RACA_LABEL: Record<RacaCor, string> = {
  NaoInformado: 'Não informado', Branca: 'Branca', Preta: 'Preta',
  Parda: 'Parda', Amarela: 'Amarela', Indigena: 'Indígena',
};
const ESCOLARIDADE_LABEL: Record<Escolaridade, string> = {
  NaoInformado: 'Não informado', Analfabeto: 'Analfabeto',
  SemEscolaridade: 'Sem escolaridade',
  FundamentalIncompleto: 'Fundamental incompleto', FundamentalCompleto: 'Fundamental completo',
  MedioIncompleto: 'Médio incompleto', MedioCompleto: 'Médio completo',
  SuperiorIncompleto: 'Superior incompleto', SuperiorCompleto: 'Superior completo',
  PosGraduacao: 'Pós-graduação',
};
const TIPO_SANG_LABEL: Record<TipoSanguineo, string> = {
  NaoInformado: 'Não informado', A: 'A', B: 'B', AB: 'AB', O: 'O',
};
const FATOR_RH_LABEL: Record<FatorRh, string> = {
  NaoInformado: 'Não informado', Positivo: 'Positivo (+)', Negativo: 'Negativo (−)',
};

function parseDataBr(data: string): string {
  // "14/02/1983" → "1983-02-14"
  const m = data.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
  return m ? `${m[3]}-${m[2]}-${m[1]}` : '';
}

function pacienteParaEstado(p: Paciente): Estado {
  return {
    nomeCompleto: p.nomeCompleto,
    nomeSocial: p.nomeSocial ?? '',
    cpf: p.cpf,
    dataNascimento: p.dataNascimento ?? '',
    cns: p.cns ?? '',
    rg: p.rg ?? '',
    sexo: p.sexo,
    estadoCivil: p.estadoCivil,
    racaCor: p.racaCor,
    escolaridade: p.escolaridade,
    ocupacao: p.ocupacao ?? '',
    naturalidade: p.naturalidade ?? '',
    nacionalidade: p.nacionalidade ?? 'Brasileira',
    nomeDaMae: p.nomeDaMae ?? '',
    nomeDoPai: p.nomeDoPai ?? '',
    responsavelLegal: p.responsavelLegal ?? '',
    endereco: p.endereco
      ? {
          cep: p.endereco.cep ?? '',
          logradouro: p.endereco.logradouro ?? '',
          numero: p.endereco.numero ?? '',
          complemento: p.endereco.complemento ?? '',
          bairro: p.endereco.bairro ?? '',
          cidade: p.endereco.cidade ?? '',
          uf: p.endereco.uf ?? '',
          pontoReferencia: p.endereco.pontoReferencia ?? '',
        }
      : ENDERECO_VAZIO,
    telefonePrincipal: p.telefonePrincipal ?? '',
    telefoneCelular: p.telefoneCelular ?? '',
    telefoneResidencial: p.telefoneResidencial ?? '',
    email: p.email ?? '',
    contatoEmergencia: p.contatoEmergencia
      ? {
          nome: p.contatoEmergencia.nome ?? '',
          parentesco: p.contatoEmergencia.parentesco ?? '',
          telefone: p.contatoEmergencia.telefone ?? '',
        }
      : CONTATO_VAZIO,
    alturaCm: p.alturaCm != null ? String(p.alturaCm) : '',
    pesoKg: p.pesoKg != null ? String(p.pesoKg) : '',
    tipoSanguineo: p.tipoSanguineo,
    fatorRh: p.fatorRh,
    alergias: [...p.alergias],
    medicamentosContinuos: [...p.medicamentosContinuos],
    comorbidades: [...p.comorbidades],
    deficiencias: [...p.deficiencias],
    planoSaude: p.planoSaude ?? '',
    observacoes: p.observacoes ?? '',
    fotoBase64: p.fotoBase64 ?? null,
  };
}

function estadoParaPayload(e: Estado) {
  const enderecoVazio = !e.endereco.cep && !e.endereco.logradouro && !e.endereco.bairro;
  const contatoVazio = !e.contatoEmergencia.nome && !e.contatoEmergencia.telefone;
  return {
    nomeCompleto: e.nomeCompleto.trim(),
    nomeSocial: e.nomeSocial.trim() || null,
    cpf: e.cpf.replace(/\D/g, ''),
    dataNascimento: e.dataNascimento,
    cns: e.cns ? e.cns.replace(/\D/g, '') : null,
    rg: e.rg || null,
    sexo: e.sexo,
    estadoCivil: e.estadoCivil,
    racaCor: e.racaCor,
    escolaridade: e.escolaridade,
    ocupacao: e.ocupacao || null,
    naturalidade: e.naturalidade || null,
    nacionalidade: e.nacionalidade || 'Brasileira',
    nomeDaMae: e.nomeDaMae || null,
    nomeDoPai: e.nomeDoPai || null,
    responsavelLegal: e.responsavelLegal || null,
    endereco: enderecoVazio
      ? null
      : {
          cep: e.endereco.cep.replace(/\D/g, ''),
          logradouro: e.endereco.logradouro,
          numero: e.endereco.numero || null,
          complemento: e.endereco.complemento || null,
          bairro: e.endereco.bairro,
          cidade: e.endereco.cidade,
          uf: e.endereco.uf.toUpperCase(),
          pontoReferencia: e.endereco.pontoReferencia || null,
        },
    telefonePrincipal: e.telefonePrincipal || null,
    telefoneCelular: e.telefoneCelular || null,
    telefoneResidencial: e.telefoneResidencial || null,
    email: e.email || null,
    contatoEmergencia: contatoVazio
      ? null
      : {
          nome: e.contatoEmergencia.nome,
          parentesco: e.contatoEmergencia.parentesco || null,
          telefone: e.contatoEmergencia.telefone,
        },
    alturaCm: e.alturaCm ? Number(e.alturaCm) : null,
    pesoKg: e.pesoKg ? Number(e.pesoKg) : null,
    tipoSanguineo: e.tipoSanguineo,
    fatorRh: e.fatorRh,
    alergias: e.alergias,
    medicamentosContinuos: e.medicamentosContinuos,
    comorbidades: e.comorbidades,
    deficiencias: e.deficiencias,
    planoSaude: e.planoSaude || null,
    observacoes: e.observacoes || null,
    fotoBase64: e.fotoBase64,
  };
}

export function PacienteFormPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const params = useParams<{ id?: string }>();
  const modo: Modo = params.id ? 'editar' : 'criar';

  // CPF pré-preenchido quando o cadastro foi iniciado a partir de uma busca por
  // CPF válido (ex.: tela de solicitação → "Cadastrar paciente"). `origem`
  // marca de onde o fluxo nasceu (ex.: 'solicitacao') para, ao concluir,
  // sugerir criar a solicitação já com o paciente recém-cadastrado.
  const navState = (location.state as { termo?: string; origem?: string } | null) ?? null;
  const cpfPreFill = navState?.termo && cpfValido(navState.termo) ? apenasDigitosCpf(navState.termo) : '';
  const origemSolicitacao = navState?.origem === 'solicitacao';

  const [estado, setEstado] = useState<Estado>(() =>
    cpfPreFill ? { ...ESTADO_INICIAL, cpf: cpfPreFill } : ESTADO_INICIAL,
  );
  const [erros, setErros] = useState<Record<string, string>>({});
  const [erroGlobal, setErroGlobal] = useState<string | null>(null);
  const [passoCpfConcluido, setPassoCpfConcluido] = useState<boolean>(modo === 'editar');
  // Método de identificação inicial: por CPF (hub da Receita) ou por CNS (SISREG/CADSUS).
  // No CNS a data de nascimento não é pedida — o SISREG a devolve.
  const [metodoBusca, setMetodoBusca] = useState<'cpf' | 'cns'>('cpf');
  const [consultandoCpf, setConsultandoCpf] = useState(false);
  const [consultandoCns, setConsultandoCns] = useState(false);
  const [consultandoCep, setConsultandoCep] = useState(false);
  const [reativacaoPendente, setReativacaoPendente] = useState<{ id: string; nome: string } | null>(null);
  // Após cadastrar vindo da solicitação, sugere abrir a solicitação já preenchida.
  const [sugerirSolicitacao, setSugerirSolicitacao] = useState<{ id: string; nome: string } | null>(null);

  const detalhe = usePacientePorId(params.id ?? null);
  const cadastrar = useCadastrarPaciente();
  const atualizar = useAtualizarPaciente();
  const reativar = useReativarPaciente();

  useEffect(() => {
    if (modo === 'editar' && detalhe.data) {
      setEstado(pacienteParaEstado(detalhe.data));
    }
  }, [modo, detalhe.data]);

  function atualizarCampo<K extends keyof Estado>(campo: K, valor: Estado[K]) {
    setEstado((s) => ({ ...s, [campo]: valor }));
  }

  function atualizarEndereco<K extends keyof Endereco>(campo: K, valor: Endereco[K]) {
    setEstado((s) => ({ ...s, endereco: { ...s.endereco, [campo]: valor } }));
  }

  function atualizarContato<K extends keyof ContatoEmergencia>(campo: K, valor: ContatoEmergencia[K]) {
    setEstado((s) => ({ ...s, contatoEmergencia: { ...s.contatoEmergencia, [campo]: valor } }));
  }

  async function aoConfirmarPasso1() {
    setErros({});
    setErroGlobal(null);

    if (!cpfValido(estado.cpf)) {
      setErros({ cpf: 'CPF inválido.' });
      return;
    }
    if (!estado.dataNascimento) {
      setErros({ dataNascimento: 'Informe a data de nascimento.' });
      return;
    }

    const cpfLimpo = estado.cpf.replace(/\D/g, '');

    setConsultandoCpf(true);
    try {
      const existente = await consultarPacientePorCpf(cpfLimpo);
      if (existente) {
        if (existente.ativo) {
          setErroGlobal(`Já existe paciente ativo com este CPF: ${existente.nomeCompleto}.`);
          return;
        }
        setReativacaoPendente({ id: existente.id, nome: existente.nomeCompleto });
        return;
      }

      const hub: ConsultaCpfResposta = await consultarCpf(cpfLimpo, estado.dataNascimento);
      const dataIso = parseDataBr(hub.dataNascimento) || estado.dataNascimento;
      const sexoHub = hub.sexo && (SEXOS as readonly string[]).includes(hub.sexo)
        ? (hub.sexo as Sexo)
        : null;
      setEstado((s) => ({
        ...s,
        nomeCompleto: hub.nome.trim(),
        cpf: hub.cpf,
        dataNascimento: dataIso,
        // Pré-preenche o sexo quando a Receita/Hub retorna; senão mantém o atual.
        sexo: sexoHub ?? s.sexo,
      }));
      setPassoCpfConcluido(true);
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    } finally {
      setConsultandoCpf(false);
    }
  }

  async function aoConfirmarPasso1Cns() {
    setErros({});
    setErroGlobal(null);

    const cnsLimpo = estado.cns.replace(/\D/g, '');
    if (cnsLimpo.length !== 15) {
      setErros({ cns: 'CNS precisa ter 15 dígitos.' });
      return;
    }

    setConsultandoCns(true);
    try {
      const sisreg: ConsultaCnsResposta = await consultarCns(cnsLimpo);

      const cpfLimpo = (sisreg.cpf ?? '').replace(/\D/g, '');
      if (cpfLimpo.length !== 11) {
        setErroGlobal('O SISREG não retornou um CPF para este CNS. Cadastre o paciente pela busca por CPF.');
        return;
      }
      if (!sisreg.dataNascimento) {
        setErroGlobal('O SISREG não informou a data de nascimento deste CNS. Cadastre o paciente pela busca por CPF.');
        return;
      }

      // Dedup/reativação por CPF — mesmo comportamento do fluxo por CPF.
      const existente = await consultarPacientePorCpf(cpfLimpo);
      if (existente) {
        if (existente.ativo) {
          setErroGlobal(`Já existe paciente ativo com este CPF: ${existente.nomeCompleto}.`);
          return;
        }
        setReativacaoPendente({ id: existente.id, nome: existente.nomeCompleto });
        return;
      }

      const sexoSisreg = sisreg.sexo && (SEXOS as readonly string[]).includes(sisreg.sexo)
        ? (sisreg.sexo as Sexo)
        : null;
      setEstado((s) => ({
        ...s,
        nomeCompleto: sisreg.nome.trim(),
        cpf: cpfLimpo,
        cns: sisreg.cns || cnsLimpo,
        dataNascimento: sisreg.dataNascimento ?? s.dataNascimento,
        // Sexo e nome da mãe vêm do CADSUS quando disponíveis (endereço/telefone NÃO — vêm errados).
        sexo: sexoSisreg ?? s.sexo,
        nomeDaMae: sisreg.nomeMae?.trim() || s.nomeDaMae,
      }));
      setPassoCpfConcluido(true);
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    } finally {
      setConsultandoCns(false);
    }
  }

  async function buscarCep() {
    const cep = estado.endereco.cep.replace(/\D/g, '');
    if (cep.length !== 8) {
      setErros({ 'endereco.cep': 'CEP precisa ter 8 dígitos.' });
      return;
    }
    setConsultandoCep(true);
    setErros({});
    try {
      const r = await consultarCep(cep);
      setEstado((s) => ({
        ...s,
        endereco: {
          ...s.endereco,
          cep,
          logradouro: r.logradouro || s.endereco.logradouro,
          bairro: r.bairro || s.endereco.bairro,
          cidade: r.localidade || s.endereco.cidade,
          uf: r.uf || s.endereco.uf,
          complemento: r.complemento || s.endereco.complemento,
        },
      }));
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    } finally {
      setConsultandoCep(false);
    }
  }

  async function aoConfirmarReativar() {
    if (!reativacaoPendente) return;
    try {
      await reativar.mutateAsync(reativacaoPendente.id);
      navigate(`/app/pacientes/${reativacaoPendente.id}/editar`, { replace: true });
    } catch (e) {
      setErroGlobal(extrairMensagemDeErro(e));
    }
  }

  async function aoSalvar(e: FormEvent) {
    e.preventDefault();
    setErros({});
    setErroGlobal(null);

    const payload = estadoParaPayload(estado);
    const parsed = pacienteFormSchema.safeParse({
      ...payload,
      cns: payload.cns ?? undefined,
    });
    if (!parsed.success) {
      const novos: Record<string, string> = {};
      for (const issue of parsed.error.issues) {
        const path = issue.path.join('.');
        if (!novos[path]) novos[path] = issue.message;
      }
      setErros(novos);
      const detalhes = Object.entries(novos)
        .map(([path, msg]) => `${LABELS_CAMPOS[path] ?? path}: ${msg}`);
      setErroGlobal(
        detalhes.length === 1
          ? detalhes[0]
          : `Corrija ${detalhes.length} campo(s): ${detalhes.join(' · ')}`,
      );
      return;
    }

    try {
      if (modo === 'criar') {
        const id = await cadastrar.mutateAsync(payload);
        // O cadastro já salvou tudo (todas as abas estão neste formulário). Não
        // mandamos mais para "/editar" — isso fazia o usuário achar que precisava
        // "Salvar alterações" de novo. Se o fluxo nasceu da solicitação, oferecemos
        // criar a solicitação já com o paciente; senão, voltamos para a lista.
        if (origemSolicitacao) {
          setSugerirSolicitacao({ id, nome: payload.nomeCompleto });
        } else {
          navigate('/app/pacientes', { replace: true });
        }
      } else if (params.id) {
        // Nome, CPF e data de nascimento são imutáveis — não vão no payload.
        const { nomeCompleto: _nc, cpf: _cpf, dataNascimento: _dn, ...resto } = payload;
        await atualizar.mutateAsync({ id: params.id, payload: resto });
        navigate('/app/pacientes', { replace: false });
      }
    } catch (err) {
      setErroGlobal(extrairMensagemDeErro(err));
    }
  }

  const carregando = modo === 'editar' && detalhe.isFetching && !detalhe.data;
  const salvando = cadastrar.isPending || atualizar.isPending;

  const abas = useMemo<Aba[]>(() => [
    {
      id: 'identificacao',
      rotulo: 'Identificação',
      conteudo: <SecaoIdentificacao estado={estado} erros={erros} setCampo={atualizarCampo} pacienteId={params.id} />,
    },
    {
      id: 'filiacao',
      rotulo: 'Filiação',
      conteudo: <SecaoFiliacao estado={estado} erros={erros} setCampo={atualizarCampo} />,
    },
    {
      id: 'endereco',
      rotulo: 'Endereço',
      conteudo: (
        <SecaoEndereco
          estado={estado}
          erros={erros}
          setEndereco={atualizarEndereco}
          buscandoCep={consultandoCep}
          aoBuscarCep={buscarCep}
        />
      ),
    },
    {
      id: 'contatos',
      rotulo: 'Contatos',
      conteudo: (
        <SecaoContatos
          estado={estado}
          erros={erros}
          setCampo={atualizarCampo}
          setContato={atualizarContato}
        />
      ),
    },
    {
      id: 'saude',
      rotulo: 'Saúde',
      conteudo: <SecaoSaude estado={estado} erros={erros} setCampo={atualizarCampo} />,
    },
    {
      id: 'observacoes',
      rotulo: 'Observações',
      conteudo: <SecaoObservacoes estado={estado} erros={erros} setCampo={atualizarCampo} />,
    },
  ], [estado, erros, consultandoCep]);

  if (modo === 'criar' && !passoCpfConcluido) {
    return (
      <div className="space-y-6">
        <Cabecalho titulo="Novo paciente" voltar={() => navigate('/app/pacientes')} />
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="text-base font-medium text-gray-900">Identificação inicial</h2>
          <p className="mt-1 text-sm text-gray-600">
            {metodoBusca === 'cpf'
              ? 'Informe o CPF e a data de nascimento. Esses dados não poderão ser editados depois.'
              : 'Informe o CNS (Cartão SUS). Buscamos os dados no SISREG — sem precisar da data de nascimento.'}
          </p>

          {/* Método de identificação: CPF (hub da Receita) ou CNS (SISREG/CADSUS). */}
          <div className="mt-4 inline-flex rounded-lg border border-gray-200 bg-gray-50 p-1">
            <button
              type="button"
              onClick={() => { setMetodoBusca('cpf'); setErros({}); setErroGlobal(null); }}
              className={`rounded-md px-4 py-1.5 text-sm font-medium ${
                metodoBusca === 'cpf' ? 'bg-white text-primary-700 shadow-sm' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Por CPF
            </button>
            <button
              type="button"
              onClick={() => { setMetodoBusca('cns'); setErros({}); setErroGlobal(null); }}
              className={`rounded-md px-4 py-1.5 text-sm font-medium ${
                metodoBusca === 'cns' ? 'bg-white text-primary-700 shadow-sm' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Por CNS (SISREG)
            </button>
          </div>

          {metodoBusca === 'cpf' ? (
            <div className="mt-5 grid max-w-xl grid-cols-1 gap-4 md:grid-cols-2">
              <Campo label="CPF" htmlFor="cpf" erro={erros.cpf} required>
                <Input
                  id="cpf"
                  value={estado.cpf}
                  onChange={(e) => atualizarCampo('cpf', e.target.value)}
                  placeholder="00000000000"
                  inputMode="numeric"
                  autoFocus
                />
              </Campo>
              <Campo label="Data de nascimento" htmlFor="dataNascimento" erro={erros.dataNascimento} required>
                <Input
                  id="dataNascimento"
                  type="date"
                  value={estado.dataNascimento}
                  onChange={(e) => atualizarCampo('dataNascimento', e.target.value)}
                />
              </Campo>
            </div>
          ) : (
            <div className="mt-5 grid max-w-xl grid-cols-1 gap-4">
              <Campo label="CNS (Cartão SUS)" htmlFor="cnsBusca" erro={erros.cns} required>
                <Input
                  id="cnsBusca"
                  value={estado.cns}
                  onChange={(e) => atualizarCampo('cns', e.target.value)}
                  placeholder="000000000000000"
                  inputMode="numeric"
                  autoFocus
                />
              </Campo>
            </div>
          )}

          {erroGlobal ? (
            <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erroGlobal}
            </div>
          ) : null}

          {reativacaoPendente ? (
            <div className="mt-4 rounded-md border border-amber-300 bg-amber-50 p-4">
              <p className="text-sm text-amber-900">
                Existe um paciente <strong>desativado</strong> com este CPF:{' '}
                <strong>{reativacaoPendente.nome}</strong>. Deseja reativar o cadastro existente?
              </p>
              <div className="mt-3 flex gap-2">
                <Button onClick={aoConfirmarReativar} disabled={reativar.isPending}>
                  {reativar.isPending ? 'Reativando…' : 'Reativar cadastro'}
                </Button>
                <Button variante="ghost" onClick={() => setReativacaoPendente(null)}>
                  Cancelar
                </Button>
              </div>
            </div>
          ) : null}

          <div className="mt-6 flex justify-end gap-3">
            <Button variante="ghost" onClick={() => navigate('/app/pacientes')}>
              Cancelar
            </Button>
            <Button
              onClick={metodoBusca === 'cpf' ? aoConfirmarPasso1 : aoConfirmarPasso1Cns}
              disabled={consultandoCpf || consultandoCns}
            >
              {consultandoCpf || consultandoCns ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Consultando…
                </>
              ) : (
                <>
                  <Search className="mr-2 h-4 w-4" /> Consultar e prosseguir
                </>
              )}
            </Button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <>
    <form onSubmit={aoSalvar} className="space-y-6">
      <Cabecalho
        titulo={modo === 'criar' ? 'Novo paciente' : 'Editar paciente'}
        subtitulo={estado.nomeCompleto || undefined}
        voltar={() => navigate('/app/pacientes')}
      />

      {/* Ações duplicadas no topo para o operador não precisar rolar até o fim. */}
      <BarraAcoes salvando={salvando} modo={modo} aoCancelar={() => navigate('/app/pacientes')} />

      {carregando ? (
        <div className="text-sm text-gray-500">Carregando dados…</div>
      ) : (
        <>
          <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <UploadFoto
              valor={estado.fotoBase64}
              aoMudar={(v) => atualizarCampo('fotoBase64', v)}
              nome={estado.nomeCompleto || undefined}
              desabilitado={salvando}
            />
          </div>
          <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
            <Tabs abas={abas} />
          </div>
        </>
      )}

      {erroGlobal ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroGlobal}
        </div>
      ) : null}

      <BarraAcoes salvando={salvando} modo={modo} aoCancelar={() => navigate('/app/pacientes')} />
    </form>

    <ConfirmDialog
      aberto={!!sugerirSolicitacao}
      titulo="Paciente cadastrado"
      mensagem={`Cadastro concluído. Deseja criar uma solicitação de exame para ${sugerirSolicitacao?.nome ?? ''}?`}
      rotuloConfirmar="Criar solicitação"
      rotuloCancelar="Agora não"
      aoConfirmar={() => {
        const sug = sugerirSolicitacao;
        setSugerirSolicitacao(null);
        if (sug) {
          // Abre a solicitação já com o paciente selecionado — sem buscar de novo.
          navigate('/app/solicitacoes-exame/novo', {
            state: { pacienteCriado: { id: sug.id, nomeCompleto: sug.nome } },
          });
        }
      }}
      aoCancelar={() => {
        setSugerirSolicitacao(null);
        navigate('/app/pacientes', { replace: true });
      }}
    />
    </>
  );
}

function BarraAcoes({
  salvando,
  modo,
  aoCancelar,
}: {
  salvando: boolean;
  modo: Modo;
  aoCancelar: () => void;
}) {
  return (
    <div className="flex justify-end gap-3">
      <Button variante="ghost" type="button" onClick={aoCancelar} disabled={salvando}>
        Cancelar
      </Button>
      <Button type="submit" disabled={salvando}>
        {salvando ? 'Salvando…' : modo === 'criar' ? 'Cadastrar paciente' : 'Salvar alterações'}
      </Button>
    </div>
  );
}

function Cabecalho({ titulo, subtitulo, voltar }: { titulo: string; subtitulo?: string; voltar: () => void }) {
  return (
    <header className="flex items-start justify-between gap-4">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={voltar}
          className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
          aria-label="Voltar"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">{titulo}</h1>
          {subtitulo ? <p className="text-sm text-gray-600">{subtitulo}</p> : null}
        </div>
      </div>
    </header>
  );
}

type SecProps = {
  estado: Estado;
  erros: Record<string, string>;
  setCampo: <K extends keyof Estado>(campo: K, valor: Estado[K]) => void;
  /** Id do paciente em modo edição — habilita o botão "Verificar" nome. */
  pacienteId?: string;
};

function SecaoIdentificacao({ estado, erros, setCampo, pacienteId }: SecProps) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      <Campo label="Nome completo" htmlFor="nomeCompleto" className="md:col-span-2"
        dica="Nome oficial — corrija pelo botão “Verificar” (recheca no CPF, com auditoria).">
        <div className="flex items-center gap-2">
          <Input id="nomeCompleto" value={estado.nomeCompleto} disabled readOnly className="flex-1" />
          {pacienteId ? (
            <VerificarNomeBotao
              pacienteId={pacienteId}
              nomeCompleto={estado.nomeCompleto}
              cpf={estado.cpf}
              dataNascimento={estado.dataNascimento}
              onSalvo={(novoNome) => setCampo('nomeCompleto', novoNome)}
            />
          ) : null}
        </div>
      </Campo>
      <Campo
        label="Nome social"
        htmlFor="nomeSocial"
        className="md:col-span-2"
        dica="Nome pelo qual o paciente gosta de ser chamado (opcional)."
      >
        <Input
          id="nomeSocial"
          value={estado.nomeSocial}
          onChange={(e) => setCampo('nomeSocial', e.target.value)}
          maxLength={200}
        />
      </Campo>
      <Campo label="CPF" htmlFor="cpf" dica="Não pode ser alterado.">
        <Input id="cpf" value={estado.cpf} disabled readOnly />
      </Campo>
      <Campo label="Data de nascimento" htmlFor="dataNascimento" dica="Imutável.">
        <Input id="dataNascimento" type="date" value={estado.dataNascimento} disabled readOnly />
      </Campo>
      <Campo label="CNS (Cartão SUS)" htmlFor="cns" erro={erros.cns}>
        <Input id="cns" value={estado.cns} onChange={(e) => setCampo('cns', e.target.value)} placeholder="000000000000000" inputMode="numeric" />
      </Campo>
      <Campo label="RG" htmlFor="rg" erro={erros.rg}>
        <Input id="rg" value={estado.rg} onChange={(e) => setCampo('rg', e.target.value)} />
      </Campo>
      <Campo label="Sexo" htmlFor="sexo" erro={erros.sexo}>
        <Select id="sexo" value={estado.sexo} onChange={(e) => setCampo('sexo', e.target.value as Sexo)}>
          {SEXOS.map((s) => <option key={s} value={s}>{RIOLABEL[s]}</option>)}
        </Select>
      </Campo>
      <Campo label="Estado civil" htmlFor="estadoCivil" erro={erros.estadoCivil}>
        <Select id="estadoCivil" value={estado.estadoCivil} onChange={(e) => setCampo('estadoCivil', e.target.value as EstadoCivil)}>
          {ESTADOS_CIVIS.map((s) => <option key={s} value={s}>{ESTADO_CIVIL_LABEL[s]}</option>)}
        </Select>
      </Campo>
      <Campo label="Raça/Cor" htmlFor="racaCor" erro={erros.racaCor}>
        <Select id="racaCor" value={estado.racaCor} onChange={(e) => setCampo('racaCor', e.target.value as RacaCor)}>
          {RACAS.map((s) => <option key={s} value={s}>{RACA_LABEL[s]}</option>)}
        </Select>
      </Campo>
      <Campo label="Escolaridade" htmlFor="escolaridade" erro={erros.escolaridade}>
        <Select id="escolaridade" value={estado.escolaridade} onChange={(e) => setCampo('escolaridade', e.target.value as Escolaridade)}>
          {ESCOLARIDADES.map((s) => <option key={s} value={s}>{ESCOLARIDADE_LABEL[s]}</option>)}
        </Select>
      </Campo>
      <Campo label="Ocupação / profissão" htmlFor="ocupacao" erro={erros.ocupacao}>
        <Input id="ocupacao" value={estado.ocupacao} onChange={(e) => setCampo('ocupacao', e.target.value)} />
      </Campo>
      <Campo label="Naturalidade" htmlFor="naturalidade" erro={erros.naturalidade} dica="Cidade/UF de nascimento.">
        <Input id="naturalidade" value={estado.naturalidade} onChange={(e) => setCampo('naturalidade', e.target.value)} />
      </Campo>
      <Campo label="Nacionalidade" htmlFor="nacionalidade" erro={erros.nacionalidade}>
        <Input id="nacionalidade" value={estado.nacionalidade} onChange={(e) => setCampo('nacionalidade', e.target.value)} />
      </Campo>
    </div>
  );
}

function SecaoFiliacao({ estado, erros, setCampo }: SecProps) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      <Campo label="Nome da mãe" htmlFor="nomeDaMae" erro={erros.nomeDaMae} className="md:col-span-2">
        <Input id="nomeDaMae" value={estado.nomeDaMae} onChange={(e) => setCampo('nomeDaMae', e.target.value)} />
      </Campo>
      <Campo label="Nome do pai" htmlFor="nomeDoPai" erro={erros.nomeDoPai} className="md:col-span-2">
        <Input id="nomeDoPai" value={estado.nomeDoPai} onChange={(e) => setCampo('nomeDoPai', e.target.value)} />
      </Campo>
      <Campo label="Responsável legal" htmlFor="responsavelLegal" erro={erros.responsavelLegal}
        className="md:col-span-2" dica="Quando paciente menor ou com tutela.">
        <Input id="responsavelLegal" value={estado.responsavelLegal} onChange={(e) => setCampo('responsavelLegal', e.target.value)} />
      </Campo>
    </div>
  );
}

function SecaoEndereco({
  estado, erros, setEndereco, buscandoCep, aoBuscarCep,
}: {
  estado: Estado;
  erros: Record<string, string>;
  setEndereco: <K extends keyof Endereco>(c: K, v: Endereco[K]) => void;
  buscandoCep: boolean;
  aoBuscarCep: () => void;
}) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-6">
      <Campo label="CEP" htmlFor="cep" erro={erros['endereco.cep']} className="md:col-span-2">
        <div className="flex gap-2">
          <Input
            id="cep"
            value={estado.endereco.cep}
            onChange={(e) => setEndereco('cep', e.target.value)}
            placeholder="00000000"
            inputMode="numeric"
          />
          <Button type="button" variante="outline" onClick={aoBuscarCep} disabled={buscandoCep}>
            {buscandoCep ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Buscar'}
          </Button>
        </div>
      </Campo>
      <Campo label="Logradouro" htmlFor="logradouro" erro={erros['endereco.logradouro']} className="md:col-span-4">
        <Input id="logradouro" value={estado.endereco.logradouro} onChange={(e) => setEndereco('logradouro', e.target.value)} />
      </Campo>
      <Campo label="Número" htmlFor="numero" erro={erros['endereco.numero']} className="md:col-span-1">
        <Input id="numero" value={estado.endereco.numero} onChange={(e) => setEndereco('numero', e.target.value)} />
      </Campo>
      <Campo label="Complemento" htmlFor="complemento" erro={erros['endereco.complemento']} className="md:col-span-3">
        <Input id="complemento" value={estado.endereco.complemento} onChange={(e) => setEndereco('complemento', e.target.value)} />
      </Campo>
      <Campo label="Bairro" htmlFor="bairro" erro={erros['endereco.bairro']} className="md:col-span-2">
        <Input id="bairro" value={estado.endereco.bairro} onChange={(e) => setEndereco('bairro', e.target.value)} />
      </Campo>
      <Campo label="Cidade" htmlFor="cidade" erro={erros['endereco.cidade']} className="md:col-span-3">
        <Input id="cidade" value={estado.endereco.cidade} onChange={(e) => setEndereco('cidade', e.target.value)} />
      </Campo>
      <Campo label="UF" htmlFor="uf" erro={erros['endereco.uf']} className="md:col-span-1">
        <Input id="uf" value={estado.endereco.uf} onChange={(e) => setEndereco('uf', e.target.value.toUpperCase())} maxLength={2} />
      </Campo>
      <Campo label="Ponto de referência" htmlFor="pontoReferencia" erro={erros['endereco.pontoReferencia']} className="md:col-span-6">
        <Input id="pontoReferencia" value={estado.endereco.pontoReferencia} onChange={(e) => setEndereco('pontoReferencia', e.target.value)} />
      </Campo>
      <p className="md:col-span-6 text-xs text-gray-500">
        Coordenadas (latitude/longitude) serão preenchidas pelo paciente no app do cidadão.
      </p>
    </div>
  );
}

function SecaoContatos({
  estado, erros, setCampo, setContato,
}: SecProps & { setContato: <K extends keyof ContatoEmergencia>(c: K, v: ContatoEmergencia[K]) => void }) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Campo label="Contato principal (WhatsApp)" htmlFor="telefonePrincipal" erro={erros.telefonePrincipal}>
          <Input id="telefonePrincipal" value={estado.telefonePrincipal} onChange={(e) => setCampo('telefonePrincipal', e.target.value)} placeholder="(21) 99999-9999" />
          <div className="mt-1.5"><BotaoValidarTelefone cpf={estado.cpf} numero={estado.telefonePrincipal} /></div>
        </Campo>
        <Campo label="Celular" htmlFor="telefoneCelular" erro={erros.telefoneCelular}>
          <Input id="telefoneCelular" value={estado.telefoneCelular} onChange={(e) => setCampo('telefoneCelular', e.target.value)} />
        </Campo>
        <Campo label="Telefone residencial" htmlFor="telefoneResidencial" erro={erros.telefoneResidencial}>
          <Input id="telefoneResidencial" value={estado.telefoneResidencial} onChange={(e) => setCampo('telefoneResidencial', e.target.value)} />
        </Campo>
        <Campo label="E-mail" htmlFor="email" erro={erros.email}>
          <Input id="email" type="email" value={estado.email} onChange={(e) => setCampo('email', e.target.value)} />
        </Campo>
      </div>
      <div>
        <h3 className="text-sm font-semibold text-gray-900">Contato de emergência</h3>
        <div className="mt-3 grid grid-cols-1 gap-4 md:grid-cols-3">
          <Campo label="Nome" htmlFor="emerg.nome" erro={erros['contatoEmergencia.nome']}>
            <Input id="emerg.nome" value={estado.contatoEmergencia.nome} onChange={(e) => setContato('nome', e.target.value)} />
          </Campo>
          <Campo label="Parentesco" htmlFor="emerg.parentesco" erro={erros['contatoEmergencia.parentesco']}>
            <Input id="emerg.parentesco" value={estado.contatoEmergencia.parentesco} onChange={(e) => setContato('parentesco', e.target.value)} placeholder="Mãe, Filho, Cônjuge…" />
          </Campo>
          <Campo label="Telefone" htmlFor="emerg.telefone" erro={erros['contatoEmergencia.telefone']}>
            <Input id="emerg.telefone" value={estado.contatoEmergencia.telefone} onChange={(e) => setContato('telefone', e.target.value)} />
          </Campo>
        </div>
      </div>
    </div>
  );
}

function SecaoSaude({ estado, erros, setCampo }: SecProps) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        <Campo label="Altura (cm)" htmlFor="alturaCm" erro={erros.alturaCm}>
          <Input id="alturaCm" type="number" inputMode="numeric" value={estado.alturaCm} onChange={(e) => setCampo('alturaCm', e.target.value)} />
        </Campo>
        <Campo label="Peso (kg)" htmlFor="pesoKg" erro={erros.pesoKg}>
          <Input id="pesoKg" type="number" step="0.1" inputMode="decimal" value={estado.pesoKg} onChange={(e) => setCampo('pesoKg', e.target.value)} />
        </Campo>
        <Campo label="Tipo sanguíneo" htmlFor="tipoSanguineo" erro={erros.tipoSanguineo}>
          <Select id="tipoSanguineo" value={estado.tipoSanguineo} onChange={(e) => setCampo('tipoSanguineo', e.target.value as TipoSanguineo)}>
            {TIPOS_SANGUINEOS.map((s) => <option key={s} value={s}>{TIPO_SANG_LABEL[s]}</option>)}
          </Select>
        </Campo>
        <Campo label="Fator Rh" htmlFor="fatorRh" erro={erros.fatorRh}>
          <Select id="fatorRh" value={estado.fatorRh} onChange={(e) => setCampo('fatorRh', e.target.value as FatorRh)}>
            {FATORES_RH.map((s) => <option key={s} value={s}>{FATOR_RH_LABEL[s]}</option>)}
          </Select>
        </Campo>
      </div>
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <Campo label="Alergias" htmlFor="alergias" dica="Pressione Enter para adicionar item.">
          <ListaChips id="alergias" itens={estado.alergias} onChange={(v) => setCampo('alergias', v)} placeholder="Ex: Dipirona" />
        </Campo>
        <Campo label="Medicamentos contínuos" htmlFor="medicamentos" dica="Pressione Enter para adicionar item.">
          <ListaChips id="medicamentos" itens={estado.medicamentosContinuos} onChange={(v) => setCampo('medicamentosContinuos', v)} placeholder="Ex: Losartana 50mg" />
        </Campo>
        <Campo label="Comorbidades / condições crônicas" htmlFor="comorbidades" dica="Pressione Enter para adicionar item.">
          <ListaChips id="comorbidades" itens={estado.comorbidades} onChange={(v) => setCampo('comorbidades', v)} placeholder="Ex: Hipertensão" />
        </Campo>
        <Campo label="Deficiências / necessidades especiais" htmlFor="deficiencias" dica="Pressione Enter para adicionar item.">
          <ListaChips id="deficiencias" itens={estado.deficiencias} onChange={(v) => setCampo('deficiencias', v)} placeholder="Ex: Cadeirante" />
        </Campo>
      </div>
      <Campo label="Plano de saúde" htmlFor="planoSaude" erro={erros.planoSaude}>
        <Input id="planoSaude" value={estado.planoSaude} onChange={(e) => setCampo('planoSaude', e.target.value)} />
      </Campo>
    </div>
  );
}

function SecaoObservacoes({ estado, erros, setCampo }: SecProps) {
  return (
    <Campo label="Observações gerais" htmlFor="observacoes" erro={erros.observacoes}>
      <textarea
        id="observacoes"
        className="input min-h-[150px]"
        value={estado.observacoes}
        onChange={(e) => setCampo('observacoes', e.target.value)}
        placeholder="Anotações relevantes para a equipe de transporte/saúde."
      />
    </Campo>
  );
}
