import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  ArrowRightLeft,
  Check,
  CheckCheck,
  CheckCircle2,
  Clock,
  Hand,
  Loader2,
  MessageSquareText,
  PhoneOff,
  Undo2,
  UserRound,
  X,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { Passos } from '@/features/manual/components/Passos';
import { TelaSimulada } from '@/features/manual/components/TelaSimulada';
import { BotaoRef } from '@/features/manual/components/Referencia';
import {
  FICHAS_INICIAIS,
  abaDaFicha,
  quandoDaFicha,
  type AbaSim,
  type FichaSim,
  type StatusEnvioSim,
} from '@/features/manual/simulacoes/dadosSimulados';

const ABAS: { id: AbaSim; rotulo: string }[] = [
  { id: 'NaoConfirmados', rotulo: 'Não confirmados' },
  { id: 'Confirmados', rotulo: 'Confirmados' },
  { id: 'ContatoErrado', rotulo: 'Contato errado' },
  { id: 'Pendentes', rotulo: 'Pendentes' },
  { id: 'TelefoneComprometido', rotulo: 'Telefone comprometido' },
];

const MEIOS = ['Ligação', 'WhatsApp', 'Presencial', 'Outro'];

type ModalSim =
  | null
  | { tipo: 'confirmar' | 'pendente' | 'contato-errado' | 'cancelar' | 'contato-corrigido'; id: string };

const agora = () => new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });

const primeiroNome = (f: FichaSim) => f.nome.split(' ')[0];

/**
 * A tela de Confirmações inteira, de mentira, dentro do manual.
 *
 * Por que uma cópia clicável e não um print: confirmar agendamento é uma SEQUÊNCIA de decisões
 * (pegar a ficha, falar, dar o desfecho), e imagem parada não ensina sequência. Aqui a pessoa erra
 * à vontade — não há paciente do outro lado nem vaga que se perde.
 *
 * O roteiro do topo é DERIVADO do estado, não de um contador de cliques: quem sai dele e volta
 * sozinho vê o passo certo acender de novo.
 */
