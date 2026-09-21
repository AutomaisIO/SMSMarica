import { useState, type ReactNode } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante, formatarWallClock, hojeSP } from '@/shared/lib/datas';
import { cn } from '@/shared/lib/cn';
import { Modal } from '@/shared/ui/Modal';
import { useAtosDaAtendente, useEquipeConfirmacoes } from '@/features/confirmacoes/api';
import type { AtendenteProducao } from '@/features/confirmacoes/types';

const PRESETS = [
  { rotulo: '7d', dias: 7 },
  { rotulo: '30d', dias: 30 },
  { rotulo: '90d', dias: 90 },
] as const;

/** Como cada tipo da trilha aparece na linha do tempo da atendente. */
const ROTULO_ATO: Record<string, string> = {
  Atendido: 'Começou a atender',
  Assumido: 'Assumiu de outra atendente',
  Retomado: 'Retomou uma pendente',
  Transferido: 'Transferiu',
  Liberado: 'Liberou sem desfecho',
  Confirmado: 'Confirmou a presença',
  Cancelado: 'Cancelou a vaga',
  CanceladoNoSisreg: 'SISREG confirmou o cancelamento',
  SisregRecusouCancelamento: 'SISREG recusou o cancelamento',
  PacienteAvisadoCancelamento: 'Avisou o paciente do cancelamento',
  EnviadoPendente: 'Estacionou como pendente',
  ContatoErrado: 'Marcou telefone errado',
  ContatoCorrigido: 'Resolveu o telefone errado',
  PedidoCancelamentoDesfeito: 'Desfez um pedido de cancelamento',
};

function menosDias(iso: string, dias: number): string {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() - dias);
  return d.toISOString().slice(0, 10);
}

function minutos(v: number | null): string {
  if (v === null) return '—';
  if (v < 1) return '< 1 min';
  if (v < 90) return `${v.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} min`;
  return `${(v / 60).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} h`;
}

/**
 * Aba Equipe — quem fez o quê na tela de Confirmações. Só existe para quem tem o módulo
 * `ConfirmacoesEquipe`: quem atende não vê o ranking das colegas. Os números são ATOS lidos da
 * trilha (append-only); a trilha começou em 21/09/2026, e a tela diz isso em vez de mostrar zero
 * como se fosse ociosidade.
 */
