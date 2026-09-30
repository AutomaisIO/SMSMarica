import { useState } from 'react';
import { AxiosError } from 'axios';
import {
  AlertTriangle,
  BookCopy,
  Clock,
  KeyRound,
  Loader2,
  Play,
  RefreshCw,
  Save,
} from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante, formatarWallClock } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useDispararVarreduraEsusSg,
  useExecucoesEsusSg,
  useSalvarCredencialEsusSg,
  useSalvarVarreduraAutomaticaEsusSg,
  useSincronizarCatalogoEsusSg,
  useStatusMotorEsusSg,
  useTestarCredencialEsusSg,
  useVarreduraAutomaticaEsusSg,
} from '@/features/esussg/api/queries';
import type {
  ExecucaoEsusSg,
  ModoVarreduraEsusSg,
  StatusVarreduraEsusSg,
} from '@/features/esussg/types';

const CLIENTE_PADRAO = 'SGO';

const ROTULO_STATUS: Record<StatusVarreduraEsusSg, string> = {
  Pendente: 'Pendente',
  EmExecucao: 'Em execução',
  Concluida: 'Concluída',
  Parcial: 'Parcial',
  Erro: 'Erro',
  Cancelada: 'Cancelada',
  Interrompida: 'Interrompida',
};

/**
 * "Parcial" é âmbar tracejado e com ícone — tem de saltar aos olhos ao lado de "Concluída": é a
 * rodada que DECLAROU cobertura incompleta (uma fatia de agendados não fechou a conta lido =
 * declarado, ou uma fila não pôde ser lida).
 */
const CLASSE_STATUS: Record<StatusVarreduraEsusSg, string> = {
  Pendente: 'bg-slate-50 text-slate-700 border-slate-200',
  EmExecucao: 'bg-blue-50 text-blue-700 border-blue-200',
  Concluida: 'bg-green-50 text-green-700 border-green-200',
  Parcial: 'bg-amber-100 text-amber-900 border-amber-400 border-dashed',
  Erro: 'bg-red-50 text-red-700 border-red-200',
  Cancelada: 'bg-orange-50 text-orange-700 border-orange-200',
  // Roxo, e não vermelho: parada retomável não é erro — ela volta sozinha pelo cursor.
  Interrompida: 'bg-purple-50 text-purple-700 border-purple-200',
};

const DICA_STATUS: Partial<Record<StatusVarreduraEsusSg, string>> = {
  Parcial:
    'Cobertura incompleta, declarada: alguma fatia de agendados não fechou a conta (lido ≠ declarado pelo ESUS) ou uma fila não pôde ser lida. Rode de novo.',
  Interrompida: 'Parada por queda do serviço — retoma sozinha pelo cursor.',
};

const ROTULO_MODO: Record<ModoVarreduraEsusSg, string> = {
  CargaInicial: 'Carga inicial — todo o histórico desde 2015 (em fatias de um ano; alguns minutos)',
  Diaria: 'Diária — fila + agendados de 45 dias atrás a 400 dias à frente',
  SomenteFila: 'Só a fila',
};

const ROTULO_MODO_CURTO: Record<ModoVarreduraEsusSg, string> = {
  CargaInicial: 'Carga inicial',
  Diaria: 'Diária',
  SomenteFila: 'Só a fila',
};

/** "2026-09-01" → "09/2026". */
function mesAno(iso: string | null): string {
  if (!iso) return '—';
  const [ano, mes] = iso.slice(0, 7).split('-');
  return ano && mes ? `${mes}/${ano}` : '—';
}

/** O ponteiro de retomada em uma linha: sem isso, rodada longa que reinicia parece travada. */
function descreverFase(x: ExecucaoEsusSg): string {
  switch (x.fase) {
    case 'Fila':
      return 'Fila · lendo a fila de regulação';
    case 'Agendados':
      return x.cursorMes ? `Agendados · lidos até ${mesAno(x.cursorMes)}` : 'Agendados · começando';
    case 'Finalizada':
      return 'Finalizada';
    default:
      return '—';
  }
}