export function SimulacaoConfirmacoes() {
  const [fichas, setFichas] = useState<FichaSim[]>(FICHAS_INICIAIS);
  const [aba, setAba] = useState<AbaSim>('NaoConfirmados');
  const [modal, setModal] = useState<ModalSim>(null);
  const [eventos, setEventos] = useState<string[]>([]);
  const [recemMudada, setRecemMudada] = useState<string | null>(null);

  function reiniciar() {
    setFichas(FICHAS_INICIAIS);
    setAba('NaoConfirmados');
    setModal(null);
    setEventos([]);
    setRecemMudada(null);
  }

  function registrar(texto: string) {
    setEventos((e) => [`${agora()} · ${texto}`, ...e].slice(0, 8));
  }

  function mudar(id: string, muda: (f: FichaSim) => FichaSim, evento: string) {
    setFichas((atual) => {
      const antes = atual.find((f) => f.id === id);
      const novas = atual.map((f) => (f.id === id ? muda(f) : f));
      const depois = novas.find((f) => f.id === id);
      if (antes && depois && abaDaFicha(antes) !== abaDaFicha(depois)) setRecemMudada(id);
      return novas;
    });
    registrar(evento);
  }

  const f1 = fichas.find((f) => f.id === 'f1')!;
  const desviou = Boolean(f1.cancelado || f1.numeroNegado || f1.posse?.situacao === 'Pendente');
  const passo = desviou
    ? -1
    : f1.confirmado
      ? aba === 'Confirmados'
        ? 4
        : 3
      : modal?.tipo === 'confirmar' && modal.id === 'f1'
        ? 2
        : f1.posse?.de === 'voce'
          ? 1
          : 0;

  const contagem = useMemo(() => {
    const c: Record<AbaSim, number> = {
      NaoConfirmados: 0,
      Confirmados: 0,
      ContatoErrado: 0,
      Pendentes: 0,
      TelefoneComprometido: 0,
    };
    for (const f of fichas) {
      const a = abaDaFicha(f);
      if (a) c[a] += 1;
    }
    return c;
  }, [fichas]);

  const daAba = fichas.filter((f) => abaDaFicha(f) === aba);
  const fichaDoModal = modal ? fichas.find((f) => f.id === modal.id) ?? null : null;

  return (
    <TelaSimulada aoReiniciar={reiniciar}>
      <div className="space-y-4">
        <Passos
          itens={[
            {
              titulo: (
                <>
                  Pegue a ficha: clique em <BotaoRef>Atender</BotaoRef> no card de Maria Aparecida
                </>
              ),
              detalhe: 'Enquanto ela é sua, ninguém mais liga para a mesma pessoa — e o aviso automático para de sair.',
            },
            {
              titulo: (
                <>
                  Ligou e ela vai comparecer? Clique em <BotaoRef>Confirmar</BotaoRef>
                </>
              ),
            },
            {
              titulo: 'Diga como falou com ela e conclua',
              detalhe: 'O meio fica registrado: é ele que explica, depois, de onde veio a confirmação.',
            },
            {
              titulo: (
                <>
                  Abra a aba <b>Confirmados</b> e encontre-a lá
                </>
              ),
              detalhe: 'A ficha muda de fila sozinha — não existe "salvar" nesta tela.',
            },
          ]}
          atual={passo >= 0 && passo < 4 ? passo : undefined}
          concluidos={passo < 0 ? 0 : passo}
        />

        {passo === 4 ? (
          <p className="rounded-theme-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">
            É isso. Você acabou de fazer, do começo ao fim, o que a tela pede o dia inteiro. Agora explore o resto: mande
            uma para pendente, registre um contato errado, cancele um agendamento.
          </p>
        ) : null}
        {passo < 0 ? (
          <p className="rounded-theme-md border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900">
            Você seguiu por outro caminho — é para isso que a simulação existe. Use <b>Recomeçar</b> lá em cima para
            refazer o roteiro.
          </p>
        ) : null}

        {/* Abas — os contadores são derivados das fichas, como na tela real. */}
        <div className="border-b border-gray-200">
          <nav className="-mb-px flex flex-wrap gap-1" role="tablist">
            {ABAS.map((a) => (
              <button
                key={a.id}
                type="button"
                role="tab"
                aria-selected={a.id === aba}
                onClick={() => setAba(a.id)}
                className={cn(
                  '-mb-px whitespace-nowrap rounded-t-md border-b-2 px-3 py-2 text-sm font-medium transition-colors',
                  a.id === aba
                    ? 'border-primary-600 text-primary-700'
                    : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700',
                  passo === 3 && a.id === 'Confirmados' ? 'anim-atencao rounded-md' : null,
                )}
              >
                {a.rotulo}
                <span className="ml-2 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">{contagem[a.id]}</span>
              </button>
            ))}
          </nav>
        </div>

        {daAba.length === 0 ? (
          <p className="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">
            Nada nesta fila.
          </p>
        ) : (
          <div className="space-y-2">
            {daAba.map((f) => (
              <CardSim
                key={f.id}
                ficha={f}
                aba={aba}
                destacarAtender={passo === 0 && f.id === 'f1'}
                destacarConfirmar={passo === 1 && f.id === 'f1'}
                recemMudada={recemMudada === f.id}
                aoAcao={(acao) => {
                  if (acao === 'atender' || acao === 'assumir') {
                    mudar(
                      f.id,
                      (x) => ({
                        ...x,
                        posse: { de: 'voce', nome: 'Você', desde: agora(), situacao: 'EmAtendimento' },
                      }),
                      acao === 'assumir'
                        ? `Você assumiu o atendimento de ${primeiroNome(f)} (a colega perde a ficha e é avisada).`
                        : `Você está atendendo ${primeiroNome(f)}. O envio automático desta ficha foi encerrado.`,
                    );
                    return;
                  }
                  if (acao === 'liberar') {
                    mudar(
                      f.id,
                      (x) => ({ ...x, posse: undefined }),
                      `Você liberou ${primeiroNome(f)} — a ficha voltou para a fila.`,
                    );
                    return;
                  }
                  setModal({ tipo: acao, id: f.id });
                }}
              />
            ))}
          </div>
        )}

        {eventos.length > 0 ? (
          <div className="rounded-theme-md border border-gray-200 bg-white p-3">
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">O que o sistema registrou</p>
            <ul className="mt-2 space-y-1 text-sm text-gray-700">
              {eventos.map((e, i) => (
                <li key={`${e}-${i}`} className={cn('flex gap-2', i === 0 && 'anim-entrada')}>
                  <span aria-hidden className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-primary-400" />
                  <span>{e}</span>
                </li>
              ))}
            </ul>
            <p className="mt-2 text-xs text-gray-500">
              Na tela real essa trilha fica gravada com o seu nome e a hora — é dela que sai a resposta para "quem falou
              com o paciente?".
            </p>
          </div>
        ) : null}
      </div>

      {fichaDoModal && modal?.tipo === 'confirmar' ? (
        <ModalConfirmarSim
          ficha={fichaDoModal}
          aoFechar={() => setModal(null)}
          aoConfirmar={(meio, obs) => {
            mudar(
              fichaDoModal.id,
              (x) => ({ ...x, confirmado: true, canal: 'atendente', envio: 'AtendidaPorPessoa', posse: undefined }),
              `Presença de ${primeiroNome(fichaDoModal)} confirmada por ${meio.toLowerCase()}${obs ? ` — "${obs}"` : ''}.`,
            );
            setModal(null);
          }}
        />
      ) : null}

      {fichaDoModal && modal?.tipo === 'pendente' ? (
        <ModalPendenteSim
          ficha={fichaDoModal}
          aoFechar={() => setModal(null)}
          aoEnviar={(motivo) => {
            mudar(
              fichaDoModal.id,
              (x) => ({ ...x, posse: { de: 'voce', nome: 'Você', desde: agora(), situacao: 'Pendente', motivo } }),
              `${primeiroNome(fichaDoModal)} foi para Pendentes: ${motivo}.`,
            );
            setModal(null);
          }}
        />
      ) : null}

      {fichaDoModal && modal?.tipo === 'contato-errado' ? (
        <ModalContatoErradoSim
          ficha={fichaDoModal}
          aoFechar={() => setModal(null)}
          aoRegistrar={(obs) => {
            mudar(
              fichaDoModal.id,
              (x) => ({
                ...x,
                numeroNegado: true,
                envio: 'NumeroNegado',
                posse: {
                  de: 'voce',
                  nome: 'Você',
                  desde: agora(),
                  situacao: 'ContatoErrado',
                  motivo: obs || undefined,
                },
              }),
              `Número de ${primeiroNome(fichaDoModal)} marcado como negado — nada automático sai mais para ele.`,
            );
            setModal(null);
          }}
        />
      ) : null}

      {fichaDoModal && modal?.tipo === 'cancelar' ? (
        <ModalCancelarSim
          ficha={fichaDoModal}
          aoFechar={() => setModal(null)}
          aoCancelar={(motivo, meio) => {
            mudar(
              fichaDoModal.id,
              (x) => ({ ...x, cancelado: true, posse: undefined }),
              `Agendamento de ${primeiroNome(fichaDoModal)} cancelado no SISREG e aqui (${meio.toLowerCase()}): ${motivo}.`,
            );
          }}
        />
      ) : null}

      {fichaDoModal && modal?.tipo === 'contato-corrigido' ? (
        <ModalContatoCorrigidoSim
          ficha={fichaDoModal}
          aoFechar={() => setModal(null)}
          aoCorrigido={(telefone) => {
            mudar(
              fichaDoModal.id,
              (x) => ({
                ...x,
                telefone,
                telefoneVerificado: true,
                numeroNegado: false,
                envio: 'NaFila',
                posse: undefined,
              }),
              `Telefone de ${primeiroNome(fichaDoModal)} corrigido e verificado — a ficha voltou para a fila automática.`,
            );
            setModal(null);
          }}
        />
      ) : null}
    </TelaSimulada>
  );
}

