import { useMemo, useState } from 'react';
import { ChevronRight, Loader2, Pencil, Plus, Tags } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Select } from '@/shared/ui/Select';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabs } from '@/shared/ui/Tabs';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { useAssuntos, useMarcadores, useSalvarAssunto, useSalvarMarcador } from '@/features/ouvidoria/api/queries';
import type { AssuntoDto, MarcadorDto } from '@/features/ouvidoria/types';

/** Catálogo da ouvidoria: assuntos em dois níveis (com código OuvidorSUS) e marcadores livres. */
export function AssuntosPage() {
  return (
    <div className="space-y-4">
      <div>
        <div className="flex items-center gap-1.5">
          <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
            <Tags className="h-5 w-5 text-red-600" aria-hidden="true" />
            Assuntos e marcadores
          </h1>
          <AjudaManual artigo="ouvidoria" secao="gestao" />
        </div>
        <p className="text-sm text-slate-500">Como as manifestações são classificadas. Assunto tem dois níveis; marcador é etiqueta livre.</p>
      </div>
      <Tabs
        abas={[
          { id: 'assuntos', rotulo: 'Assuntos', conteudo: <AbaAssuntos /> },
          { id: 'marcadores', rotulo: 'Marcadores', conteudo: <AbaMarcadores /> },
        ]}
      />
    </div>
  );
}

// ---- Assuntos ----

function AbaAssuntos() {
  const podeCriar = usePermissao('OuvidoriaGestao', 'Inclusao');
  const podeEditar = usePermissao('OuvidoriaGestao', 'Edicao');
  const { data: assuntos = [], isLoading, isError, error } = useAssuntos();
  const [mostrarInativos, setMostrarInativos] = useState(false);
  const [editando, setEditando] = useState<AssuntoDto | { paiId: string | null } | null>(null);

  const arvore = useMemo(() => {
    const ordenar = (x: AssuntoDto, y: AssuntoDto) => x.ordem - y.ordem || x.nome.localeCompare(y.nome, 'pt-BR');
    const visiveis = assuntos.filter((a) => mostrarInativos || a.ativo);
    return visiveis
      .filter((a) => !a.paiId)
      .sort(ordenar)
      .map((pai) => ({ pai, filhos: visiveis.filter((f) => f.paiId === pai.id).sort(ordenar) }));
  }, [assuntos, mostrarInativos]);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={mostrarInativos} onChange={(e) => setMostrarInativos(e.target.checked)} />
          Mostrar inativos
        </label>
        {podeCriar ? (
          <Button tamanho="sm" onClick={() => setEditando({ paiId: null })}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Novo assunto
          </Button>
        ) : null}
      </div>

      {isError ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(error)}
        </p>
      ) : null}
      {isLoading ? <p className="text-sm text-slate-500">Carregando…</p> : null}
      {!isLoading && arvore.length === 0 ? <p className="py-6 text-center text-sm text-slate-400">Nenhum assunto cadastrado.</p> : null}

      <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
        {arvore.map(({ pai, filhos }) => (
          <li key={pai.id} className="p-3">
            <div className="flex items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <span className="font-medium text-slate-800">{pai.nome}</span>
                {pai.codigoOuvidorSus ? <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[11px] text-slate-600">OuvidorSUS {pai.codigoOuvidorSus}</span> : null}
                <StatusBadge ativo={pai.ativo} />
              </div>
              {podeEditar ? (
                <div className="flex items-center gap-1">
                  <Button variante="ghost" tamanho="sm" onClick={() => setEditando({ paiId: pai.id })} title="Adicionar subassunto">
                    <Plus className="h-4 w-4" aria-hidden="true" /> Subassunto
                  </Button>
                  <Button variante="ghost" tamanho="sm" onClick={() => setEditando(pai)} aria-label={`Editar ${pai.nome}`}>
                    <Pencil className="h-4 w-4" aria-hidden="true" />
                  </Button>
                </div>
              ) : null}
            </div>
            {filhos.length > 0 ? (
              <ul className="mt-2 space-y-1 pl-4">
                {filhos.map((f) => (
                  <li key={f.id} className="flex items-center justify-between gap-2 text-sm">
                    <div className="flex items-center gap-2">
                      <ChevronRight className="h-3.5 w-3.5 text-slate-300" aria-hidden="true" />
                      <span className="text-slate-700">{f.nome}</span>
                      {f.codigoOuvidorSus ? <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[11px] text-slate-600">{f.codigoOuvidorSus}</span> : null}
                      {!f.ativo ? <StatusBadge ativo={false} /> : null}
                    </div>
                    {podeEditar ? (
                      <Button variante="ghost" tamanho="sm" onClick={() => setEditando(f)} aria-label={`Editar ${f.nome}`}>
                        <Pencil className="h-4 w-4" aria-hidden="true" />
                      </Button>
                    ) : null}
                  </li>
                ))}
              </ul>
            ) : null}
          </li>
        ))}
      </ul>

      {editando ? (
        <ModalAssunto
          assunto={'id' in editando ? editando : null}
          paiIdInicial={editando.paiId}
          raizes={assuntos.filter((a) => !a.paiId)}
          temFilhos={'id' in editando && assuntos.some((a) => a.paiId === editando.id)}
          aoFechar={() => setEditando(null)}
        />
      ) : null}
    </div>
  );
}