/** "há Xs" desde o último progresso gravado. Envelhecer é o sinal de que parou. */
function desdeUltimoSinal(iso: string | null): { texto: string; velho: boolean } | null {
  if (!iso) return null;
  const seg = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
  const texto =
    seg < 60 ? `há ${seg}s` : seg < 3600 ? `há ${Math.floor(seg / 60)}min` : `há ${Math.floor(seg / 3600)}h`;
  return { texto, velho: seg > 120 };
}

function duracao(seg: number | null): string {
  if (seg == null) return '—';
  if (seg < 60) return `${seg}s`;
  const m = Math.floor(seg / 60);
  return `${m}m ${seg % 60}s`;
}

/**
 * Configuração do motor do ESUS de São Gonçalo (ADR-0063): estado, rodada manual, catálogo,
 * disparo diário, credencial e as últimas rodadas. Espelha a aba do SERNIT.
 */
export function AbaEsusSgConfiguracao() {
  const podeEditar = usePermissao('RegulacaoConfiguracao', 'Edicao');
  const { data: status, isLoading } = useStatusMotorEsusSg();
  const { data: execucoes } = useExecucoesEsusSg(20, status?.varreduraEmAndamento ?? false);
  const disparar = useDispararVarreduraEsusSg();
  const { data: automatica } = useVarreduraAutomaticaEsusSg();
  const salvarAutomatica = useSalvarVarreduraAutomaticaEsusSg();
  const sincronizarCatalogo = useSincronizarCatalogoEsusSg();
  const testar = useTestarCredencialEsusSg();
  const salvar = useSalvarCredencialEsusSg();

  const [modo, setModo] = useState<ModoVarreduraEsusSg>('Diaria');
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [cliente, setCliente] = useState(CLIENTE_PADRAO);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [horaEdit, setHoraEdit] = useState<string | null>(null);

  const hora = horaEdit ?? automatica?.horaLocal ?? '03:00';

  function limparMensagens() {
    setErro(null);
    setAviso(null);
  }

  async function aoSalvarAutomatica(ativo: boolean, horaNova: string) {
    limparMensagens();
    try {
      await salvarAutomatica.mutateAsync({ ativo, horaLocal: horaNova });
      setHoraEdit(null);
      setAviso(
        ativo
          ? `Rodada diária automática ligada para ${horaNova} (Brasília).`
          : 'Rodada diária automática desligada. Só rodada manual.',
      );
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoDisparar() {
    limparMensagens();
    try {
      await disparar.mutateAsync({ modo });
      setAviso('Rodada enfileirada. Ela roda em segundo plano — acompanhe em “Últimas rodadas”.');
    } catch (e) {
      if (e instanceof AxiosError && e.response?.status === 409) {
        setErro('Já existe uma varredura do ESUS em andamento. Aguarde ela terminar.');
      } else {
        setErro(extrairMensagemDeErro(e));
      }
    }
  }

  async function aoSincronizarCatalogo() {
    limparMensagens();
    try {
      const r = await sincronizarCatalogo.mutateAsync();
      const mudou = r.mudou ?? r.novos + r.alterados + r.inativados > 0;
      setAviso(
        `Catálogo do ESUS relido: ${r.lidos} procedimento(s) lido(s) — ${r.novos} novo(s), ` +
          `${r.alterados} alterado(s), ${r.inativados} inativado(s).` +
          (mudou ? ' As mudanças foram levadas ao catálogo canônico.' : ' Nada mudou.'),
      );
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const credencial = () => ({ usuario: usuario.trim(), senha, cliente: cliente.trim() || null });

  async function aoTestar() {
    limparMensagens();
    try {
      const nome = await testar.mutateAsync(credencial());
      setAviso(`O ESUS aceitou a credencial${nome ? ` — entrou como ${nome}` : ''}. Nada foi salvo.`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function aoSalvar() {
    limparMensagens();
    try {
      await salvar.mutateAsync(credencial());
      // A senha não fica na tela depois de salva: ela é write-only.
      setSenha('');
      setAviso('Credencial validada no ESUS e salva (cifrada). O motor já pode rodar.');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const colunas: Coluna<ExecucaoEsusSg>[] = [
    {
      chave: 'iniciadoEm',
      cabecalho: 'Início',
      className: 'w-36',
      render: (x) => (
        <div className="text-xs">
          <div>{formatarInstante(x.iniciadoEm)}</div>
          <div className="text-slate-400">
            {x.disparo === 'Agendado' ? 'automática' : (x.criadoPorNome ?? 'manual')}
          </div>
        </div>
      ),
    },
    {
      chave: 'modo',
      cabecalho: 'Modo',
      className: 'w-28',
      render: (x) => (
        <div className="text-xs">
          <div>{ROTULO_MODO_CURTO[x.modo]}</div>
          <div className="text-slate-400">
            {formatarWallClock(x.janelaInicio)} a {formatarWallClock(x.janelaFim)}
          </div>
        </div>
      ),
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      className: 'w-28',
      render: (x) => (
        <div className="space-y-1">
          <span
            title={DICA_STATUS[x.status]}
            className={`inline-flex items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-xs ${CLASSE_STATUS[x.status]}`}
          >
            {x.status === 'Parcial' && <AlertTriangle className="size-3.5" />}
            {ROTULO_STATUS[x.status]}
          </span>
          {x.mensagemErro && (
            <div className="max-w-48 truncate text-[11px] text-red-700" title={x.mensagemErro}>
              {x.mensagemErro}
            </div>
          )}
        </div>
      ),
    },
    {
      chave: 'lidos',
      cabecalho: 'Lidos',
      render: (x) => (
        <span className="text-xs">
          {x.naFila} na fila · {x.agendadosLidos} agendados
        </span>
      ),
    },
    {
      chave: 'mudancas',
      cabecalho: 'Mudanças',
      render: (x) => (
        <span className="text-xs">
          {x.solicitacoesNovas} novos · {x.solicitacoesAtualizadas} atualizados · {x.mudancasSituacao}{' '}
          mudaram de situação · <strong>{x.saidasDaFila} saíram da fila</strong> · {x.eventosNovos} eventos
        </span>
      ),
    },
    {
      chave: 'cobertura',
      cabecalho: 'Cobertura',
      className: 'w-36',
      render: (x) =>
        x.mesesIncompletos > 0 ? (
          // > 0 = meses de agendados que NÃO fecharam a conta. Precisa gritar.
          <span className="inline-flex items-center gap-1 text-xs font-medium text-amber-700">
            <AlertTriangle className="size-3.5" />
            {x.mesesIncompletos} fatia(s) incompleta(s)
          </span>
        ) : (
          <span className="text-xs text-slate-500">completa</span>
        ),
    },
    {
      chave: 'progresso',
      cabecalho: 'Progresso',
      className: 'w-52',
      render: (x) => (
        <div className="space-y-0.5 text-xs">
          <div className="text-slate-700">{descreverFase(x)}</div>
          {x.status === 'EmExecucao' &&
            (() => {
              const sinal = desdeUltimoSinal(x.ultimoSinalEm);
              if (!sinal) return null;
              return (
                <div className={sinal.velho ? 'font-medium text-red-700' : 'text-emerald-700'}>
                  {sinal.velho ? 'sem sinal ' : 'sinal '}
                  {sinal.texto}
                </div>
              );
            })()}
          {x.retomadas > 0 && (
            <div className="text-purple-700">
              retomada {x.retomadas}× · última em {formatarInstante(x.retomadaEm)}
            </div>
          )}
        </div>
      ),
    },
    {
      chave: 'duracao',
      cabecalho: 'Duração',
      className: 'w-20',
      render: (x) => <span className="text-xs">{duracao(x.duracaoSegundos)}</span>,
    },
  ];

  return (
    <div className="space-y-4">
      {isLoading && <p className="text-sm text-slate-500">Carregando estado do motor…</p>}

      {status && (
        <>
          <div className="grid gap-3 sm:grid-cols-5">
            <Cartao valor={status.totalSolicitacoes} rotulo="Pedidos espelhados" />
            <Cartao valor={status.totalEventos} rotulo="Marcos na trilha" />
            <Cartao valor={status.gatilhosPendentes} rotulo="Gatilhos pendentes" />
            <Cartao valor={status.recursosNoCatalogo} rotulo="Procedimentos no catálogo" />
            <div className="rounded-lg border border-slate-200 bg-white p-3">
              <div
                className={`text-lg font-semibold ${
                  status.credencialConfigurada ? 'text-green-700' : 'text-red-700'
                }`}
              >
                {status.credencialConfigurada ? 'OK' : 'Falta'}
              </div>
              <div className="text-xs text-slate-500">Credencial do ESUS</div>
              {status.credencialConfigurada && (
                <div className="mt-0.5 truncate text-[11px] text-slate-400" title={status.usuario ?? ''}>
                  {status.usuario ?? '—'} · cliente {status.cliente}
                </div>
              )}
            </div>
          </div>

          {!status.credencialConfigurada && (
            <p className="rounded bg-amber-50 p-3 text-sm text-amber-800">
              A credencial do ESUS ainda não está cadastrada. Preencha usuário, senha e cliente
              abaixo, teste e salve — sem ela o motor não roda.
            </p>
          )}
        </>
      )}

      {erro && <p className="rounded bg-red-50 p-3 text-sm text-red-700">{erro}</p>}
      {aviso && <p className="rounded bg-green-50 p-3 text-sm text-green-800">{aviso}</p>}

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h3 className="mb-3 text-sm font-semibold">Motor de atualização</h3>

        <div className="flex flex-wrap items-end gap-3">
          <Campo label="Modo da rodada" htmlFor="esussg-modo-da-rodada" className="min-w-80 flex-1">
            <Select
              id="esussg-modo-da-rodada"
              value={modo}
              onChange={(e) => setModo(e.target.value as ModoVarreduraEsusSg)}
            >
              {(Object.keys(ROTULO_MODO) as ModoVarreduraEsusSg[]).map((m) => (
                <option key={m} value={m}>
                  {ROTULO_MODO[m]}
                </option>
              ))}
            </Select>
          </Campo>

          <Button
            onClick={aoDisparar}
            disabled={
              !podeEditar ||
              disparar.isPending ||
              status?.varreduraEmAndamento ||
              !status?.credencialConfigurada
            }
          >
            {disparar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Play className="size-4" />}
            Rodar agora
          </Button>
        </div>

        {status?.varreduraEmAndamento && (
          <p className="mt-3 flex items-center gap-2 rounded bg-blue-50 p-3 text-sm text-blue-800">
            <RefreshCw className="size-4 animate-spin" />
            Varredura em andamento.
          </p>
        )}

        <p className="mt-3 text-xs text-slate-500">
          A <strong>carga inicial</strong> lê a fila inteira e os agendados de todo o histórico desde
          2015, em fatias de um ano, e aplica pedido a pedido — leva alguns minutos e retoma sozinha se
          o serviço reiniciar. A{' '}
          <strong>diária</strong> lê a fila e os agendados de uma janela móvel (45 dias para trás, 400
          para a frente) e detecta quem saiu da fila. <strong>Só a fila</strong> atualiza posição,
          prioridade e pendência sem ler agendados.
        </p>

        {/* Catálogo: uma requisição só; o que mudou vai ao catálogo canônico da regulação. */}
        <div className="mt-4 flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
          <BookCopy className="size-4 text-slate-500" />
          <div className="min-w-64 flex-1 text-sm text-slate-700">
            <strong>Catálogo do ESUS</strong>
            {status && <> — {status.recursosNoCatalogo} procedimento(s) copiado(s)</>}
            <p className="text-xs text-slate-500">
              Relê a lista de procedimentos do ESUS (uma requisição) e, se mudou, leva ao catálogo
              canônico da regulação — é por ele que a análise de regras sabe quais regras valem.
            </p>
          </div>
          <Button
            variante="secundaria"
            onClick={aoSincronizarCatalogo}
            disabled={!podeEditar || sincronizarCatalogo.isPending || !status?.credencialConfigurada}
          >
            {sincronizarCatalogo.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <BookCopy className="size-4" />
            )}
            Sincronizar catálogo
          </Button>
        </div>

        {/* Disparo diário: mora em BANCO — mudar a hora não pode exigir deploy. */}
        <div className="mt-4 flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              className="size-4"
              checked={automatica?.ativo ?? false}
              onChange={(e) => void aoSalvarAutomatica(e.target.checked, hora)}
              disabled={!podeEditar || salvarAutomatica.isPending}
            />
            <Clock className="size-4 text-slate-500" />
            Rodar a varredura diária automaticamente
          </label>

          <Campo label="Horário (Brasília)" htmlFor="esussg-hora" className="w-40">
            <Input
              id="esussg-hora"
              type="time"
              value={hora}
              onChange={(e) => setHoraEdit(e.target.value)}
              disabled={!podeEditar}
            />
          </Campo>

          <Button
            variante="secundaria"
            onClick={() => void aoSalvarAutomatica(automatica?.ativo ?? false, hora)}
            disabled={!podeEditar || salvarAutomatica.isPending || horaEdit === null}
          >
            {salvarAutomatica.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            Salvar horário
          </Button>

          <p className="w-full text-xs text-slate-500">
            A rodada automática é sempre no modo <strong>Diária</strong>. A carga inicial continua
            sendo disparo manual.
          </p>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4">
        <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold">
          <KeyRound className="size-4 text-red-600" /> Credencial do ESUS
        </h3>
        <div className="flex flex-wrap items-end gap-3">
          <Campo label="Usuário" htmlFor="esussg-usuario" className="w-56">
            <Input
              id="esussg-usuario"
              value={usuario}
              onChange={(e) => setUsuario(e.target.value)}
              autoComplete="off"
              placeholder={status?.usuario ?? ''}
            />
          </Campo>
          <Campo label="Senha" htmlFor="esussg-senha" className="w-56">
            <Input
              id="esussg-senha"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              autoComplete="new-password"
            />
          </Campo>
          <Campo label="Cliente" htmlFor="esussg-cliente" className="w-32">
            <Input
              id="esussg-cliente"
              value={cliente}
              onChange={(e) => setCliente(e.target.value.toUpperCase())}
              autoComplete="off"
            />
          </Campo>
          <Button
            variante="secundaria"
            onClick={aoTestar}
            disabled={!podeEditar || testar.isPending || salvar.isPending || !usuario || !senha}
          >
            {testar.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
            Testar
          </Button>
          <Button
            onClick={aoSalvar}
            disabled={!podeEditar || salvar.isPending || testar.isPending || !usuario || !senha}
          >
            {salvar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            Salvar
          </Button>
        </div>
        <p className="mt-2 text-xs text-slate-500">
          <strong>Testar</strong> só faz o login e mostra o nome com que o ESUS reconheceu a conta,
          sem gravar nada. <strong>Salvar</strong> valida primeiro e só então guarda a credencial
          cifrada — credencial que não funciona faria o motor falhar de madrugada, sem ninguém por
          perto. A senha é write-only: depois de salva, não volta para a tela. O cliente é o código
          da instalação do ESUS (padrão {CLIENTE_PADRAO}).
        </p>
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Últimas rodadas</h3>
        <Tabela
          colunas={colunas}
          dados={execucoes ?? []}
          chaveLinha={(x) => x.id}
          scrollXFlutuante
          vazio={<div className="py-6 text-center text-sm text-slate-500">Nenhuma rodada ainda.</div>}
        />
      </section>
    </div>
  );
}

function Cartao({ valor, rotulo }: { valor: number; rotulo: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-3">
      <div className="text-lg font-semibold">{valor.toLocaleString('pt-BR')}</div>
      <div className="text-xs text-slate-500">{rotulo}</div>
    </div>
  );
}
