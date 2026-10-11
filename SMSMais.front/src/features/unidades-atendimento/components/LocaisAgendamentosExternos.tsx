import { useState } from 'react';
import { Link2, MapPin, MapPinOff, Sparkles, Unlink } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { BotaoLinhaAcao } from '@/shared/ui/BotaoLinhaAcao';
import { SelectComBusca } from '@/shared/ui/SelectComBusca';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { useLocaisExternos, useVincularLocalExterno } from '@/features/unidades-atendimento/api/queries';
import type { LocalAgendamentoExterno, UnidadeAtendimentoListItem } from '@/features/unidades-atendimento/types';

/**
 * Os locais dos agendamentos do SER, do SERNIT e do ESUS — como cada sistema os escreve — e a
 * unidade de atendimento ligada a cada um. É daqui que sai o ENDEREÇO do aviso ao paciente: os
 * sistemas só mandam o nome ("MS HGB HOSPITAL GERAL DE BONSUCESSO").
 *
 * <p>Quem liga é uma pessoa. A sugestão poupa a busca, mas não decide: "RIO IMAGEM LAGOS" se
 * parece com o Rio Imagem do Rio de Janeiro, e o paciente iria para a cidade errada.</p>
 */
/** "2026-10-19" (dia, sem fuso) → "19/10/2026". */
function dia(iso: string): string {
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

export function LocaisAgendamentosExternos({
  unidades,
  podeEditar,
}: {
  unidades: UnidadeAtendimentoListItem[];
  podeEditar: boolean;
}) {
  const locais = useLocaisExternos();
  const vincular = useVincularLocalExterno();
  const [erro, setErro] = useState<string | null>(null);

  const opcoes = unidades
    .filter((u) => u.ativo)
    .map((u) => ({ valor: u.id, rotulo: u.nome }))
    .sort((a, b) => a.rotulo.localeCompare(b.rotulo, 'pt-BR'));

  async function ligar(nome: string, unidadeAtendimentoId: string | null) {
    setErro(null);
    try {
      await vincular.mutateAsync({ nome, unidadeAtendimentoId });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const semEndereco = (locais.data ?? []).filter((l) => !l.ligada?.temEndereco).length;

  const colunas: Coluna<LocalAgendamentoExterno>[] = [
    {
      chave: 'nome',
      cabecalho: 'Local, como o sistema escreve',
      ordenar: (l) => l.nome,
      render: (l) => (
        <div>
          <span className="font-medium text-gray-900">{l.nome}</span>
          <p className="text-xs text-gray-500">{l.sistemas.join(' · ')}</p>
        </div>
      ),
    },
    {
      chave: 'agendamentos',
      cabecalho: 'Agendamentos futuros',
      className: 'text-right',
      ordenar: (l) => l.agendamentosFuturos,
      render: (l) => (
        <div className="text-right">
          <span className="tabular-nums">{l.agendamentosFuturos}</span>
          {l.proximaData ? <p className="text-xs text-gray-500">próximo {dia(l.proximaData)}</p> : null}
        </div>
      ),
    },
    {
      chave: 'ligada',
      cabecalho: 'Endereço do aviso',
      render: (l) =>
        l.ligada ? (
          <div>
            <span className="inline-flex items-center gap-1 text-sm text-gray-900">
              <MapPin className="h-3.5 w-3.5 text-emerald-600" /> {l.ligada.nome}
            </span>
            {!l.ligada.temEndereco ? (
              <p className="text-xs text-amber-700">Unidade sem endereço cadastrado — o aviso sai só com o nome.</p>
            ) : null}
          </div>
        ) : (
          <div>
            <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800">
              <MapPinOff className="h-3 w-3" /> Sem ligação — o aviso sai sem endereço
            </span>
            {l.sugerida ? (
              <p className="mt-1 inline-flex items-center gap-1 text-xs text-gray-600">
                <Sparkles className="h-3 w-3" /> Parece: {l.sugerida.nome}
              </p>
            ) : null}
          </div>
        ),
    },
    {
      chave: 'acoes',
      cabecalho: 'Ligar a',
      render: (l) =>
        podeEditar ? (
          <div className="flex min-w-[16rem] flex-col gap-1">
            <SelectComBusca
              opcoes={opcoes}
              valor={l.ligada?.id ?? ''}
              onChange={(v) => void ligar(l.nome, v || null)}
              desabilitado={vincular.isPending}
              placeholder="Escolha a unidade de atendimento…"
            />
            <div className="flex flex-wrap gap-1">
              {!l.ligada && l.sugerida ? (
                <BotaoLinhaAcao onClick={() => void ligar(l.nome, l.sugerida!.id)} disabled={vincular.isPending}>
                  <Link2 className="h-3.5 w-3.5" /> Conferi, é esta
                </BotaoLinhaAcao>
              ) : null}
              {l.ligada ? (
                <BotaoLinhaAcao tom="perigo" onClick={() => void ligar(l.nome, null)} disabled={vincular.isPending}>
                  <Unlink className="h-3.5 w-3.5" /> Desfazer
                </BotaoLinhaAcao>
              ) : null}
            </div>
          </div>
        ) : null,
    },
  ];

  return (
    <section id="locais-externos" className="space-y-3">
      <div>
        <h2 className="text-base font-semibold text-gray-900">Locais dos agendamentos do SER, SERNIT e ESUS</h2>
        <p className="mt-1 text-sm text-gray-600">
          Quando um pedido desses sistemas é agendado, o paciente recebe pelo WhatsApp o procedimento, o dia, a hora e o
          local. O <strong>endereço</strong> sai da unidade de atendimento ligada ao nome do local — os sistemas só
          mandam o nome. Confira o endereço antes de ligar: é para lá que o paciente vai.
          {semEndereco > 0 ? (
            <>
              {' '}
              <strong className="text-amber-800">
                {semEndereco} {semEndereco === 1 ? 'local está' : 'locais estão'} saindo sem endereço.
              </strong>
            </>
          ) : null}
        </p>
      </div>
      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}
      {locais.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(locais.error)}
        </div>
      ) : null}
      <Tabela
        colunas={colunas}
        dados={locais.data ?? []}
        chaveLinha={(l) => l.nome}
        carregando={locais.isLoading}
        vazio="Nenhum agendamento futuro do SER, do SERNIT ou do ESUS por enquanto."
      />
    </section>
  );
}