function ModalAssunto({
  assunto,
  paiIdInicial,
  raizes,
  temFilhos,
  aoFechar,
}: {
  assunto: AssuntoDto | null;
  paiIdInicial: string | null;
  raizes: AssuntoDto[];
  /** Assunto com subassuntos não pode virar subassunto (só dois níveis). */
  temFilhos: boolean;
  aoFechar: () => void;
}) {
  const salvar = useSalvarAssunto();
  const [nome, setNome] = useState(assunto?.nome ?? '');
  const [paiId, setPaiId] = useState<string>(assunto?.paiId ?? paiIdInicial ?? '');
  const [codigo, setCodigo] = useState(assunto?.codigoOuvidorSus ?? '');
  const [ordem, setOrdem] = useState(String(assunto?.ordem ?? 0));
  const [ativo, setAtivo] = useState(assunto?.ativo ?? true);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!nome.trim()) return setErro('Informe o nome.');
    try {
      await salvar.mutateAsync({
        id: assunto?.id ?? null,
        payload: { paiId: paiId || null, nome: nome.trim(), codigoOuvidorSus: codigo.trim() || null, ordem: Number(ordem) || 0, ativo },
      });
      notificar(assunto ? 'Assunto atualizado.' : 'Assunto criado.', 'sucesso');
      aoFechar();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo={assunto ? 'Editar assunto' : paiIdInicial ? 'Novo subassunto' : 'Novo assunto'} largura="sm">
      <form onSubmit={enviar} className="space-y-4">
        <Campo label="Assunto pai" htmlFor="as-pai" dica={temFilhos ? "Tem subassuntos, então fica no primeiro nível." : "Em branco = assunto de primeiro nível."}>
          <Select id="as-pai" value={paiId} onChange={(e) => setPaiId(e.target.value)} disabled={temFilhos}>
            <option value="">— (primeiro nível)</option>
            {raizes
              .filter((r) => r.id !== assunto?.id)
              .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'))
              .map((r) => (
                <option key={r.id} value={r.id}>
                  {r.nome}
                </option>
              ))}
          </Select>
        </Campo>
        <Campo label="Nome" htmlFor="as-nome" required>
          <Input id="as-nome" value={nome} onChange={(e) => setNome(e.target.value)} maxLength={200} autoFocus />
        </Campo>
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="Código OuvidorSUS" htmlFor="as-codigo" dica="Opcional; para exportação futura.">
            <Input id="as-codigo" value={codigo} onChange={(e) => setCodigo(e.target.value)} maxLength={20} />
          </Campo>
          <Campo label="Ordem" htmlFor="as-ordem" dica="Menor aparece primeiro.">
            <Input id="as-ordem" type="number" min={0} value={ordem} onChange={(e) => setOrdem(e.target.value)} />
          </Campo>
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Ativo (aparece no registro)
        </label>
        {erro ? (
          <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </p>
        ) : null}
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variante="ghost" onClick={aoFechar} disabled={salvar.isPending}>
            Cancelar
          </Button>
          <Button type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : null}
            {salvar.isPending ? 'Salvando…' : 'Salvar'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

// ---- Marcadores ----

function AbaMarcadores() {
  const podeCriar = usePermissao('OuvidoriaGestao', 'Inclusao');
  const podeEditar = usePermissao('OuvidoriaGestao', 'Edicao');
  const { data: marcadores = [], isLoading, isError, error } = useMarcadores();
  const [editando, setEditando] = useState<MarcadorDto | 'novo' | null>(null);

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-slate-500">Etiquetas livres para agrupar manifestações (ex.: &quot;Imprensa&quot;, &quot;Conselho de Saúde&quot;).</p>
        {podeCriar ? (
          <Button tamanho="sm" onClick={() => setEditando('novo')}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Novo marcador
          </Button>
        ) : null}
      </div>
      {isError ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(error)}
        </p>
      ) : null}
      {isLoading ? <p className="text-sm text-slate-500">Carregando…</p> : null}
      {!isLoading && marcadores.length === 0 ? <p className="py-6 text-center text-sm text-slate-400">Nenhum marcador.</p> : null}
      <ul className="flex flex-wrap gap-2">
        {[...marcadores]
          .sort((a, b) => Number(b.ativo) - Number(a.ativo) || a.nome.localeCompare(b.nome, 'pt-BR'))
          .map((mk) => (
            <li key={mk.id}>
              <button
                type="button"
                disabled={!podeEditar}
                onClick={() => setEditando(mk)}
                className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-sm ${
                  mk.ativo ? 'border-slate-300 bg-white text-slate-800 hover:bg-slate-50' : 'border-slate-200 bg-slate-50 text-slate-400 line-through'
                } disabled:cursor-default`}
                title={podeEditar ? 'Editar marcador' : undefined}
              >
                {mk.nome}
                {podeEditar ? <Pencil className="h-3 w-3 text-slate-400" aria-hidden="true" /> : null}
              </button>
            </li>
          ))}
      </ul>
      {editando ? <ModalMarcador marcador={editando === 'novo' ? null : editando} aoFechar={() => setEditando(null)} /> : null}
    </div>
  );
}

function ModalMarcador({ marcador, aoFechar }: { marcador: MarcadorDto | null; aoFechar: () => void }) {
  const salvar = useSalvarMarcador();
  const [nome, setNome] = useState(marcador?.nome ?? '');
  const [ativo, setAtivo] = useState(marcador?.ativo ?? true);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!nome.trim()) return setErro('Informe o nome.');
    try {
      await salvar.mutateAsync({ id: marcador?.id ?? null, payload: { nome: nome.trim(), ativo } });
      notificar(marcador ? 'Marcador atualizado.' : 'Marcador criado.', 'sucesso');
      aoFechar();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo={marcador ? 'Editar marcador' : 'Novo marcador'} largura="sm">
      <form onSubmit={enviar} className="space-y-4">
        <Campo label="Nome" htmlFor="mk-nome" required>
          <Input id="mk-nome" value={nome} onChange={(e) => setNome(e.target.value)} maxLength={80} autoFocus />
        </Campo>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Ativo
        </label>
        {erro ? (
          <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </p>
        ) : null}
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variante="ghost" onClick={aoFechar} disabled={salvar.isPending}>
            Cancelar
          </Button>
          <Button type="submit" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : 'Salvar'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
