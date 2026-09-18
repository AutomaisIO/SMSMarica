import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { CalendarCheck2, Clock, RefreshCw, Save, Search, Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { Tabs } from '@/shared/ui/Tabs';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import type {
  NotificacaoResumo,
  StatusConfirmacao,
  StatusNotificacao,
} from '@/features/notificacoes-agendamento/types';
import {
  useAlterarRegraUnidade,
  useConfiguracaoConfirmacao,
  useDispararLote,
  usePreviaLote,
  useFila,
  useItemFila,
  useRegrasUnidades,
  useReenviarConfirmacao,
  useResumoFila,
  useRespostas,
  useSalvarConfiguracaoConfirmacao,
} from '@/features/confirmacoes/api';
import type { RegraUnidade, RespostaConfirmacao } from '@/features/confirmacoes/types';

const TAMANHOS = [50, 100, 200] as const;

function useDebounce<T>(valor: T, ms = 400): T {
  const [v, setV] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setV(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return v;
}

function dataHora(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

const ROTULO_STATUS: Record<StatusNotificacao, string> = {
  Pendente: 'Na fila',
  Enviada: 'Enviada',
  Entregue: 'Entregue',
  Lida: 'Lida',
  Falha: 'Falha',
  SemTelefoneValido: 'Sem celular válido',
  AguardandoTelefoneVerificado: 'Aguardando contato verificado',
  AguardandoVerificacaoCadastral: 'Aguardando o paciente se identificar',
  AguardandoCorrecaoContato: 'Número inválido',
};

const CLASSE_STATUS: Record<StatusNotificacao, string> = {
  Pendente: 'badge-gray',
  Enviada: 'badge-info',
  Entregue: 'badge-info',
  Lida: 'badge-success',
  Falha: 'badge-danger',
  SemTelefoneValido: 'badge-warning',
  AguardandoTelefoneVerificado: 'badge-warning',
  AguardandoVerificacaoCadastral: 'badge-warning',
  AguardandoCorrecaoContato: 'badge-danger',
};

const ROTULO_RESPOSTA: Record<StatusConfirmacao, string> = {
  Pendente: 'Sem resposta',
  Confirmada: 'Confirmou',
  Cancelada: 'Não vai',
};

const CLASSE_RESPOSTA: Record<StatusConfirmacao, string> = {
  Pendente: 'badge-gray',
  Confirmada: 'badge-success',
  Cancelada: 'badge-danger',
};

const ROTULO_CANAL: Record<string, string> = {
  'whatsapp-link': 'Link do WhatsApp',
  'whatsapp-quickreply': 'Botões do WhatsApp',
  'whatsapp-robo': 'Robô do WhatsApp',
  app: 'App do cidadão',
  presencial: 'Presencial (recepção)',
  sandbox: 'Sandbox',
};

function Badge({ classe, children }: { classe: string; children: ReactNode }) {
  return <span className={`badge ${classe}`}>{children}</span>;
}

function Numero({ rotulo, valor, destaque, dica }: { rotulo: string; valor: number; destaque?: string; dica?: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3" title={dica}>
      <div className={`text-2xl font-semibold tabular-nums ${destaque ?? 'text-gray-900'}`}>
        {valor.toLocaleString('pt-BR')}
      </div>
      <div className="text-xs text-gray-500">{rotulo}</div>
    </div>
  );
}

// ------------------------------------------------------------------ Fila

function DetalheFila({ id, aoFechar }: { id: string; aoFechar: () => void }) {
  const q = useItemFila(id);
  const reenviar = useReenviarConfirmacao();
  const podeEditar = usePermissao('Confirmacoes', 'Edicao');
  const d = q.data;
  const r = d?.resumo;

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Confirmação de agendamento" largura="lg">
      {q.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : !d || !r ? (
        <p className="text-sm text-red-700">Não foi possível carregar.</p>
      ) : (
        <div className="space-y-4 text-sm">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div><span className="text-xs uppercase text-gray-500">Paciente</span><div>{r.pacienteNome ?? '—'}</div></div>
            <div><span className="text-xs uppercase text-gray-500">Telefone</span><div><TelefoneCopiavel numero={r.telefone} /></div></div>
            <div><span className="text-xs uppercase text-gray-500">Procedimento</span><div>{r.tipoExameNome ?? '—'}</div></div>
            <div><span className="text-xs uppercase text-gray-500">Agendado para</span><div>{dataHora(r.dataAgendada)}</div></div>
            <div><span className="text-xs uppercase text-gray-500">Envio</span><div><Badge classe={CLASSE_STATUS[r.status] ?? 'badge-gray'}>{ROTULO_STATUS[r.status] ?? r.status}</Badge></div></div>
            <div><span className="text-xs uppercase text-gray-500">Resposta</span><div><Badge classe={CLASSE_RESPOSTA[r.statusConfirmacao]}>{ROTULO_RESPOSTA[r.statusConfirmacao]}</Badge></div></div>
            <div><span className="text-xs uppercase text-gray-500">Próxima tentativa</span><div>{dataHora(d.proximaTentativaEm)}</div></div>
            <div><span className="text-xs uppercase text-gray-500">Enviada / entregue / lida</span><div>{dataHora(r.enviadoEm)} · {dataHora(r.entregueEm)} · {dataHora(r.lidoEm)}</div></div>
          </div>
          {r.motivoFalha ? (
            <p className="rounded-md bg-red-50 px-3 py-2 text-red-700">{r.motivoFalha}</p>
          ) : null}
          {r.motivoCancelamentoPaciente ? (
            <p className="rounded-md bg-amber-50 px-3 py-2 text-amber-800">
              <strong>Motivo do paciente:</strong> {r.motivoCancelamentoPaciente}
            </p>
          ) : null}
          {d.mensagemConteudo ? (
            <p className="whitespace-pre-wrap rounded-md bg-gray-50 px-3 py-2 text-xs text-gray-600">{d.mensagemConteudo}</p>
          ) : null}
          {podeEditar ? (
            <div className="flex items-center justify-end gap-2">
              {reenviar.isError ? <span className="text-red-700">{extrairMensagemDeErro(reenviar.error)}</span> : null}
              {reenviar.isSuccess ? <span className="text-emerald-700">Recolocada na fila.</span> : null}
              <Button variante="outline" disabled={reenviar.isPending} onClick={() => reenviar.mutate(r.id)}>
                <RefreshCw className="mr-1.5 h-4 w-4" /> Recolocar na fila
              </Button>
            </div>
          ) : null}
        </div>
      )}
    </Modal>
  );
}

function AbaFila() {
  const resumo = useResumoFila();
  const [texto, setTexto] = useState('');
  const [status, setStatus] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(50);
  const [detalhe, setDetalhe] = useState<string | null>(null);
  const textoDeb = useDebounce(texto);

  useEffect(() => setPagina(1), [textoDeb, status, confirmacao, tamanho]);

  const q = useFila({
    texto: textoDeb.trim() || undefined,
    status: status || undefined,
    confirmacao: confirmacao || undefined,
    pagina,
    tamanho,
  });

  const colunas: Coluna<NotificacaoResumo>[] = useMemo(
    () => [
      {
        chave: 'paciente',
        cabecalho: 'Paciente',
        render: (n) => (
          <button type="button" onClick={() => setDetalhe(n.id)} className="font-medium text-red-700 hover:underline">
            {n.pacienteNome ?? '(sem nome)'}
          </button>
        ),
      },
      { chave: 'sisreg', cabecalho: 'Nº SISREG', render: (n) => n.codigoSolicitacao ?? '—' },
      { chave: 'proc', cabecalho: 'Procedimento', render: (n) => n.tipoExameNome ?? '—' },
      { chave: 'unidade', cabecalho: 'Unidade', render: (n) => n.unidadeNome ?? '—' },
      { chave: 'data', cabecalho: 'Agendado para', render: (n) => dataHora(n.dataAgendada) },
      { chave: 'fone', cabecalho: 'Telefone', render: (n) => <TelefoneCopiavel numero={n.telefone} /> },
      {
        chave: 'envio',
        cabecalho: 'Envio',
        render: (n) => (
          <span title={n.motivoFalha ?? undefined}>
            <Badge classe={CLASSE_STATUS[n.status] ?? 'badge-gray'}>{ROTULO_STATUS[n.status] ?? n.status}</Badge>
          </span>
        ),
      },
      {
        chave: 'resposta',
        cabecalho: 'Resposta',
        render: (n) => <Badge classe={CLASSE_RESPOSTA[n.statusConfirmacao]}>{ROTULO_RESPOSTA[n.statusConfirmacao]}</Badge>,
      },
      { chave: 'criada', cabecalho: 'Entrou na fila', render: (n) => dataHora(n.criadoEm) },
    ],
    [],
  );

  const r = resumo.data;

  return (
    <div className="space-y-4">
      {r ? (
        <div
          className={`flex flex-wrap items-center gap-2 rounded-lg border px-4 py-3 text-sm ${
            r.janelaAbertaAgora
              ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
              : 'border-amber-200 bg-amber-50 text-amber-900'
          }`}
        >
          <Clock className="h-4 w-4" />
          {r.janelaAbertaAgora ? (
            <span>
              <strong>Disparando agora.</strong> Confirmações saem das {r.horaInicioEnvio} às {r.horaFimEnvio}.
            </span>
          ) : (
            <span>
              <strong>Fora do horário de envio</strong> ({r.horaInicioEnvio}–{r.horaFimEnvio}). As mensagens
              ficam empilhadas e começam a sair em <strong>{dataHora(r.proximaAberturaEm)}</strong>.
            </span>
          )}
        </div>
      ) : null}

      {r ? (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-8">
          <Numero rotulo="Na fila" valor={r.naFila} dica="Esperando o horário ou a próxima rodada do envio." />
          <Numero rotulo="Enviadas hoje" valor={r.enviadasHoje} />
          <Numero rotulo="Confirmaram hoje" valor={r.confirmadasHoje} destaque="text-emerald-700" />
          <Numero rotulo="Não vão (hoje)" valor={r.canceladasHoje} destaque="text-red-700" />
          <Numero
            rotulo="Aguardando identificação"
            valor={r.aguardandoVerificacaoCadastral}
            destaque="text-amber-700"
            dica="Número não verificado: foi pedido o início do CPF antes de mandar os dados do agendamento."
          />
          <Numero
            rotulo="Número inválido"
            valor={r.numeroInvalido}
            destaque="text-red-700"
            dica="Quem atende disse que não conhece o paciente. Corrija o telefone no cadastro."
          />
          <Numero rotulo="Sem celular" valor={r.semTelefoneValido} destaque="text-amber-700" />
          <Numero rotulo="Falhas" valor={r.falha} destaque="text-red-700" />
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Nº SISREG ou telefone…" className="pl-9" />
        </div>
        <Select value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Situação do envio">
          <option value="">Envio: todos</option>
          <option value="Pendente">Na fila</option>
          <option value="AguardandoVerificacaoCadastral">Aguardando identificação</option>
          <option value="Enviada">Enviada</option>
          <option value="Entregue">Entregue</option>
          <option value="Lida">Lida</option>
          <option value="AguardandoCorrecaoContato">Número inválido</option>
          <option value="SemTelefoneValido">Sem celular válido</option>
          <option value="Falha">Falha</option>
        </Select>
        <Select value={confirmacao} onChange={(e) => setConfirmacao(e.target.value)} aria-label="Resposta">
          <option value="">Resposta: todas</option>
          <option value="Pendente">Sem resposta</option>
          <option value="Confirmada">Confirmou</option>
          <option value="Cancelada">Não vai</option>
        </Select>
      </div>

      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}
      <Tabela colunas={colunas} dados={q.data?.itens ?? []} chaveLinha={(n) => n.id} carregando={q.isLoading} vazio="Nada na fila." />
      {q.data ? (
        <Paginacao
          pagina={pagina}
          tamanho={tamanho}
          total={q.data.total}
          tamanhos={TAMANHOS}
          aoMudarPagina={setPagina}
          aoMudarTamanho={setTamanho}
        />
      ) : null}
      {detalhe ? <DetalheFila id={detalhe} aoFechar={() => setDetalhe(null)} /> : null}
    </div>
  );
}

// ------------------------------------------------------------------ Respostas

function AbaRespostas() {
  const [resposta, setResposta] = useState('Cancelada');
  const [texto, setTexto] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(50);
  const textoDeb = useDebounce(texto);

  useEffect(() => setPagina(1), [resposta, textoDeb, de, ate, tamanho]);

  const q = useRespostas({
    resposta: resposta || undefined,
    texto: textoDeb.trim() || undefined,
    de: de ? `${de}T00:00:00` : undefined,
    ate: ate ? `${ate}T00:00:00` : undefined,
    pagina,
    tamanho,
  });

  const colunas: Coluna<RespostaConfirmacao>[] = useMemo(
    () => [
      {
        chave: 'paciente',
        cabecalho: 'Paciente',
        render: (r) => <NomePacienteComResumo pacienteId={r.pacienteId} nome={r.pacienteNome ?? '(sem nome)'} />,
      },
      {
        chave: 'resposta',
        cabecalho: 'Resposta',
        render: (r) => <Badge classe={CLASSE_RESPOSTA[r.statusConfirmacao]}>{ROTULO_RESPOSTA[r.statusConfirmacao]}</Badge>,
      },
      {
        chave: 'motivo',
        cabecalho: 'Motivo informado',
        render: (r) =>
          r.statusConfirmacao === 'Cancelada' ? (
            r.motivo ? <span className="whitespace-pre-wrap">{r.motivo}</span> : <span className="text-gray-400">não informou</span>
          ) : (
            <span className="text-gray-300">—</span>
          ),
      },
      {
        chave: 'proc',
        cabecalho: 'Procedimento',
        render: (r) => {
          const rota = r.exameId ? `/app/solicitacoes-exame/${r.exameId}` : `/app/consultas/${r.solicitacaoId}`;
          return (
            <Link to={rota} className="text-red-700 hover:underline">
              {r.procedimento ?? (r.categoria === 'Consulta' ? 'Consulta' : 'Exame')}
            </Link>
          );
        },
      },
      { chave: 'sisreg', cabecalho: 'Nº SISREG', render: (r) => r.codigoSolicitacao ?? '—' },
      { chave: 'unidade', cabecalho: 'Unidade executante', render: (r) => r.unidadeExecutante ?? '—' },
      { chave: 'data', cabecalho: 'Agendado para', render: (r) => dataHora(r.dataAgendada) },
      { chave: 'quando', cabecalho: 'Respondeu em', render: (r) => dataHora(r.respondidoEm) },
      { chave: 'canal', cabecalho: 'Canal', render: (r) => ROTULO_CANAL[r.canal ?? ''] ?? r.canal ?? '—' },
    ],
    [],
  );

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-600">
        Resposta dada pelo paciente ao aviso de agendamento. <strong>Fica só no SMSMais</strong> — nada é
        alterado no SISREG. O que fazer com os cancelamentos ainda vai ser definido.
      </p>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-5">
        <Select value={resposta} onChange={(e) => setResposta(e.target.value)} aria-label="Resposta">
          <option value="Cancelada">Não vão (cancelaram)</option>
          <option value="Confirmada">Confirmaram</option>
          <option value="">Todas as respostas</option>
        </Select>
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Nome, CPF ou nº SISREG…" className="pl-9" />
        </div>
        <div className="flex items-center gap-2 md:col-span-2">
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} aria-label="Respondeu de" />
          <span className="text-sm text-gray-400">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} aria-label="Respondeu até" />
        </div>
      </div>
      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}
      <Tabela
        colunas={colunas}
        dados={q.data?.itens ?? []}
        chaveLinha={(r) => r.solicitacaoId}
        carregando={q.isLoading}
        vazio="Nenhuma resposta no filtro."
      />
      {q.data ? (
        <Paginacao
          pagina={pagina}
          tamanho={tamanho}
          total={q.data.total}
          tamanhos={TAMANHOS}
          aoMudarPagina={setPagina}
          aoMudarTamanho={setTamanho}
        />
      ) : null}
    </div>
  );
}

