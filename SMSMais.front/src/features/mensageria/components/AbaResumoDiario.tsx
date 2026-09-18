import { useMemo, useState } from 'react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useResumoDiario, useRegrasUnidades } from '@/features/mensageria/api/queries';
import { diaLegivel, hojeMais } from '@/features/mensageria/lib/rotulos';
import type { DiaMensageria } from '@/features/mensageria/types';

function nf(n: number): string {
  return n.toLocaleString('pt-BR');
}

function Tile({ rotulo, valor, sufixo, tom }: { rotulo: string; valor: number; sufixo?: string; tom?: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3">
      <div className={`text-2xl font-semibold tabular-nums ${tom ?? 'text-gray-900'}`}>
        {nf(valor)}
        {sufixo ? <span className="ml-0.5 text-base font-normal text-gray-500">{sufixo}</span> : null}
      </div>
      <div className="text-xs text-gray-500">{rotulo}</div>
    </div>
  );
}

const COLUNAS: { chave: keyof DiaMensageria; rotulo: string; dica?: string; tom?: string }[] = [
  { chave: 'enfileiradas', rotulo: 'Entraram', dica: 'Comunicações criadas no dia (coorte).' },
  { chave: 'enviadasNoDia', rotulo: 'Saíram no dia', dica: 'Enviadas de fato neste dia (qualquer coorte).' },
  { chave: 'enviadas', rotulo: 'Enviadas', dica: 'Da coorte do dia, quantas já saíram.' },
  { chave: 'entregues', rotulo: 'Entregues' },
  { chave: 'lidas', rotulo: 'Lidas' },
  { chave: 'visualizadas', rotulo: 'Abriram', dica: 'Clicaram no link / abriram no app.' },
  { chave: 'falhas', rotulo: 'Falhas', tom: 'text-red-700' },
  { chave: 'naFila', rotulo: 'Na fila' },
  { chave: 'aguardandoIdentificacao', rotulo: 'Aguard. identif.', dica: 'Número não verificado: recebeu o desafio do CPF.' },
  { chave: 'numeroNegado', rotulo: 'Nº negado', dica: 'Quem atende disse que não é o paciente.', tom: 'text-red-700' },
  { chave: 'semTelefone', rotulo: 'Sem celular' },
  { chave: 'aguardandoVerificado', rotulo: 'Aguard. verificado', dica: 'Resultado/laudo retido até verificar o contato.' },
  { chave: 'substituidasPorAtendente', rotulo: 'Atendidas por pessoa', dica: 'Uma atendente entrou antes de a mensagem sair.' },
  { chave: 'confirmadas', rotulo: 'Confirmaram', tom: 'text-emerald-700' },
  { chave: 'canceladas', rotulo: 'Não vão', tom: 'text-amber-700' },
  { chave: 'semResposta', rotulo: 'Sem resposta' },
];

/**
 * Visão do gestor: um resumo por dia do que entrou, saiu, chegou, falhou (e por quê) e do que o
 * paciente respondeu. Mede a qualidade da entrega via zap.
 */
