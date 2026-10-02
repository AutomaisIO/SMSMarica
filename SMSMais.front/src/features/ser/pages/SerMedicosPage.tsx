import { useEffect, useState } from 'react';
import { AlertTriangle, Link2, Link2Off, Loader2, RefreshCw, Search, Send, Stethoscope } from 'lucide-react';

import {
  useDesligarSerProfissional,
  useImportarSerProfissionais,
  useLigarSerProfissional,
  useResumoSerProfissionais,
  useSerProfissionais,
  type SerProfissional,
} from '@/features/ser/api/profissionaisApi';
import { useBuscarMedicos } from '@/features/medicos/api/queries';
import { PedidosDeCadastroMedico } from '@/features/regulacao/components/PedidosDeCadastroMedico';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarInstante } from '@/shared/lib/datas';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';

/**
 * Regulação → SER → Médicos: os profissionais como estão no SER (Cadastro → Profissionais),
 * num espelho à parte do nosso cadastro de Médicos (ADR-0065).
 *
 * <p>O cadastro do SER é ruim — medido em 01/10/2026: CPF em 4,6% dos 930, metade só com o nome.
 * Por isso nada daqui entra no nosso cadastro: a ligação "é o mesmo médico" é feita por uma
 * pessoa, nesta tela. É desta lista que sai o "Médico solicitante" das solicitações ao SER.</p>
 *
 * <p>O envio de médico novo ao SER está desenhado (ADR-0065 §Envio) e desligado: é escrita no
 * sistema do Estado e só liga com OK explícito.</p>
 */
