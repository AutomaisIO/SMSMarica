import { useMemo, useState } from 'react';
import { BellRing, BellOff, CheckCircle2, Loader2, Plus, Send, Trash2, TriangleAlert } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { notificar } from '@/shared/ui/Notificacoes';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useAdicionarDestinatario,
  useAtualizarDestinatario,
  useEnviosAlerta,
  usePainelAlertas,
  useRemoverDestinatario,
  useSilenciarOrigem,
  useTestarAlerta,
} from '@/features/alertas-plataforma/api/queries';
import type {
  AlertaDestinatario,
  AlertaEnvio,
  AlertaOrigem,
  AlertaTemplate,
  SituacaoAlertaEnvio,
} from '@/features/alertas-plataforma/types';

/** "21979997000" → "(21) 97999-7000". Só para exibir. */
function formatarTelefone(telefone: string): string {
  const d = telefone.replace(/\D/g, '');
  const local = d.length > 11 ? d.slice(-11) : d;
  if (local.length === 11) return `(${local.slice(0, 2)}) ${local.slice(2, 7)}-${local.slice(7)}`;
  if (local.length === 10) return `(${local.slice(0, 2)}) ${local.slice(2, 6)}-${local.slice(6)}`;
  return telefone;
}

function quando(iso: string | null): string {
  return iso ? formatarInstante(iso) : '—';
}

const SITUACAO: Record<SituacaoAlertaEnvio, { rotulo: string; classe: string }> = {
  Enviado: { rotulo: 'Enviado', classe: 'bg-emerald-100 text-emerald-800' },
  Parcial: { rotulo: 'Parcial', classe: 'bg-amber-100 text-amber-800' },
  Falhou: { rotulo: 'Falhou', classe: 'bg-red-100 text-red-700' },
  SemDestinatario: { rotulo: 'Sem destinatário', classe: 'bg-gray-200 text-gray-700' },
  TetoDiario: { rotulo: 'Teto diário', classe: 'bg-gray-200 text-gray-700' },
};

