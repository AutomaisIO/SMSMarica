import { useState } from 'react';
import { Loader2, Phone, Users } from 'lucide-react';
import { useFilaDaOferta, useStatusFila } from '../api/queries';
import type { FilaCargaStatus, OrdemDaFila, PessoaNaFila } from '../types';

const ORDENS: { valor: OrdemDaFila; rotulo: string; dica: string }[] = [
  { valor: 'espera', rotulo: 'Quem espera há mais tempo', dica: 'Ordem de chegada — o critério mais defensável numa fila pública.' },
  { valor: 'risco', rotulo: 'Risco', dica: 'Vermelho primeiro; dentro do mesmo risco, quem espera há mais tempo.' },
  { valor: 'idade', rotulo: 'Idade', dica: 'Mais velho primeiro.' },
  { valor: 'nome', rotulo: 'Nome', dica: 'Alfabética — para procurar alguém específico.' },
];

const RISCO = [
  { rotulo: 'Vermelho', classe: 'bg-red-100 text-red-800 ring-red-300' },
  { rotulo: 'Amarelo', classe: 'bg-amber-100 text-amber-800 ring-amber-300' },
  { rotulo: 'Verde', classe: 'bg-emerald-100 text-emerald-800 ring-emerald-300' },
  { rotulo: 'Azul', classe: 'bg-blue-100 text-blue-800 ring-blue-300' },
];

function Risco({ n }: { n: number | null }) {
  if (n === null || n < 0 || n >= RISCO.length) {
    return <span className="text-xs text-gray-400">—</span>;
  }
  const r = RISCO[n];
  return (
    <span className={`inline-block rounded-full px-2 py-0.5 text-[11px] ring-1 ${r.classe}`}>
      {r.rotulo}
    </span>
  );
}