export function SerMedicosPage() {
  const podeEditar = usePermissao('RegulacaoSer', 'Edicao');

  const [termo, setTermo] = useState('');
  const [buscado, setBuscado] = useState('');
  const [situacao, setSituacao] = useState('');
  const [ligacao, setLigacao] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState(50);
  const [ligando, setLigando] = useState<SerProfissional | null>(null);

  const resumo = useResumoSerProfissionais();
  const lista = useSerProfissionais({ termo: buscado, situacao, ligacao, pagina, tamanhoPagina: tamanho });
  const importar = useImportarSerProfissionais();
  const desligar = useDesligarSerProfissional();

  // Terminou a importação (resumo saiu de "em execução"): a lista precisa refletir o novo estado.
  const emExecucao = resumo.data?.importacaoEmExecucao ?? false;
  const refetchLista = lista.refetch;
  const [estavaRodando, setEstavaRodando] = useState(false);
  useEffect(() => {
    if (emExecucao) setEstavaRodando(true);
    else if (estavaRodando) {
      setEstavaRodando(false);
      void refetchLista();
    }
  }, [emExecucao, estavaRodando, refetchLista]);

  const r = resumo.data;

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <Stethoscope className="size-6 text-red-700" />
          <div>
            <div className="flex items-center gap-1">
              <h1 className="text-xl font-semibold text-slate-900">Médicos do SER</h1>
              <AjudaManual artigo="ser-medicos" />
            </div>
            <p className="text-sm text-slate-600">
              Como estão no SER (Cadastro → Profissionais). À parte do nosso cadastro de Médicos.
            </p>
          </div>
        </div>
        {podeEditar ? (
          <Button
            variante="outline"
            disabled={emExecucao || importar.isPending}
            onClick={async () => {
              try {
                await importar.mutateAsync();
                notificar('Importação do SER iniciada.');
              } catch {
                // o interceptor já mostrou o motivo (ex.: 409, já em andamento)
              }
            }}
          >
            {emExecucao ? <Loader2 className="mr-1 size-4 animate-spin" /> : <RefreshCw className="mr-1 size-4" />}
            {emExecucao ? 'Lendo o SER…' : 'Importar do SER'}
          </Button>
        ) : null}
      </header>

      {r ? (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
          <Cartao rotulo="No SER" valor={r.total} />
          <Cartao rotulo="Ativos" valor={r.ativos} />
          <Cartao rotulo="Com CPF no SER" valor={r.comCpf} />
          <Cartao rotulo="Ligados a médico nosso" valor={r.ligados} />
          <Cartao rotulo="Saíram do SER" valor={r.foraDoSer} />
        </div>
      ) : null}

      {r?.ultimoErro ? (
        <p className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" />
          A última importação falhou: {r.ultimoErro}
        </p>
      ) : null}

      {r && r.total === 0 && !emExecucao ? (
        <p className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-600">
          Nada importado ainda. Use “Importar do SER” — a leitura leva um ou dois minutos.
        </p>
      ) : null}

      <form
        className="flex flex-wrap items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          setPagina(1);
          setBuscado(termo.trim());
        }}
      >
        <Input
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
          placeholder="Nome, CPF ou documento"
          className="w-72"
        />
        <Select value={situacao} onChange={(e) => { setSituacao(e.target.value); setPagina(1); }} className="w-44">
          <option value="">Todos no SER</option>
          <option value="ativos">Ativos</option>
          <option value="inativos">Inativos</option>
          <option value="fora">Saíram do SER</option>
        </Select>
        <Select value={ligacao} onChange={(e) => { setLigacao(e.target.value); setPagina(1); }} className="w-52">
          <option value="">Ligados ou não</option>
          <option value="ligados">Ligados a médico nosso</option>
          <option value="soltos">Sem ligação</option>
        </Select>
        <Button type="submit" variante="outline">
          <Search className="mr-1 size-4" /> Buscar
        </Button>
      </form>

      {/* Enquanto a página nova não chega, a anterior continua na tela (placeholderData) — sem
          este aviso, quem pagina fica sem saber se o clique pegou. */}
      <div className="relative overflow-x-auto rounded-md border border-slate-200 bg-white">
        {lista.isFetching ? (
          <div className="absolute inset-x-0 top-0 z-10 flex justify-center pt-10">
            <span className="flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-600 shadow">
              <Loader2 className="size-4 animate-spin" /> Carregando…
            </span>
          </div>
        ) : null}
        <table className={`w-full text-sm transition-opacity ${lista.isFetching ? 'opacity-50' : ''}`}>
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-3 py-2">Nome no SER</th>
              <th className="px-3 py-2">CPF</th>
              <th className="px-3 py-2">Documento</th>
              <th className="px-3 py-2">Situação</th>
              <th className="px-3 py-2">Médico nosso</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {(lista.data?.itens ?? []).map((p) => (
              <tr key={p.id}>
                <td className="px-3 py-2 text-slate-900">
                  {p.nome}
                  {p.ocorrencias > 1 ? (
                    <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] text-amber-800" title="O SER tem este cadastro repetido">
                      {p.ocorrencias}× no SER
                    </span>
                  ) : null}
                </td>
                <td className="px-3 py-2 font-mono text-xs text-slate-600">{p.cpf ?? '—'}</td>
                <td className="px-3 py-2 text-xs text-slate-600">
                  {p.documento ? `${p.tipoDocumento ?? ''} ${p.documento}`.trim() : '—'}
                </td>
                <td className="px-3 py-2 text-xs">
                  {!p.presenteNoSer ? (
                    <span className="rounded bg-slate-100 px-1.5 py-0.5 text-slate-600">Saiu do SER</span>
                  ) : p.ativo ? (
                    <span className="rounded bg-emerald-100 px-1.5 py-0.5 text-emerald-700">Ativo</span>
                  ) : (
                    <span className="rounded bg-slate-100 px-1.5 py-0.5 text-slate-600">Inativo</span>
                  )}
                </td>
                <td className="px-3 py-2 text-slate-800">{p.medicoNome ?? <span className="text-slate-400">—</span>}</td>
                <td className="px-3 py-2 text-right">
                  {podeEditar ? (
                    p.medicoId ? (
                      <Button
                        variante="ghost"
                        tamanho="sm"
                        disabled={desligar.isPending}
                        onClick={() => desligar.mutate(p.id)}
                      >
                        <Link2Off className="mr-1 size-3.5" /> Desligar
                      </Button>
                    ) : (
                      <Button variante="ghost" tamanho="sm" onClick={() => setLigando(p)}>
                        <Link2 className="mr-1 size-3.5" /> Ligar a médico nosso
                      </Button>
                    )
                  ) : null}
                </td>
              </tr>
            ))}
            {lista.data && lista.data.itens.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-3 py-6 text-center text-slate-500">Nenhum profissional com esses filtros.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      <Paginacao
        pagina={pagina}
        tamanho={tamanho}
        total={lista.data?.total ?? 0}
        aoMudarPagina={setPagina}
        aoMudarTamanho={(t) => { setTamanho(t); setPagina(1); }}
      />

      {r?.ultimaLeituraEm ? (
        <p className="text-xs text-slate-500">Última leitura do SER: {formatarInstante(r.ultimaLeituraEm)}.</p>
      ) : null}

      <div className="flex items-start gap-2 rounded-md border border-slate-200 bg-slate-50 p-3 text-xs text-slate-600">
        <Send className="mt-0.5 size-4 shrink-0 text-slate-400" />
        <p>
          Enviar ao SER um médico que só existe no nosso cadastro ainda não está liberado: é escrita
          no sistema do Estado e precisa de autorização antes do primeiro envio.
        </p>
      </div>

      {/* Médicos que as unidades pediram e ainda não estão no SER — quem cadastra é a regulação. */}
      <div className="border-t border-slate-200 pt-4">
        <PedidosDeCadastroMedico sistema="Ser" />
      </div>

      <ModalLigar profissional={ligando} aoFechar={() => setLigando(null)} />
    </div>
  );
}