function SeloSituacao({ situacao }: { situacao: SituacaoAlertaEnvio }) {
  const s = SITUACAO[situacao] ?? { rotulo: situacao, classe: 'bg-gray-100 text-gray-700' };
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.classe}`}>{s.rotulo}</span>;
}

function Cartao({ titulo, children, acoes }: { titulo: string; children: React.ReactNode; acoes?: React.ReactNode }) {
  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold text-gray-900">{titulo}</h2>
        {acoes}
      </header>
      {children}
    </section>
  );
}

/** Estado do template da Meta — sem ele aprovado, aviso fora da janela de 24h não chega. */
function EstadoTemplate({ t }: { t: AlertaTemplate }) {
  if (!t.templateAprovado) {
    return (
      <div className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
        <TriangleAlert className="mt-0.5 h-5 w-5 shrink-0" />
        <div>
          <p className="font-medium">
            O template <code>{t.nome}</code> não aparece no catálogo que o Automais.Zap expõe.
          </p>
          <p className="mt-1">
            Isso não prova que ele não foi aprovado — o catálogo do relay costuma listar só parte dos
            templates. Use <strong>Enviar teste agora</strong>: a resposta da Meta aparece no resultado e no
            histórico. Quem escreveu para o número da Secretaria nas últimas 23 horas recebe por mensagem
            normal de qualquer jeito. Variáveis configuradas:{' '}
            {t.parametrosConfigurados.map((p, i) => (
              <code key={p + i} className="mr-1">{`{{${i + 1}}}=${p}`}</code>
            ))}
          </p>
        </div>
      </div>
    );
  }
  return (
    <div className="flex gap-3 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
      <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
      <div>
        <p className="font-medium">
          Template <code>{t.nome}</code> aprovado ({t.parametrosAprovados ?? 0} variáveis).
        </p>
        {t.templateCorpo && <p className="mt-1 whitespace-pre-wrap text-emerald-800">{t.templateCorpo}</p>}
        <p className="mt-1 text-xs">
          Preenchimento: {t.parametrosConfigurados.map((p, i) => `{{${i + 1}}}=${p}`).join(', ')}. Teto de{' '}
          {t.tetoDiario} avisos por dia.
        </p>
      </div>
    </div>
  );
}

function Destinatarios({
  lista,
  porIntegracao,
  podeEditar,
}: {
  lista: AlertaDestinatario[];
  porIntegracao: { provedor: string; telefones: string[] }[];
  podeEditar: boolean;
}) {
  const adicionar = useAdicionarDestinatario();
  const atualizar = useAtualizarDestinatario();
  const remover = useRemoverDestinatario();
  const testar = useTestarAlerta();
  const [telefone, setTelefone] = useState('');
  const [nome, setNome] = useState('');
  const [resultadoTeste, setResultadoTeste] = useState<AlertaEnvio | null>(null);

  async function aoAdicionar() {
    if (telefone.replace(/\D/g, '').length < 10) {
      notificar('Informe o telefone com DDD.', 'erro');
      return;
    }
    try {
      await adicionar.mutateAsync({ telefone, nome: nome.trim() || null, ativo: true });
      setTelefone('');
      setNome('');
      notificar('Telefone adicionado.', 'sucesso');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  async function aoTestar() {
    setResultadoTeste(null);
    try {
      setResultadoTeste(await testar.mutateAsync());
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    }
  }

  return (
    <Cartao
      titulo="Quem recebe"
      acoes={
        podeEditar && (
          <Button variante="outline" tamanho="sm" disabled={testar.isPending || lista.length === 0} onClick={aoTestar}>
            {testar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            Enviar teste agora
          </Button>
        )
      }
    >
      {resultadoTeste && (
        <div className="mb-3 rounded-md border border-gray-200 bg-gray-50 p-3 text-sm">
          <div className="mb-1 flex items-center gap-2">
            <SeloSituacao situacao={resultadoTeste.situacao} />
            <span className="text-gray-600">resultado do teste</span>
          </div>
          <pre className="whitespace-pre-wrap font-mono text-xs text-gray-700">{resultadoTeste.resultado}</pre>
        </div>
      )}

      {lista.length === 0 ? (
        <p className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
          Ninguém recebe os erros da plataforma. Cadastre pelo menos um celular.
        </p>
      ) : (
        <ul className="mb-3 divide-y divide-gray-100">
          {lista.map((d) => (
            <li key={d.id} className="flex flex-wrap items-center gap-3 py-2 text-sm">
              <span className={`font-medium ${d.ativo ? 'text-gray-900' : 'text-gray-400 line-through'}`}>
                {formatarTelefone(d.telefone)}
              </span>
              <span className="text-gray-600">{d.nome ?? ''}</span>
              {podeEditar && (
                <span className="ml-auto flex items-center gap-2">
                  <label className="flex items-center gap-1 text-xs text-gray-600">
                    <input
                      type="checkbox"
                      checked={d.ativo}
                      onChange={(e) =>
                        atualizar
                          .mutateAsync({ id: d.id, body: { telefone: d.telefone, nome: d.nome, ativo: e.target.checked } })
                          .catch((err) => notificar(extrairMensagemDeErro(err), 'erro'))
                      }
                    />
                    recebe
                  </label>
                  <button
                    type="button"
                    title="Remover"
                    className="text-gray-400 hover:text-red-600"
                    onClick={() => remover.mutateAsync(d.id).catch((err) => notificar(extrairMensagemDeErro(err), 'erro'))}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {podeEditar && (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            className="w-48"
            placeholder="(21) 99999-0000"
            value={telefone}
            onChange={(e) => setTelefone(e.target.value)}
          />
          <Input
            className="w-56"
            placeholder="Nome (opcional)"
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                void aoAdicionar();
              }
            }}
          />
          <Button variante="outline" tamanho="sm" disabled={adicionar.isPending} onClick={aoAdicionar}>
            {adicionar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
            Adicionar
          </Button>
        </div>
      )}

      {porIntegracao.length > 0 && (
        <p className="mt-3 text-xs text-gray-500">
          Também recebem os avisos do próprio sincronismo (cadastrados na integração):{' '}
          {porIntegracao
            .map((p) => `${p.provedor.toUpperCase()}: ${p.telefones.map(formatarTelefone).join(', ')}`)
            .join(' · ')}
        </p>
      )}
    </Cartao>
  );
}

function OQueEReportado({
  origens,
  podeEditar,
  aoVerHistorico,
}: {
  origens: AlertaOrigem[];
  podeEditar: boolean;
  aoVerHistorico: (chave: string) => void;
}) {
  const silenciar = useSilenciarOrigem();
  const grupos = useMemo(() => {
    const mapa = new Map<string, AlertaOrigem[]>();
    for (const o of origens) mapa.set(o.grupo, [...(mapa.get(o.grupo) ?? []), o]);
    return [...mapa.entries()];
  }, [origens]);

  return (
    <Cartao titulo="O que é reportado">
      <div className="mb-4 space-y-1 text-sm text-gray-600">
        <p>
          <strong>Fontes fixas</strong> (robô, conta da IA, erro 500 novo, sincronismos) mais{' '}
          <strong>todo erro que qualquer motor gravar no log</strong>: varreduras SISREG/SER/SERNIT,
          escalas, fila, histórico, PEP/Salux, worklist, imagens, comunicação ao paciente, pesquisa de
          satisfação, treinamento do robô. Erro novo entra nesta lista sozinho na primeira vez que
          acontece, já reportando.
        </p>
        <p>
          <strong>Freio:</strong> a mesma fonte não avisa de novo antes de 30 min; se continua falhando, o
          intervalo dobra (1 h, 2 h… até 16 h) e o aviso seguinte diz quantas ocorrências foram seguradas.
          Doze horas sem ocorrer, a fonte volta a avisar na hora. Silenciar mantém a contagem, só não manda.
        </p>
      </div>

      <div className="space-y-5">
        {grupos.map(([grupo, lista]) => (
          <div key={grupo}>
            <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">{grupo}</h3>
            <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
              {lista.map((o) => (
                <li key={o.chave} className="flex flex-wrap items-start gap-3 px-3 py-2 text-sm">
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`font-medium ${o.silenciada ? 'text-gray-400' : 'text-gray-900'}`}>{o.rotulo}</span>
                      {o.silenciada && (
                        <span className="rounded-full bg-gray-200 px-2 py-0.5 text-xs text-gray-600">silenciada</span>
                      )}
                      {!o.catalogada && (
                        <span className="rounded-full bg-sky-100 px-2 py-0.5 text-xs text-sky-800">do log</span>
                      )}
                    </div>
                    <p className="text-xs text-gray-500">{o.catalogada ? o.descricao : (o.ultimoTitulo ?? o.descricao)}</p>
                    {o.ocorrencias > 0 && (
                      <p className="mt-0.5 text-xs text-gray-500">
                        {o.ocorrencias} ocorrência(s) · última {quando(o.ultimaOcorrenciaEm)} · último aviso{' '}
                        {quando(o.ultimoAvisoEm)}
                        {o.ocorrenciasSemAviso > 0 && ` · ${o.ocorrenciasSemAviso} segurada(s) pelo freio`}
                        {' · '}
                        <button type="button" className="text-primary-700 underline" onClick={() => aoVerHistorico(o.chave)}>
                          ver avisos
                        </button>
                      </p>
                    )}
                  </div>
                  {podeEditar && (
                    <Button
                      variante="ghost"
                      tamanho="sm"
                      disabled={silenciar.isPending}
                      title={o.silenciada ? 'Voltar a mandar para o celular' : 'Parar de mandar (continua contando)'}
                      onClick={() =>
                        silenciar
                          .mutateAsync({ chave: o.chave, silenciada: !o.silenciada })
                          .catch((err) => notificar(extrairMensagemDeErro(err), 'erro'))
                      }
                    >
                      {o.silenciada ? <BellRing className="h-4 w-4" /> : <BellOff className="h-4 w-4" />}
                      {o.silenciada ? 'Reportar' : 'Silenciar'}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </Cartao>
  );
}

function Historico({
  envios,
  filtro,
  rotuloFiltro,
  aoLimparFiltro,
}: {
  envios: AlertaEnvio[];
  filtro: string | null;
  rotuloFiltro: string | null;
  aoLimparFiltro: () => void;
}) {
  const filtrados = useEnviosAlerta(filtro);
  const dados = filtro ? (filtrados.data ?? []) : envios;

  const colunas: Coluna<AlertaEnvio>[] = [
    { chave: 'quando', cabecalho: 'Quando', className: 'w-36', render: (e) => quando(e.criadoEm) },
    { chave: 'fonte', cabecalho: 'Fonte', className: 'w-48', render: (e) => e.origemRotulo ?? e.origemChave },
    {
      chave: 'aviso',
      cabecalho: 'Aviso',
      render: (e) => (
        <div>
          <div className="font-medium text-gray-900">{e.titulo}</div>
          <div className="line-clamp-2 whitespace-pre-wrap text-xs text-gray-500">{e.detalhe}</div>
          {e.ocorrencias > 1 && <div className="text-xs text-gray-500">{e.ocorrencias} ocorrências agrupadas</div>}
        </div>
      ),
    },
    {
      chave: 'situacao',
      cabecalho: 'Situação',
      className: 'w-72',
      render: (e) => (
        <div>
          <SeloSituacao situacao={e.situacao} />
          {e.resultado && (
            <pre className="mt-1 whitespace-pre-wrap font-mono text-[11px] text-gray-600">{e.resultado}</pre>
          )}
        </div>
      ),
    },
  ];

  return (
    <Cartao
      titulo="Histórico do que foi enviado"
      acoes={
        filtro && (
          <Button variante="ghost" tamanho="sm" onClick={aoLimparFiltro}>
            Mostrar todos (filtrando: {rotuloFiltro ?? filtro})
          </Button>
        )
      }
    >
      <Tabela
        colunas={colunas}
        dados={dados}
        chaveLinha={(e) => e.id}
        carregando={Boolean(filtro) && filtrados.isLoading}
        vazio="Nenhum aviso enviado ainda."
      />
    </Cartao>
  );
}

/**
 * Sistema → Avisos no celular. Todo erro da plataforma (robô, sincronismos, erro 500, qualquer
 * erro gravado no log) é mandado por WhatsApp aos telefones daqui — com o template
 * `erro_plataforma` quando a janela de 24h está fechada.
 */
export function AvisosCelularPage() {
  const painel = usePainelAlertas();
  const podeEditar = usePermissao('Erros', 'Edicao');
  const [filtro, setFiltro] = useState<string | null>(null);

  if (painel.isLoading) return <div className="p-6 text-sm text-gray-500">Carregando…</div>;
  if (painel.isError || !painel.data)
    return (
      <div className="m-6 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(painel.error)}
      </div>
    );

  const p = painel.data;
  const rotuloFiltro = filtro ? (p.origens.find((o) => o.chave === filtro)?.rotulo ?? null) : null;

  return (
    <div className="mx-auto max-w-5xl space-y-5 p-4 md:p-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <BellRing className="h-6 w-6 text-primary-600" />
          Avisos no celular
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Erros da plataforma mandados por WhatsApp a quem cuida dela: robô que parou de responder, crédito da
          IA, sincronismo diário que falhou, erro 500 novo e qualquer erro gravado no log pelos motores.
        </p>
      </header>

      <EstadoTemplate t={p.template} />
      <Destinatarios lista={p.destinatarios} porIntegracao={p.telefonesPorIntegracao} podeEditar={podeEditar} />
      <OQueEReportado origens={p.origens} podeEditar={podeEditar} aoVerHistorico={setFiltro} />
      <Historico envios={p.envios} filtro={filtro} rotuloFiltro={rotuloFiltro} aoLimparFiltro={() => setFiltro(null)} />
    </div>
  );
}
