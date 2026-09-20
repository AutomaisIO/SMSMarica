import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Megaphone, Plus } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { useManifestacoes, useResumoOuvidoria } from '@/features/ouvidoria/api/queries';
import { SeletorPontoResposta, SeletorUnidade } from '@/features/ouvidoria/components/Seletores';
import { TabelaManifestacoes } from '@/features/ouvidoria/components/TabelaManifestacoes';
import { PRIORIDADES, ROTULO_PRIORIDADE, ROTULO_TIPO, TIPOS } from '@/features/ouvidoria/lib/rotulos';
import type { FiltroManifestacoes, OuvidoriaPrioridade, OuvidoriaStatus, OuvidoriaTipo } from '@/features/ouvidoria/types';

type AbaId = 'triagem' | 'andamento' | 'validacao' | 'atrasadas' | 'recurso' | 'concluidas';

const ABAS: { id: AbaId; rotulo: string; status?: OuvidoriaStatus[]; atrasadas?: boolean }[] = [
  { id: 'triagem', rotulo: 'Triagem', status: ['Registrada', 'EmTriagem'] },
  { id: 'andamento', rotulo: 'Em andamento', status: ['Encaminhada', 'AguardandoComplementacao', 'RespondidaPelaArea'] },
  { id: 'validacao', rotulo: 'Aguardando validação', status: ['RespondidaPelaArea', 'EmValidacao'] },
  { id: 'atrasadas', rotulo: 'Atrasadas', atrasadas: true },
  { id: 'recurso', rotulo: 'Recurso', status: ['EmRecurso'] },
  { id: 'concluidas', rotulo: 'Concluídas', status: ['Respondida', 'Concluida', 'Arquivada', 'EncaminhadaOutroOrgao'] },
];

const TAMANHOS = [25, 50, 100] as const;

/** Fila da ouvidoria central: abas por etapa, filtros, tabela paginada no servidor. */
export function OuvidoriaFilaPage() {
  const navigate = useNavigate();
  const podeRegistrar = usePermissao('Ouvidoria', 'Inclusao');
  const [aba, setAba] = useState<AbaId>('triagem');
  const [tipo, setTipo] = useState<OuvidoriaTipo | ''>('');
  const [unidadeId, setUnidadeId] = useState('');
  const [pontoRespostaId, setPontoRespostaId] = useState('');
  const [prioridade, setPrioridade] = useState<OuvidoriaPrioridade | ''>('');
  const [busca, setBusca] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(TAMANHOS[0]);

  const { data: resumo } = useResumoOuvidoria();

  const filtro = useMemo<FiltroManifestacoes>(() => {
    const def = ABAS.find((a) => a.id === aba)!;
    return {
      status: def.status,
      atrasadas: def.atrasadas,
      tipo,
      unidadeId,
      pontoRespostaId,
      prioridade,
      busca,
      pagina,
      tamanho,
    };
  }, [aba, tipo, unidadeId, pontoRespostaId, prioridade, busca, pagina, tamanho]);

  const lista = useManifestacoes(filtro);

  function trocarAba(id: string) {
    setAba(id as AbaId);
    setPagina(1);
  }

  const contadores: Partial<Record<AbaId, number>> = resumo
    ? {
        triagem: resumo.registradas + resumo.emTriagem,
        andamento: resumo.encaminhadas + resumo.aguardandoComplementacao,
        validacao: resumo.aguardandoValidacao,
        atrasadas: resumo.atrasadas,
        recurso: resumo.emRecurso,
      }
    : {};

  const conteudo = (
    <div className="space-y-3">
      {lista.isError ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(lista.error)}
        </p>
      ) : null}
      <TabelaManifestacoes
        dados={lista.data?.itens ?? []}
        carregando={lista.isLoading}
        aoAbrir={(m) => navigate(`/app/ouvidoria/${m.id}`)}
        vazio={aba === 'atrasadas' ? 'Nenhuma manifestação atrasada. Ótimo.' : undefined}
      />
      <Paginacao
        pagina={pagina}
        tamanho={tamanho}
        total={lista.data?.total ?? 0}
        aoMudarPagina={setPagina}
        aoMudarTamanho={(t) => {
          setTamanho(t);
          setPagina(1);
        }}
        tamanhos={TAMANHOS}
      />
    </div>
  );

  const abas: Aba[] = ABAS.map((a) => ({
    id: a.id,
    rotulo: a.rotulo,
    badge: contadores[a.id],
    conteudo,
  }));

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
            <Megaphone className="h-5 w-5 text-red-600" aria-hidden="true" />
            Ouvidoria
          </h1>
          <p className="text-sm text-slate-500">Manifestações dos cidadãos: triagem, encaminhamento, resposta e prazos.</p>
        </div>
        {podeRegistrar ? (
          <Button onClick={() => navigate('/app/ouvidoria/registrar')}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Registrar manifestação
          </Button>
        ) : null}
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-64">
          <label htmlFor="ouv-busca" className="sr-only">Buscar</label>
          <Input
            id="ouv-busca"
            placeholder="Protocolo, nome ou CPF…"
            value={busca}
            onChange={(e) => {
              setBusca(e.target.value);
              setPagina(1);
            }}
          />
        </div>
        <div className="w-44">
          <label htmlFor="ouv-f-tipo" className="sr-only">Tipo</label>
          <Select
            id="ouv-f-tipo"
            value={tipo}
            onChange={(e) => {
              setTipo(e.target.value as OuvidoriaTipo | '');
              setPagina(1);
            }}
          >
            <option value="">Todos os tipos</option>
            {TIPOS.map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO[t]}
              </option>
            ))}
          </Select>
        </div>
        <div className="w-40">
          <label htmlFor="ouv-f-prioridade" className="sr-only">Prioridade</label>
          <Select
            id="ouv-f-prioridade"
            value={prioridade}
            onChange={(e) => {
              setPrioridade(e.target.value as OuvidoriaPrioridade | '');
              setPagina(1);
            }}
          >
            <option value="">Toda prioridade</option>
            {PRIORIDADES.map((p) => (
              <option key={p} value={p}>
                {ROTULO_PRIORIDADE[p]}
              </option>
            ))}
          </Select>
        </div>
        <div className="w-56">
          <label htmlFor="ouv-f-unidade" className="sr-only">Unidade</label>
          <SeletorUnidade
            id="ouv-f-unidade"
            value={unidadeId}
            onChange={(v) => {
              setUnidadeId(v);
              setPagina(1);
            }}
            rotuloVazio="Todas as unidades"
          />
        </div>
        <div className="w-64">
          <label htmlFor="ouv-f-ponto" className="sr-only">Ponto de resposta</label>
          <SeletorPontoResposta
            id="ouv-f-ponto"
            value={pontoRespostaId}
            onChange={(v) => {
              setPontoRespostaId(v);
              setPagina(1);
            }}
            incluirInativos
            rotuloVazio="Todos os pontos de resposta"
          />
        </div>
      </div>

      <Tabs abas={abas} abaAtiva={aba} aoTrocarAba={trocarAba} />
    </div>
  );
}