function Cartao({ rotulo, valor }: { rotulo: string; valor: number }) {
  return (
    <div className="rounded-md border border-slate-200 bg-white px-3 py-2">
      <p className="text-xs text-slate-500">{rotulo}</p>
      <p className="text-lg font-semibold text-slate-900">{valor.toLocaleString('pt-BR')}</p>
    </div>
  );
}

/**
 * "É o mesmo médico": a busca abre com o nome do SER, e quem decide é a pessoa — o SER tem
 * homônimos e nome abreviado em 1 de cada 5 cadastros.
 */
function ModalLigar({ profissional, aoFechar }: { profissional: SerProfissional | null; aoFechar: () => void }) {
  const [termo, setTermo] = useState('');
  const ligar = useLigarSerProfissional();

  useEffect(() => {
    setTermo(profissional?.cpf ?? profissional?.nome ?? '');
  }, [profissional]);

  const busca = useBuscarMedicos(profissional ? termo : '');

  return (
    <Modal
      aberto={!!profissional}
      aoFechar={aoFechar}
      titulo="Ligar a médico nosso"
      descricao={profissional ? `No SER: ${profissional.nome}${profissional.cpf ? ` — CPF ${profissional.cpf}` : ''}${profissional.documento ? ` — ${profissional.tipoDocumento ?? ''} ${profissional.documento}` : ''}` : undefined}
    >
      <div className="space-y-3">
        <Input value={termo} onChange={(e) => setTermo(e.target.value)} placeholder="Nome ou CPF do médico no nosso cadastro" autoFocus />
        <ul className="max-h-80 divide-y divide-slate-100 overflow-y-auto rounded-md border border-slate-200">
          {busca.isFetching && !busca.data ? (
            <li className="px-3 py-4 text-center text-sm text-slate-500">Procurando…</li>
          ) : null}
          {(busca.data ?? []).map((m) => (
            <li key={m.id} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
              <span>
                <span className="block text-slate-900">{m.nomeCompleto}</span>
                <span className="block text-xs text-slate-500">
                  CPF {m.cpf} · {m.conselho} {m.registro}{m.ufConselho ? `/${m.ufConselho}` : ''}
                  {m.especialidade ? ` · ${m.especialidade}` : ''}
                </span>
              </span>
              <Button
                tamanho="sm"
                disabled={ligar.isPending}
                onClick={async () => {
                  if (!profissional) return;
                  try {
                    await ligar.mutateAsync({ id: profissional.id, medicoId: m.id });
                    notificar(`Ligado a ${m.nomeCompleto}.`);
                    aoFechar();
                  } catch {
                    // o interceptor já mostrou o erro
                  }
                }}
              >
                É este
              </Button>
            </li>
          ))}
          {busca.data && busca.data.length === 0 ? (
            <li className="px-3 py-4 text-center text-sm text-slate-500">
              Nenhum médico nosso com esse termo. Tente só o sobrenome, ou o CPF.
            </li>
          ) : null}
        </ul>
      </div>
    </Modal>
  );
}