export function AbaResumoDiario() {
  const [de, setDe] = useState(hojeMais(-29));
  const [ate, setAte] = useState(hojeMais(0));
  const [finalidade, setFinalidade] = useState('');
  const [unidadeId, setUnidadeId] = useState('');
  const unidades = useRegrasUnidades();

  const filtro = useMemo(
    () => ({ de, ate, finalidade: finalidade || undefined, unidadeId: unidadeId || undefined }),
    [de, ate, finalidade, unidadeId],
  );
  const q = useResumoDiario(filtro);
  const d = q.data;

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-5">
        <div className="flex items-center gap-2 md:col-span-2">
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} aria-label="De" />
          <span className="text-sm text-gray-400">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} aria-label="Até" />
        </div>
        <Select value={finalidade} onChange={(e) => setFinalidade(e.target.value)} aria-label="Finalidade">
          <option value="">Finalidade: todas</option>
          <option value="ConfirmacaoAgendamento">Confirmação de agendamento</option>
          <option value="ExameLiberado">Exame liberado</option>
          <option value="LaudoPronto">Laudo pronto</option>
        </Select>
        <Select value={unidadeId} onChange={(e) => setUnidadeId(e.target.value)} aria-label="Unidade" className="md:col-span-2">
          <option value="">Unidade executante: todas</option>
          {(unidades.data ?? []).map((u) => (
            <option key={u.unidadeId} value={u.unidadeId}>{u.unidadeNome}</option>
          ))}
        </Select>
      </div>

      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}

      {d ? (
        <>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:grid-cols-7">
            <Tile rotulo="Entraram" valor={d.totais.enfileiradas} />
            <Tile rotulo="Enviadas" valor={d.totais.enviadas} />
            <Tile rotulo="Entregues" valor={d.totais.entregues} />
            <Tile rotulo="Lidas" valor={d.totais.lidas} />
            <Tile rotulo="Falhas" valor={d.totais.falhas} tom="text-red-700" />
            <Tile rotulo="Retidas (identif./negado/sem celular)" valor={d.totais.retidas} tom="text-amber-700" />
            <Tile rotulo="Atendidas por pessoa" valor={d.totais.substituidasPorAtendente} />
            <Tile rotulo="Confirmaram" valor={d.totais.confirmadas} tom="text-emerald-700" />
            <Tile rotulo="Não vão" valor={d.totais.canceladas} tom="text-amber-700" />
            <Tile rotulo="Sem resposta" valor={d.totais.semResposta} />
            <Tile rotulo="Taxa de entrega" valor={d.totais.taxaEntrega} sufixo="%" />
            <Tile rotulo="Taxa de leitura" valor={d.totais.taxaLeitura} sufixo="%" />
            <Tile rotulo="Taxa de resposta" valor={d.totais.taxaResposta} sufixo="%" />
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
            <Lista titulo="Falhas por erro (Meta)" itens={d.falhasPorErro} vazio="Nenhuma falha no período." />
            <Lista titulo="Por finalidade" itens={d.porFinalidade} vazio="—" />
            <Lista titulo="Por unidade executante" itens={d.porUnidade} vazio="—" />
          </div>

          <div className="overflow-x-auto rounded-lg border border-gray-200">
            <table className="w-full min-w-[1200px] text-sm">
              <thead className="bg-gray-50 text-[11px] uppercase text-gray-500">
                <tr>
                  <th className="px-3 py-2 text-left">Dia</th>
                  {COLUNAS.map((c) => (
                    <th key={c.chave} className="px-2 py-2 text-right" title={c.dica}>{c.rotulo}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {d.dias.map((dia) => (
                  <tr key={dia.dia} className="border-t border-gray-100">
                    <td className="whitespace-nowrap px-3 py-1.5">{diaLegivel(dia.dia)}</td>
                    {COLUNAS.map((c) => {
                      const v = dia[c.chave] as number;
                      return (
                        <td key={c.chave} className={`px-2 py-1.5 text-right tabular-nums ${v === 0 ? 'text-gray-300' : c.tom ?? 'text-gray-800'}`}>
                          {nf(v)}
                        </td>
                      );
                    })}
                  </tr>
                ))}
                {d.dias.length === 0 ? (
                  <tr><td colSpan={COLUNAS.length + 1} className="px-3 py-3 text-center text-gray-500">Nada no período.</td></tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </>
      ) : q.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : null}
    </div>
  );
}

function Lista({ titulo, itens, vazio }: { titulo: string; itens: { erro: string; total: number }[]; vazio: string }) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white p-3">
      <h3 className="text-xs font-medium uppercase tracking-wide text-gray-500">{titulo}</h3>
      {itens.length === 0 ? (
        <p className="mt-2 text-sm text-gray-400">{vazio}</p>
      ) : (
        <ul className="mt-2 space-y-1 text-sm">
          {itens.slice(0, 12).map((i) => (
            <li key={i.erro} className="flex items-baseline justify-between gap-2">
              <span className="truncate text-gray-700" title={i.erro}>{i.erro}</span>
              <span className="shrink-0 tabular-nums font-medium text-gray-900">{nf(i.total)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