/* ------------------------------------------------------------------ cards */

function SeloEnvio({ status }: { status: StatusEnvioSim }) {
  switch (status) {
    case 'Lida':
      return (
        <span className="badge badge-success inline-flex items-center gap-1">
          <CheckCheck className="h-3 w-3 text-sky-600" /> Lida
        </span>
      );
    case 'Entregue':
      return (
        <span className="badge badge-gray inline-flex items-center gap-1">
          <CheckCheck className="h-3 w-3" /> Entregue
        </span>
      );
    case 'Enviada':
      return (
        <span className="badge badge-gray inline-flex items-center gap-1">
          <Check className="h-3 w-3" /> Enviada
        </span>
      );
    case 'Falhou':
      return (
        <span className="badge badge-error inline-flex items-center gap-1">
          <AlertTriangle className="h-3 w-3" /> Falhou
        </span>
      );
    case 'NaFila':
      return (
        <span className="badge badge-gray inline-flex items-center gap-1">
          <Clock className="h-3 w-3" /> Na fila
        </span>
      );
    case 'NumeroNegado':
      return <span className="badge badge-error">Número negado</span>;
    case 'AtendidaPorPessoa':
      return <span className="badge badge-gray">Atendida por pessoa</span>;
    default:
      return <span className="badge badge-gray">Não enviada</span>;
  }
}