function dataCurta(iso: string | null) {
  if (!iso) return '—';
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

function dataHora(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Espera em texto humano: acima de um ano, "1a 3m" diz mais que "462 dias". */
function espera(dias: number | null) {
  if (dias === null) return '—';
  if (dias < 60) return `${dias} d`;
  if (dias < 365) return `${Math.floor(dias / 30)} meses`;
  const anos = Math.floor(dias / 365);
  const meses = Math.floor((dias % 365) / 30);
  return meses > 0 ? `${anos}a ${meses}m` : `${anos} ano${anos > 1 ? 's' : ''}`;
}

function Linha({ p, i }: { p: PessoaNaFila; i: number }) {
  const antiga = (p.esperandoHaDias ?? 0) >= 365;
  return (
    <tr className={i % 2 ? 'bg-gray-50/60' : ''}>
      <td className="py-1.5 pr-2 text-right text-xs text-gray-400">{i + 1}</td>
      <td className="py-1.5 pr-3">
        <p className="text-sm text-gray-900">{p.nome ?? '—'}</p>
        <p className="text-[11px] text-gray-500">
          {p.idadeAnos !== null ? `${p.idadeAnos} anos` : '—'}
          {p.unidadeSolicitante ? ` · ${p.unidadeSolicitante}` : ''}
        </p>
      </td>
      <td className="py-1.5 pr-3 whitespace-nowrap">
        <span className={antiga ? 'text-sm font-semibold text-red-700' : 'text-sm text-gray-700'}>
          {espera(p.esperandoHaDias)}
        </span>
        <p className="text-[11px] text-gray-500">desde {dataCurta(p.dataSolicitacao)}</p>
      </td>
      <td className="py-1.5 pr-3"><Risco n={p.risco} /></td>
      <td className="py-1.5 pr-3 whitespace-nowrap text-xs text-gray-600">
        {p.telefone ? (
          <span className="inline-flex items-center gap-1">
            <Phone className="h-3 w-3 text-gray-400" />
            {p.telefone}
          </span>
        ) : (
          <span className="text-gray-400">sem telefone</span>
        )}
      </td>
      <td className="py-1.5 pr-3 text-xs text-gray-500">{p.cidCodigo ?? '—'}</td>
    </tr>
  );
}

/**
 * De onde vem a lista e se ela está completa. Sem isto, fila nunca lida aparecia como "ninguém
 * esperando" — foi o que a tela mostrou até 10/09/2026, com a tabela vazia em produção.
 *
 * <p>Só mostra: disparar a leitura é na Configuração do SISREG (seção “Fila de espera”), porque
 * gasta requisições do SISREG com o operador da integração.</p>
 */
function SituacaoDaLeitura({ status }: { status: FilaCargaStatus | undefined }) {
  if (!status) return null;

  return (
    <div className="mb-3 rounded-lg border border-gray-200 bg-gray-50 p-3 text-xs text-gray-700">
      {status.emExecucao ? (
        <p className="flex items-center gap-2 text-blue-800">
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
          Lendo a fila do SISREG: {status.janelasLidas} de {status.janelasTotal} período(s)
          {status.janelaAtualInicio
            ? ` — agora ${dataCurta(status.janelaAtualInicio)} a ${dataCurta(status.janelaAtualFim)}`
            : ''}
          . A lista abaixo cresce sozinha.
        </p>
      ) : status.ultimaLeitura ? (
        <p>
          Fila lida do SISREG em <strong>{dataHora(status.ultimaLeitura)}</strong> —{' '}
          {status.pessoasNaFila.toLocaleString('pt-BR')} pessoa(s) esperando na rede toda. É relida
          inteira todo dia, de madrugada; quem vira agendamento sai sozinho.
        </p>
      ) : (
        <p>
          <strong>A fila ainda não foi lida do SISREG.</strong> Enquanto isso, esta lista fica vazia
          — não quer dizer que ninguém espera.
        </p>
      )}

      {status.relidasAposFalha > 0 && !status.ultimoErro ? (
        <p className="mt-1 text-gray-500">
          {status.relidasAposFalha} período(s) precisaram ser relidos (a sessão do SISREG caiu) —
          resolvido na nova tentativa.
        </p>
      ) : null}
      {status.ultimoErro ? (
        <p className="mt-1 text-red-700">
          Leitura interrompida ({dataHora(status.ultimoErroEm)}): {status.ultimoErro}
        </p>
      ) : null}
    </div>
  );
}

/**
 * Quem está esperando pelo procedimento de uma oferta.
 *
 * <p>A oferta diz "abriram 4 vagas de eco"; isto diz quem chamar. A ordem padrão é a de chegada —
 * qualquer outra exige alguém escolhendo, e a escolha fica visível.</p>
 */
export function FilaDaOfertaPainel({
  procedimento,
  codigo,
}: {
  procedimento: string;
  codigo: string | null;
}) {
  const [ordem, setOrdem] = useState<OrdemDaFila>('espera');
  const status = useStatusFila();
  const { data, isLoading, isError } = useFilaDaOferta(
    procedimento,
    ordem,
    codigo,
    status.data?.emExecucao ?? false,
  );

  const dica = ORDENS.find((o) => o.valor === ordem)?.dica;
  const outros = data?.procedimentosIncluidos.filter((n) => n !== procedimento) ?? [];

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <header className="mb-3 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <Users className="h-4 w-4 text-primary-600" />
            Quem espera
          </h2>
          {data ? (
            <p className="mt-1 text-xs text-gray-600">
              <strong>{data.total}</strong> pessoa(s) na fila
              {data.esperaP50Dias !== null ? ` · metade espera há mais de ${espera(data.esperaP50Dias)}` : ''}
              {data.esperaMaxDias !== null ? ` · a mais antiga há ${espera(data.esperaMaxDias)}` : ''}
            </p>
          ) : null}
          {outros.length > 0 ? (
            <p className="mt-0.5 text-[11px] text-gray-500" title={outros.join('\n')}>
              Inclui quem pediu {outros.length === 1 ? `“${outros[0]}”` : `${outros.length} nomes do mesmo grupo`}
              {' '}— esta vaga também serve para eles.
            </p>
          ) : null}
        </div>

        <label className="text-xs text-gray-700">
          Ordenar por{' '}
          <select
            className="rounded-md border border-gray-300 px-2 py-1 text-xs"
            value={ordem}
            onChange={(e) => setOrdem(e.target.value as OrdemDaFila)}
          >
            {ORDENS.map((o) => (
              <option key={o.valor} value={o.valor}>
                {o.rotulo}
              </option>
            ))}
          </select>
        </label>
      </header>

      <SituacaoDaLeitura status={status.data} />

      {dica ? <p className="mb-2 text-[11px] text-gray-500">{dica}</p> : null}

      {data && Object.keys(data.porRisco).length > 0 ? (
        <div className="mb-3 flex flex-wrap gap-1.5">
          {RISCO.map((r, i) =>
            data.porRisco[String(i)] ? (
              <span key={i} className={`rounded-full px-2 py-0.5 text-[11px] ring-1 ${r.classe}`}>
                {data.porRisco[String(i)]} {r.rotulo.toLowerCase()}
              </span>
            ) : null,
          )}
          {data.porRisco.sem ? (
            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-600 ring-1 ring-gray-300">
              {data.porRisco.sem} sem classificação
            </span>
          ) : null}
        </div>
      ) : null}

      {isLoading ? (
        <p className="flex items-center gap-2 py-6 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" />
          Carregando a fila…
        </p>
      ) : null}

      {isError ? (
        <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">
          Não foi possível carregar a fila.
        </p>
      ) : null}

      {data && data.total === 0 && status.data?.ultimaLeitura ? (
        <p className="rounded-lg border border-dashed border-gray-200 p-6 text-center text-sm text-gray-500">
          Ninguém esperando por este procedimento na última leitura do SISREG.
        </p>
      ) : null}

      {data && data.pessoas.length > 0 ? (
        <>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left">
              <thead>
                <tr className="border-b border-gray-200 text-xs text-gray-500">
                  <th className="py-1 pr-2 text-right font-medium">#</th>
                  <th className="py-1 pr-3 font-medium">Paciente</th>
                  <th className="py-1 pr-3 font-medium">Espera</th>
                  <th className="py-1 pr-3 font-medium">Risco</th>
                  <th className="py-1 pr-3 font-medium">Telefone</th>
                  <th className="py-1 pr-3 font-medium">CID</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.pessoas.map((p, i) => (
                  <Linha key={p.codigoSolicitacao} p={p} i={i} />
                ))}
              </tbody>
            </table>
          </div>
          {data.total > data.pessoas.length ? (
            <p className="mt-2 text-xs text-gray-500">
              Mostrando {data.pessoas.length} de {data.total}. Refine pela ordenação para ver outro
              recorte.
            </p>
          ) : null}
        </>
      ) : null}

      <p className="mt-3 border-t border-gray-100 pt-2 text-[11px] text-gray-500">
        Esta lista vem do SISREG (pendentes e reenviadas na regulação) e é o retrato da última
        leitura. <strong>Agendar continua sendo no SISREG</strong> — aqui se decide quem chamar.
      </p>
    </section>
  );
}