export function AbaEquipe() {
  const [ate, setAte] = useState(() => hojeSP());
  const [de, setDe] = useState(() => menosDias(hojeSP(), 6));
  const [aberta, setAberta] = useState<AtendenteProducao | null>(null);

  const q = useEquipeConfirmacoes(de, ate, true);
  const dados = q.data;

  function preset(dias: number) {
    const hoje = hojeSP();
    setAte(hoje);
    setDe(menosDias(hoje, dias - 1));
  }

  const periodoAntesDaTrilha = dados?.trilhaDesde ? de < dados.trilhaDesde.slice(0, 10) : false;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-3xl text-sm text-gray-600">
          O que cada atendente fez nesta tela no período. Cada número é um <strong>ato</strong> dela (um clique com
          desfecho), não uma ficha — a mesma ficha pode ser pega por uma e resolvida por outra.
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex overflow-hidden rounded-md border border-gray-200">
            {PRESETS.map((p) => (
              <button
                key={p.rotulo}
                type="button"
                onClick={() => preset(p.dias)}
                className="border-r border-gray-200 px-2.5 py-1 text-xs text-gray-600 last:border-r-0 hover:bg-gray-50"
              >
                {p.rotulo}
              </button>
            ))}
          </div>
          <input type="date" value={de} max={ate} onChange={(e) => setDe(e.target.value)} aria-label="De"
            className="rounded-md border border-gray-200 px-2 py-1 text-xs" />
          <span className="text-xs text-gray-400">até</span>
          <input type="date" value={ate} min={de} max={hojeSP()} onChange={(e) => setAte(e.target.value)} aria-label="Até"
            className="rounded-md border border-gray-200 px-2 py-1 text-xs" />
        </div>
      </div>

      {periodoAntesDaTrilha && dados?.trilhaDesde ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
          O registro de quem fez cada ação começou em <strong>{formatarInstante(dados.trilhaDesde)}</strong>. O que foi
          feito antes disso não aparece aqui — não é que ninguém trabalhou, é que ainda não se anotava.
        </p>
      ) : null}

      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}

      {q.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : dados ? (
        <>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-8">
            <Numero titulo="Desfechos" valor={dados.total.desfechos} detalhe="Fichas tiradas da frente, para qualquer lado." />
            <Numero titulo="Confirmou" valor={dados.total.confirmou} detalhe="Presença confirmada pela atendente." />
            <Numero titulo="Cancelou a vaga" valor={dados.total.cancelou} detalhe={`${dados.total.cancelouNoSisreg} com o SISREG confirmando.`} />
            <Numero titulo="SISREG recusou" valor={dados.total.sisregRecusou} detalhe="Tentou cancelar e o SISREG não deixou." alerta={dados.total.sisregRecusou > 0} />
            <Numero titulo="Telefone errado" valor={dados.total.contatoErrado} detalhe={`${dados.total.contatoCorrigido} resolvido(s) no período.`} />
            <Numero titulo="Pendentes" valor={dados.total.pendente} detalhe="Estacionadas com motivo." />
            <Numero titulo="Até o desfecho" texto={minutos(dados.total.tempoAteDesfechoMin)} detalhe="Mediana entre pegar a ficha e resolver." />
            <Numero titulo="Ritmo" texto={minutos(dados.total.ritmoMin)} detalhe={`Mediana entre um desfecho e o próximo (pausa > ${dados.pausaMin} min não conta).`} />
          </div>

          {dados.atendentes.length === 0 ? (
            <p className="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">
              Nenhuma ação de atendente registrada no período.
            </p>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-left text-xs text-gray-500">
                  <tr>
                    <Th>Atendente</Th>
                    <Th num titulo="Começou a atender, assumiu ou retomou">Pegou</Th>
                    <Th num>Confirmou</Th>
                    <Th num titulo="Cancelou a vaga aqui">Cancelou</Th>
                    <Th num titulo="Cancelamentos que o SISREG confirmou / tentativas que ele recusou">SISREG ok / recusou</Th>
                    <Th num titulo="Avisos de cancelamento que saíram na hora para o paciente">Avisou</Th>
                    <Th num>Pendente</Th>
                    <Th num titulo="Marcou telefone errado / resolveu telefone errado">Tel. errado / resolveu</Th>
                    <Th num titulo="Liberou sem desfecho / transferiu para colega">Liberou / transf.</Th>
                    <Th num titulo="Total de desfechos">Desfechos</Th>
                    <Th num titulo="Mediana entre pegar a ficha e dar o desfecho">Até o desfecho</Th>
                    <Th num titulo="Mediana entre um desfecho e o seguinte">Ritmo</Th>
                    <Th num>Dias</Th>
                    <Th>Última ação</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {dados.atendentes.map((a) => (
                    <tr key={a.usuarioId} onClick={() => setAberta(a)} className="cursor-pointer hover:bg-gray-50">
                      <td className="px-3 py-2 font-medium text-red-700 hover:underline">{a.nome}</td>
                      <Td>{a.pegou}</Td>
                      <Td>{a.confirmou}</Td>
                      <Td>{a.cancelou}</Td>
                      <Td>
                        {a.cancelouNoSisreg} / <span className={cn(a.sisregRecusou > 0 && 'font-semibold text-amber-700')}>{a.sisregRecusou}</span>
                      </Td>
                      <Td>{a.avisouPaciente}</Td>
                      <Td>{a.pendente}</Td>
                      <Td>{a.contatoErrado} / {a.contatoCorrigido}</Td>
                      <Td>{a.liberou} / {a.transferiu}</Td>
                      <Td forte>{a.desfechos}</Td>
                      <Td>{minutos(a.tempoAteDesfechoMin)}</Td>
                      <Td>{minutos(a.ritmoMin)}</Td>
                      <Td>{a.diasAtivos}</Td>
                      <td className="whitespace-nowrap px-3 py-2 text-xs text-gray-500">{formatarInstante(a.ultimaAcaoEm)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {dados.porDia.length > 1 ? (
            <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-left text-xs text-gray-500">
                  <tr><Th>Dia</Th><Th num>Confirmou</Th><Th num>Cancelou</Th><Th num titulo="Pendente, telefone errado, telefone resolvido, pedido desfeito">Outros desfechos</Th></tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {dados.porDia.map((d) => (
                    <tr key={d.dia}>
                      <td className="px-3 py-1.5 text-gray-700">{formatarWallClock(d.dia)}</td>
                      <Td>{d.confirmou}</Td><Td>{d.cancelou}</Td><Td>{d.outros}</Td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </>
      ) : null}

      {aberta ? <ModalAtos atendente={aberta} de={de} ate={ate} aoFechar={() => setAberta(null)} /> : null}
    </div>
  );
}

function ModalAtos({ atendente, de, ate, aoFechar }: { atendente: AtendenteProducao; de: string; ate: string; aoFechar: () => void }) {
  const q = useAtosDaAtendente(atendente.usuarioId, de, ate);
  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      largura="lg"
      titulo={atendente.nome}
      descricao={`O que fez entre ${formatarWallClock(de)} e ${formatarWallClock(ate)}, do mais recente para o mais antigo (até 500 atos).`}
    >
      {q.isLoading ? <p className="text-sm text-gray-500">Carregando…</p> : null}
      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}
      <ol className="max-h-[60vh] space-y-1.5 overflow-y-auto pr-1">
        {(q.data ?? []).map((a, i) => (
          <li key={`${a.ocorridoEm}-${i}`} className="rounded-md border border-gray-100 px-3 py-2 text-sm">
            <div className="flex flex-wrap items-baseline justify-between gap-x-3">
              <span className="font-medium text-gray-900">{ROTULO_ATO[a.tipo] ?? a.tipo}</span>
              <span className="text-xs text-gray-500">{formatarInstante(a.ocorridoEm, { dateStyle: 'short', timeStyle: 'medium' })}</span>
            </div>
            <p className="text-xs text-gray-600">
              {a.codigoSolicitacao ? <>SISREG {a.codigoSolicitacao}</> : 'Sem código do SISREG'}
              {a.procedimento ? <> · {a.procedimento}</> : null}
            </p>
            {a.observacao ? <p className="mt-0.5 text-xs italic text-gray-500">{a.observacao}</p> : null}
          </li>
        ))}
      </ol>
    </Modal>
  );
}

function Numero({ titulo, valor, texto, detalhe, alerta }: { titulo: string; valor?: number; texto?: string; detalhe: string; alerta?: boolean }) {
  return (
    <div className={cn('rounded-lg border bg-white px-3 py-2', alerta ? 'border-amber-300' : 'border-gray-200')}>
      <p className="text-xs text-gray-500">{titulo}</p>
      <p className="text-xl font-semibold text-gray-900">{texto ?? (valor ?? 0).toLocaleString('pt-BR')}</p>
      <p className="mt-0.5 text-xs leading-snug text-gray-500">{detalhe}</p>
    </div>
  );
}

function Th({ children, num, titulo }: { children: ReactNode; num?: boolean; titulo?: string }) {
  return <th title={titulo} className={cn('whitespace-nowrap px-3 py-2 font-medium', num && 'text-right')}>{children}</th>;
}

function Td({ children, forte }: { children: ReactNode; forte?: boolean }) {
  return <td className={cn('whitespace-nowrap px-3 py-2 text-right tabular-nums text-gray-700', forte && 'font-semibold text-gray-900')}>{children}</td>;
}