type AcaoSim =
  | 'atender'
  | 'assumir'
  | 'liberar'
  | 'confirmar'
  | 'pendente'
  | 'contato-errado'
  | 'cancelar'
  | 'contato-corrigido';

function CardSim({
  ficha,
  aba,
  destacarAtender,
  destacarConfirmar,
  recemMudada,
  aoAcao,
}: {
  ficha: FichaSim;
  aba: AbaSim;
  destacarAtender: boolean;
  destacarConfirmar: boolean;
  recemMudada: boolean;
  aoAcao: (acao: AcaoSim) => void;
}) {
  const minha = ficha.posse?.de === 'voce' && ficha.posse.situacao === 'EmAtendimento';
  const deOutro = ficha.posse?.de === 'colega' && ficha.posse.situacao === 'EmAtendimento';
  const estacionada = ficha.posse?.situacao === 'Pendente' || ficha.posse?.situacao === 'ContatoErrado';
  const quando = quandoDaFicha(ficha);

  return (
    <article
      className={cn(
        'rounded-lg border bg-white p-3 shadow-sm transition-opacity',
        deOutro ? 'border-gray-200 opacity-60' : 'border-gray-200',
        minha ? 'border-primary-300 ring-1 ring-primary-100' : null,
        recemMudada ? 'anim-entrada' : null,
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-[200px] flex-1 space-y-1">
          <p className="font-medium text-gray-900">{ficha.nome}</p>
          <div className="flex flex-wrap items-center gap-2 text-xs text-gray-600">
            <span>CPF {ficha.cpf}</span>
            <span>{ficha.telefone}</span>
            {ficha.telefoneVerificado ? <span className="text-emerald-700">✔ verificado</span> : null}
            {ficha.numeroNegado ? <span className="text-red-700">❗ número negado</span> : null}
            {ficha.motivoSemCanal ? (
              <span className="rounded bg-amber-50 px-1.5 py-0.5 text-amber-900">
                {ficha.motivoSemCanal === 'SemCelular' ? 'sem celular' : 'não é WhatsApp'}
              </span>
            ) : null}
            {ficha.respondeuNoZap ? (
              <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-emerald-800">respondeu no zap</span>
            ) : null}
          </div>
        </div>

        <div className="min-w-[220px] flex-1 space-y-0.5 text-sm">
          <div className={cn('font-semibold tabular-nums', quando.ehHoje ? 'text-primary-700' : 'text-gray-900')}>
            {quando.texto}
          </div>
          <div className="text-gray-800">{ficha.procedimento}</div>
          <div className="text-xs text-gray-600">{ficha.unidade}</div>
        </div>

        <div className="flex min-w-[190px] flex-col items-start gap-1.5 text-xs">
          <div className="flex flex-wrap items-center gap-1.5">
            <SeloEnvio status={ficha.envio} />
            <span className={cn('badge', ficha.confirmado ? 'badge-success' : 'badge-warning')}>
              {ficha.confirmado ? `Confirmada${ficha.canal ? ` (${ficha.canal})` : ''}` : 'Sem resposta'}
            </span>
          </div>
          {ficha.posse ? (
            <div
              className={cn(
                'inline-flex items-center gap-1 rounded px-1.5 py-0.5',
                ficha.posse.de === 'voce' ? 'bg-primary-50 text-primary-800' : 'bg-gray-100 text-gray-700',
              )}
            >
              <UserRound className="h-3 w-3" />
              {ficha.posse.situacao === 'EmAtendimento'
                ? `${ficha.posse.de === 'voce' ? 'Você está atendendo' : `Em atendimento por ${ficha.posse.nome}`} desde ${ficha.posse.desde}`
                : ficha.posse.situacao === 'Pendente'
                  ? `Pendente (${ficha.posse.nome}): ${ficha.posse.motivo ?? 'sem motivo'}`
                  : `Contato errado (${ficha.posse.nome})${ficha.posse.motivo ? `: ${ficha.posse.motivo}` : ''}`}
            </div>
          ) : null}
        </div>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-1.5 border-t border-gray-100 pt-2">
        {!ficha.posse ? (
          <Button tamanho="sm" className={cn(destacarAtender && 'anim-atencao')} onClick={() => aoAcao('atender')}>
            <Hand className="mr-1 h-3.5 w-3.5" /> Atender
          </Button>
        ) : null}

        {deOutro ? (
          <Button tamanho="sm" variante="outline" onClick={() => aoAcao('assumir')}>
            <ArrowRightLeft className="mr-1 h-3.5 w-3.5" /> Assumir atendimento
          </Button>
        ) : null}

        {estacionada ? (
          <Button tamanho="sm" variante="outline" onClick={() => aoAcao('atender')}>
            <Undo2 className="mr-1 h-3.5 w-3.5" /> Retomar
          </Button>
        ) : null}

        {minha ? (
          <>
            {!ficha.confirmado ? (
              <Button
                tamanho="sm"
                className={cn(destacarConfirmar && 'anim-atencao')}
                onClick={() => aoAcao('confirmar')}
              >
                <Check className="mr-1 h-3.5 w-3.5" /> Confirmar
              </Button>
            ) : null}
            <Button tamanho="sm" variante="outline" onClick={() => aoAcao('pendente')}>
              <Clock className="mr-1 h-3.5 w-3.5" /> Enviar para pendente
            </Button>
            <Button tamanho="sm" variante="outline" onClick={() => aoAcao('contato-errado')}>
              <PhoneOff className="mr-1 h-3.5 w-3.5" /> Contato errado
            </Button>
            <Button tamanho="sm" variante="ghost" disabled title="Na simulação não há colegas para receber a ficha.">
              <ArrowRightLeft className="mr-1 h-3.5 w-3.5" /> Transferir
            </Button>
            <Button tamanho="sm" variante="ghost" onClick={() => aoAcao('liberar')}>
              Liberar
            </Button>
          </>
        ) : null}

        {aba === 'ContatoErrado' ? (
          <Button tamanho="sm" variante="outline" onClick={() => aoAcao('contato-corrigido')}>
            Corrigir contato
          </Button>
        ) : null}

        {minha || aba === 'Confirmados' || (!ficha.posse && aba === 'NaoConfirmados') ? (
          <Button tamanho="sm" variante="danger" className="ml-auto" onClick={() => aoAcao('cancelar')}>
            <X className="mr-1 h-3.5 w-3.5" /> Cancelar agendamento
          </Button>
        ) : null}
      </div>
    </article>
  );
}

/* ----------------------------------------------------------------- modais */

function Cabecalho({ ficha }: { ficha: FichaSim }) {
  const quando = quandoDaFicha(ficha);
  return (
    <p className="text-sm text-gray-600">
      <strong>{ficha.nome}</strong> · {ficha.procedimento} · {quando.texto}
    </p>
  );
}

function ModalConfirmarSim({
  ficha,
  aoFechar,
  aoConfirmar,
}: {
  ficha: FichaSim;
  aoFechar: () => void;
  aoConfirmar: (meio: string, observacao: string) => void;
}) {
  const [meio, setMeio] = useState(MEIOS[0]);
  const [obs, setObs] = useState('');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Confirmar presença">
      <div className="space-y-3">
        <Cabecalho ficha={ficha} />
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Como falou com o paciente</span>
          <Select value={meio} onChange={(e) => setMeio(e.target.value)}>
            {MEIOS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Observação (opcional)</span>
          <Input value={obs} onChange={(e) => setObs(e.target.value)} placeholder="ex.: confirmou com a filha" />
        </label>
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>
            Voltar
          </Button>
          <Button className="anim-atencao" onClick={() => aoConfirmar(meio, obs.trim())}>
            Confirmar presença
          </Button>
        </div>
      </div>
    </Modal>
  );
}

function ModalPendenteSim({
  ficha,
  aoFechar,
  aoEnviar,
}: {
  ficha: FichaSim;
  aoFechar: () => void;
  aoEnviar: (motivo: string) => void;
}) {
  const [motivo, setMotivo] = useState('');
  const sugestoes = ['Não atendeu', 'Caixa postal', 'Pediu para ligar depois', 'Não respondeu'];
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Enviar para pendente">
      <div className="space-y-3">
        <Cabecalho ficha={ficha} />
        <div className="flex flex-wrap gap-1.5">
          {sugestoes.map((s) => (
            <button
              key={s}
              type="button"
              className="rounded-full border border-gray-200 px-2 py-0.5 text-xs text-gray-700 hover:bg-gray-50"
              onClick={() => setMotivo(s)}
            >
              {s}
            </button>
          ))}
        </div>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Motivo</span>
          <Input value={motivo} onChange={(e) => setMotivo(e.target.value)} autoFocus />
        </label>
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>
            Voltar
          </Button>
          <Button disabled={motivo.trim().length === 0} onClick={() => aoEnviar(motivo.trim())}>
            Enviar para pendente
          </Button>
        </div>
      </div>
    </Modal>
  );
}

function ModalContatoErradoSim({
  ficha,
  aoFechar,
  aoRegistrar,
}: {
  ficha: FichaSim;
  aoFechar: () => void;
  aoRegistrar: (observacao: string) => void;
}) {
  const [obs, setObs] = useState('');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Contato errado">
      <div className="space-y-3">
        <Cabecalho ficha={ficha} />
        <p className="text-sm text-gray-600">
          O número <strong>{ficha.telefone}</strong> fica marcado como <strong>negado</strong> para este paciente: nada
          automático sai mais para ele. A ficha vai para a aba <strong>Contato errado</strong> até o telefone ser
          corrigido.
        </p>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">O que aconteceu (opcional)</span>
          <Input
            value={obs}
            onChange={(e) => setObs(e.target.value)}
            placeholder="ex.: atendeu outra pessoa, não conhece"
          />
        </label>
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>
            Voltar
          </Button>
          <Button variante="danger" onClick={() => aoRegistrar(obs.trim())}>
            Registrar contato errado
          </Button>
        </div>
      </div>
    </Modal>
  );
}