// ------------------------------------------------------------------ Lote

/** dd/mm (seg) — o dia como quem lê a agenda enxerga. */
function diaLegivel(iso: string): string {
  const d = new Date(`${iso}T12:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', weekday: 'short' });
}

function hojeMais(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() + dias);
  return d.toISOString().slice(0, 10);
}

/**
 * Avisar o estoque que a importação deixou para trás: quando a unidade liga a chave, o que já
 * estava importado não gera aviso nenhum. Aqui se escolhe o período da AGENDA (não do envio) —
 * é assim que se deixa o dia seguinte de fora e se começa na segunda.
 */
function AbaLote() {
  const podeEditar = usePermissao('Confirmacoes', 'Edicao');
  const regras = useRegrasUnidades();

  const [unidadeId, setUnidadeId] = useState('');
  const [de, setDe] = useState(hojeMais(1));
  const [ate, setAte] = useState(hojeMais(10));
  const [forcar, setForcar] = useState(false);
  const [ignorarJanela, setIgnorarJanela] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const cfg = useConfiguracaoConfirmacao();

  // Forçando, a lista precisa ter TODAS as unidades: a graça é justamente disparar para uma que
  // está com a chave desligada (sem religar o automático).
  const unidadesDaLista = useMemo(
    () => (regras.data ?? []).filter((u) => forcar || u.enviarConfirmacao),
    [regras.data, forcar],
  );

  const filtro = { unidadeId: unidadeId || undefined, de, ate, forcar: forcar || undefined };
  const disparoParams = { ...filtro, ignorarJanela: ignorarJanela || undefined };
  const previa = usePreviaLote(filtro, Boolean(de && ate) && (!forcar || Boolean(unidadeId)));
  const disparar = useDispararLote();
  const p = previa.data;
  const resultado = disparar.data;

  // 100 por rodada, uma rodada por minuto: é o que o worker escoa.
  const minutos = p ? Math.max(1, Math.ceil(p.elegiveis / 100)) : 0;

  return (
    <div className="space-y-5">
      <p className="text-sm text-gray-600">
        O aviso nasce na importação: ligar a chave da unidade <strong>não</strong> avisa quem já
        estava agendado. Este disparo cobre esse estoque, com a <strong>mesma régua</strong> — só
        SISREG, unidade ligada, procedimento marcado, agendamento futuro e paciente que ainda não
        respondeu. Quem já recebeu aviso não entra de novo.
      </p>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <label className="flex flex-col gap-1 text-sm md:col-span-2">
          <span className="font-medium text-gray-700">Unidade</span>
          <Select value={unidadeId} onChange={(e) => setUnidadeId(e.target.value)}>
            <option value="">Todas as unidades com aviso ligado</option>
            {unidadesDaLista.map((u) => (
              <option key={u.unidadeId} value={u.unidadeId}>
                {u.unidadeNome}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Agenda de</span>
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} />
        </label>
      </div>

      {podeEditar ? (
        <label className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={forcar}
            onChange={(e) => {
              setForcar(e.target.checked);
              if (e.target.checked) setUnidadeId('');
            }}
          />
          <span>
            <strong>Ignorar as chaves</strong> de unidade e de procedimento (exige escolher a unidade).
            Serve para avisar uma unidade que está com o automático <em>desligado</em>, sem religá-lo —
            o disparo passa a ser decisão de quem clica. As regras de privacidade continuam valendo:
            número marcado como inválido não recebe, e número não verificado recebe primeiro o desafio
            de identificação.
          </span>
        </label>
      ) : null}

      {podeEditar ? (
        <label className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={ignorarJanela}
            onChange={(e) => setIgnorarJanela(e.target.checked)}
          />
          <span>
            <strong>Enviar agora, ignorando o horário</strong>
            {cfg.data ? ` (${cfg.data.horaInicioEnvio}–${cfg.data.horaFimEnvio})` : ''}. Vale só para
            este lote — a janela continua valendo para todo o resto. Fora do horário comercial, lembre
            que o paciente recebe a mensagem na hora em que ela sair.
          </span>
        </label>
      ) : null}

      {forcar && !unidadeId ? (
        <p className="text-sm text-amber-800">Escolha a unidade para poder forçar o disparo.</p>
      ) : null}

      {previa.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(previa.error)}</p> : null}

      {p ? (
        <>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <Numero rotulo="Serão avisados" valor={p.elegiveis} destaque="text-emerald-700" />
            <Numero
              rotulo={forcar ? 'Procedimento desligado (incluídos)' : 'Procedimento desligado'}
              valor={p.foraProcedimentoDesligado}
              dica={
                forcar
                  ? 'No modo forçado a chave do procedimento é ignorada — estes entram no lote.'
                  : 'Agendamentos do período cujo procedimento não está marcado para avisar.'
              }
            />
            <Numero rotulo="Já avisados antes" valor={p.foraJaAvisado} />
            <Numero rotulo="Fora do SISREG" valor={p.foraNaoSisreg} />
          </div>

          <div className="overflow-hidden rounded-lg border border-gray-200">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-xs uppercase text-gray-500">
                <tr>
                  <th className="px-3 py-2 text-left">Dia da agenda</th>
                  <th className="px-3 py-2 text-right">Total</th>
                  <th className="px-3 py-2 text-right">Consultas</th>
                  <th className="px-3 py-2 text-right">Exames</th>
                </tr>
              </thead>
              <tbody>
                {p.porDia.map((d) => (
                  <tr key={d.dia} className="border-t border-gray-100">
                    <td className="px-3 py-1.5">{diaLegivel(d.dia)}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums font-medium">{d.total}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums text-gray-600">{d.consultas}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums text-gray-600">{d.exames}</td>
                  </tr>
                ))}
                {p.porDia.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="px-3 py-3 text-center text-gray-500">
                      Nada a avisar neste período.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          {p.aviso ? (
            <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">{p.aviso}</p>
          ) : null}

          {p.elegiveis > 0 ? (
            <p className="text-sm text-gray-600">
              As mensagens entram na fila e saem no ritmo do worker: cerca de <strong>{minutos} min</strong>{' '}
              de envio.{' '}
              {ignorarJanela
                ? 'Com "enviar agora" marcado, começam a sair na próxima rodada (até 1 min).'
                : 'Dentro da janela de horário; fora dela, ficam empilhadas até a janela abrir.'}
            </p>
          ) : null}
        </>
      ) : null}

      {resultado ? (
        <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          {resultado.enfileiradas} mensagem(ns) na fila. Acompanhe na aba <strong>Fila de envio</strong>.
        </p>
      ) : null}
      {disparar.isError ? (
        <p className="text-sm text-red-700">{extrairMensagemDeErro(disparar.error)}</p>
      ) : null}

      {podeEditar && p && p.elegiveis > 0 ? (
        confirmando ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4">
            <p className="text-sm text-red-900">
              Confirma o envio para <strong>{p.elegiveis}</strong> agendamento(s) de{' '}
              {diaLegivel(p.porDia[0]?.dia ?? de)} a {diaLegivel(p.porDia[p.porDia.length - 1]?.dia ?? ate)}? São
              mensagens reais para pacientes — e a resposta deles volta para a Central de Atendimento.
              {ignorarJanela ? (
                <>
                  {' '}
                  <strong>Saem agora</strong>, fora do horário de envio.
                </>
              ) : null}
            </p>
            <div className="mt-3 flex items-center gap-2">
              <Button
                disabled={disparar.isPending}
                onClick={() => {
                  disparar.mutate(disparoParams);
                  setConfirmando(false);
                }}
              >
                Sim, disparar {p.elegiveis}
              </Button>
              <Button variante="outline" onClick={() => setConfirmando(false)}>
                Cancelar
              </Button>
            </div>
          </div>
        ) : (
          <Button onClick={() => setConfirmando(true)} disabled={disparar.isPending}>
            <Send className="mr-1.5 h-4 w-4" /> Disparar lote
          </Button>
        )
      ) : null}
    </div>
  );
}

// ------------------------------------------------------------------ Regras

function AbaRegras() {
  const podeEditar = usePermissao('Confirmacoes', 'Edicao');
  const cfg = useConfiguracaoConfirmacao();
  const salvar = useSalvarConfiguracaoConfirmacao();
  const regras = useRegrasUnidades();
  const alterarUnidade = useAlterarRegraUnidade();

  const [inicio, setInicio] = useState('08:00');
  const [fim, setFim] = useState('18:00');
  const [vazao, setVazao] = useState(100);
  const [somenteSisreg, setSomenteSisreg] = useState(true);

  useEffect(() => {
    if (!cfg.data) return;
    setInicio(cfg.data.horaInicioEnvio);
    setFim(cfg.data.horaFimEnvio);
    setVazao(cfg.data.maximoPorPassagem);
    setSomenteSisreg(cfg.data.somenteSisreg);
  }, [cfg.data]);

  const colunas: Coluna<RegraUnidade>[] = useMemo(
    () => [
      { chave: 'nome', cabecalho: 'Unidade executante', render: (u) => u.unidadeNome },
      {
        chave: 'aviso',
        cabecalho: 'Avisa o paciente',
        render: (u) => (
          <label className="inline-flex items-center gap-2">
            <input
              type="checkbox"
              checked={u.enviarConfirmacao}
              disabled={!podeEditar || alterarUnidade.isPending}
              onChange={(e) => alterarUnidade.mutate({ unidadeId: u.unidadeId, enviar: e.target.checked })}
            />
            <span className={u.enviarConfirmacao ? 'text-emerald-700' : 'text-gray-500'}>
              {u.enviarConfirmacao ? 'Ligado' : 'Desligado'}
            </span>
          </label>
        ),
      },
      {
        chave: 'procs',
        cabecalho: 'Procedimentos com aviso',
        render: (u) => `${u.procedimentosComAviso} de ${u.procedimentosTotal}`,
      },
      {
        chave: 'link',
        cabecalho: '',
        render: (u) => (
          <Link to={`/app/unidades/${u.unidadeId}`} className="text-xs text-red-700 hover:underline">
            Escolher procedimentos
          </Link>
        ),
      },
    ],
    [podeEditar, alterarUnidade],
  );

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-4">
        <h2 className="text-base font-semibold text-gray-900">Parâmetros de disparo</h2>
        <p className="mt-1 text-sm text-gray-600">
          Fora do horário, as confirmações ficam <strong>empilhadas</strong> e saem quando o horário abrir — a
          sincronização do SISREG pode rodar de madrugada sem ninguém receber mensagem de noite. A única
          exceção é a resposta a quem acabou de se identificar pelo WhatsApp (a pessoa está na conversa).
        </p>
        <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-4">
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-gray-700">Começa a enviar às</span>
            <Input type="time" value={inicio} onChange={(e) => setInicio(e.target.value)} disabled={!podeEditar} />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-gray-700">Para de enviar às</span>
            <Input type="time" value={fim} onChange={(e) => setFim(e.target.value)} disabled={!podeEditar} />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-gray-700">Mensagens por rodada (a cada minuto)</span>
            <Input
              type="number"
              min={1}
              max={1000}
              value={vazao}
              onChange={(e) => setVazao(Number(e.target.value))}
              disabled={!podeEditar}
            />
          </label>
          <label className="flex items-center gap-2 self-end pb-2 text-sm">
            <input
              type="checkbox"
              checked={somenteSisreg}
              onChange={(e) => setSomenteSisreg(e.target.checked)}
              disabled={!podeEditar}
            />
            <span>Só agendamentos do <strong>SISREG</strong></span>
          </label>
        </div>
        {podeEditar ? (
          <div className="mt-4 flex items-center justify-end gap-3">
            {salvar.isError ? <span className="text-sm text-red-700">{extrairMensagemDeErro(salvar.error)}</span> : null}
            {salvar.isSuccess ? <span className="text-sm text-emerald-700">Salvo.</span> : null}
            <Button
              disabled={salvar.isPending}
              onClick={() =>
                salvar.mutate({ horaInicioEnvio: inicio, horaFimEnvio: fim, maximoPorPassagem: vazao, somenteSisreg })
              }
            >
              <Save className="mr-1.5 h-4 w-4" /> Salvar parâmetros
            </Button>
          </div>
        ) : null}
      </section>

      <section className="space-y-3">
        <div>
          <h2 className="text-base font-semibold text-gray-900">Quem recebe o aviso</h2>
          <p className="mt-1 text-sm text-gray-600">
            A mensagem só sai quando a <strong>unidade executante</strong> está ligada <strong>e</strong> o
            procedimento também está marcado para avisar (os procedimentos se escolhem na aba SISREG da unidade).
          </p>
        </div>
        {alterarUnidade.isError ? (
          <p className="text-sm text-red-700">{extrairMensagemDeErro(alterarUnidade.error)}</p>
        ) : null}
        <Tabela
          colunas={colunas}
          dados={regras.data ?? []}
          chaveLinha={(u) => u.unidadeId}
          carregando={regras.isLoading}
          vazio="Nenhuma unidade com mapeamento SISREG."
        />
      </section>

      <section className="rounded-lg border border-gray-200 bg-gray-50 p-4 text-sm text-gray-700">
        <h2 className="text-base font-semibold text-gray-900">Como a conversa acontece</h2>
        <ol className="mt-2 list-decimal space-y-1 pl-5">
          <li>
            Número <strong>ainda não verificado</strong>: primeiro pedimos os 4 primeiros dígitos do CPF, o mês/ano de
            nascimento e o nome. Quem responde <em>“Não sou essa pessoa”</em> e confirma que <em>não conhece</em> o
            paciente faz o número ficar marcado como <strong>inválido</strong> — nada mais é enviado para ele.
          </li>
          <li>
            Mensagem de confirmação com a data e a orientação de <strong>retirar a guia (ficha de solicitação) no
            posto</strong> e levar o <strong>pedido médico</strong>.
          </li>
          <li>
            O paciente confirma pelo link, ou toca em <em>“Não poderei ir!”</em> e escolhe <em>“Quero cancelar”</em> ou{' '}
            <em>“Não quero cancelar”</em>. O cancelamento vale na hora; o motivo é opcional.
          </li>
        </ol>
      </section>
    </div>
  );
}

// ------------------------------------------------------------------ Página

export function ConfirmacoesPage() {
  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <CalendarCheck2 className="mt-1 h-6 w-6 text-red-600" />
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Confirmações</h1>
          <p className="mt-1 text-sm text-gray-600">
            Aviso de agendamento do SISREG pelo WhatsApp: a fila de disparo, o que os pacientes responderam e as
            regras de envio.
          </p>
        </div>
      </header>
      <Tabs
        abas={[
          { id: 'fila', rotulo: 'Fila de envio', conteudo: <AbaFila /> },
          { id: 'respostas', rotulo: 'Respostas dos pacientes', conteudo: <AbaRespostas /> },
          { id: 'lote', rotulo: 'Disparar lote', conteudo: <AbaLote /> },
          { id: 'regras', rotulo: 'Regras e parâmetros', conteudo: <AbaRegras /> },
        ]}
      />
    </div>
  );
}