/**
 * Cancelamento, com a espera e o desfecho que a tela real tem.
 *
 * <para>A espera é de mentira (um tempo fixo), mas ela precisa existir: na tela real o sistema vai
 * ao SISREG, cancela e relê a ficha antes de responder, e quem não foi avisado disso conclui que
 * travou e clica de novo. E o desfecho fica <b>na própria janela</b> até a pessoa fechar — as três
 * linhas são o que ela tem para conferir se a vaga saiu de lá e se o paciente ficou sabendo.</para>
 */
function ModalCancelarSim({
  ficha,
  aoFechar,
  aoCancelar,
}: {
  ficha: FichaSim;
  aoFechar: () => void;
  aoCancelar: (motivo: string, meio: string) => void;
}) {
  const [motivo, setMotivo] = useState('');
  const [meio, setMeio] = useState(MEIOS[0]);
  const [etapa, setEtapa] = useState<'formulario' | 'cancelando' | 'desfecho'>('formulario');

  function confirmar() {
    setEtapa('cancelando');
    window.setTimeout(() => {
      aoCancelar(motivo.trim(), meio);
      setEtapa('desfecho');
    }, 1400);
  }

  if (etapa === 'desfecho') {
    return (
      <Modal aberto aoFechar={aoFechar} titulo="Cancelamento concluído">
        <div className="space-y-3">
          <Cabecalho ficha={ficha} />
          <ul className="space-y-2">
            <LinhaDesfecho
              icone={CheckCircle2}
              titulo="Aqui no sistema"
              detalhe="Agendamento cancelado e atendimento encerrado."
            />
            <LinhaDesfecho
              icone={CheckCircle2}
              titulo="No SISREG"
              detalhe={'A vaga foi liberada — a ficha agora diz “AGENDAMENTO / CANCELADO / REGULADOR”.'}
            />
            <LinhaDesfecho
              icone={MessageSquareText}
              titulo="Aviso ao paciente"
              detalhe="Mensagem de cancelamento enviada por WhatsApp."
            />
          </ul>
          <div className="flex justify-end">
            <Button onClick={aoFechar}>Fechar</Button>
          </div>
        </div>
      </Modal>
    );
  }

  const cancelando = etapa === 'cancelando';

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Cancelar agendamento">
      <div className="space-y-3">
        <Cabecalho ficha={ficha} />
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Motivo</span>
          <Input
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            placeholder="ex.: paciente pediu, vai fazer particular"
            autoFocus
            disabled={cancelando}
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Como falou com o paciente</span>
          <Select value={meio} onChange={(e) => setMeio(e.target.value)} disabled={cancelando}>
            {MEIOS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </Select>
        </label>
        {cancelando ? (
          <p className="flex items-center gap-2 rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-900">
            <Loader2 className="size-4 shrink-0 animate-spin" />
            <span>Cancelando no SISREG com o seu login e conferindo a ficha. Não feche esta janela.</span>
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar} disabled={cancelando}>
            Voltar
          </Button>
          <Button
            variante="danger"
            disabled={cancelando || motivo.trim().length === 0}
            onClick={confirmar}
          >
            {cancelando ? <Loader2 className="mr-1 size-4 animate-spin" /> : null}
            {cancelando ? 'Cancelando…' : 'Confirmo o cancelamento'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

/** Uma das três linhas do desfecho simulado. */
function LinhaDesfecho({ icone: Icone, titulo, detalhe }: {
  icone: typeof CheckCircle2;
  titulo: string;
  detalhe: string;
}) {
  return (
    <li className="flex items-start gap-2 rounded border border-gray-200 px-3 py-2">
      <Icone className="mt-0.5 size-4 shrink-0 text-emerald-600" />
      <div className="text-sm">
        <p className="font-medium text-gray-900">{titulo}</p>
        <p className="text-gray-600">{detalhe}</p>
      </div>
    </li>
  );
}

/**
 * Correção de contato: na tela real o número novo só vale depois que o paciente lê o código que
 * chega no WhatsApp dele. Aqui o código é qualquer coisa — o que a simulação ensina é a SEQUÊNCIA,
 * e que sem a verificação a ficha não volta para a fila automática.
 */
function ModalContatoCorrigidoSim({
  ficha,
  aoFechar,
  aoCorrigido,
}: {
  ficha: FichaSim;
  aoFechar: () => void;
  aoCorrigido: (telefone: string) => void;
}) {
  const [telefone, setTelefone] = useState(ficha.telefone);
  const [etapa, setEtapa] = useState<'numero' | 'codigo'>('numero');
  const [codigo, setCodigo] = useState('');
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Corrigir contato">
      <div className="space-y-3">
        <Cabecalho ficha={ficha} />
        {etapa === 'numero' ? (
          <>
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium text-gray-700">Celular do paciente</span>
              <Input value={telefone} onChange={(e) => setTelefone(e.target.value)} autoFocus />
            </label>
            <p className="text-sm text-gray-600">
              O sistema manda um código de 6 dígitos para este número. Quem lê o código é o paciente — é isso que prova
              que o telefone é dele.
            </p>
            <div className="flex justify-end gap-2">
              <Button variante="outline" onClick={aoFechar}>
                Voltar
              </Button>
              <Button disabled={telefone.trim().length < 8} onClick={() => setEtapa('codigo')}>
                Enviar código
              </Button>
            </div>
          </>
        ) : (
          <>
            <p className="rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900">
              Simulação: digite quaisquer 6 dígitos (ex.: <strong>123456</strong>).
            </p>
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium text-gray-700">Código recebido</span>
              <Input value={codigo} onChange={(e) => setCodigo(e.target.value)} inputMode="numeric" autoFocus />
            </label>
            <div className="flex justify-end gap-2">
              <Button variante="outline" onClick={() => setEtapa('numero')}>
                Voltar
              </Button>
              <Button disabled={codigo.trim().length < 6} onClick={() => aoCorrigido(telefone.trim())}>
                Verificar e liberar
              </Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
