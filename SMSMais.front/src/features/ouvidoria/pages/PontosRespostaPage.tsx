import { useEffect, useState } from 'react';
import { Loader2, Network, Plus, Search, Star, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { Select } from '@/shared/ui/Select';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { useListarUsuarios } from '@/features/usuarios/api/queries';
import { usePontosResposta, useSalvarPontoResposta } from '@/features/ouvidoria/api/queries';
import { SeletorUnidade } from '@/features/ouvidoria/components/Seletores';
import { ROTULO_TIPO_PONTO, TIPOS_PONTO } from '@/features/ouvidoria/lib/rotulos';
import type { OuvidoriaTipoPontoResposta, PontoRespostaDto, SalvarMembroRequest } from '@/features/ouvidoria/types';

/** Cadastro dos pontos de resposta (quem responde pela unidade/área/apuração) e seus membros. */
export function PontosRespostaPage() {
  const podeCriar = usePermissao('OuvidoriaGestao', 'Inclusao');
  const podeEditar = usePermissao('OuvidoriaGestao', 'Edicao');
  const { data: pontos = [], isLoading, isError, error } = usePontosResposta();
  const [editando, setEditando] = useState<PontoRespostaDto | null | 'novo'>(null);
  const [mostrarInativos, setMostrarInativos] = useState(false);

  const lista = pontos.filter((p) => mostrarInativos || p.ativo).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));

  const colunas: Coluna<PontoRespostaDto>[] = [
    { chave: 'nome', cabecalho: 'Nome', className: 'font-medium text-slate-800', ordenar: (p) => p.nome, render: (p) => p.nome },
    { chave: 'tipo', cabecalho: 'Tipo', ordenar: (p) => p.tipo, render: (p) => ROTULO_TIPO_PONTO[p.tipo] },
    { chave: 'unidade', cabecalho: 'Unidade', className: 'text-sm text-slate-600', ordenar: (p) => p.unidadeNome, render: (p) => p.unidadeNome ?? '—' },
    { chave: 'prazo', cabecalho: 'Prazo próprio', className: 'text-sm text-slate-600', render: (p) => (p.prazoDias ? `${p.prazoDias} dias` : 'Padrão') },
    {
      chave: 'membros',
      cabecalho: 'Membros',
      className: 'text-sm text-slate-600',
      render: (p) => (
        <span title={p.membros.map((m) => `${m.nome}${m.titular ? ' (titular)' : ''}`).join(', ')}>
          {p.membros.length} {p.membros.length === 1 ? 'membro' : 'membros'}
          {p.membros.some((m) => m.titular) ? '' : p.membros.length ? ' · sem titular' : ''}
        </span>
      ),
    },
    {
      chave: 'pendentes',
      cabecalho: 'Em aberto',
      ordenar: (p) => p.pendentes,
      render: (p) =>
        p.pendentes > 0 ? (
          <span className="inline-flex rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800 ring-1 ring-inset ring-amber-600/20">{p.pendentes}</span>
        ) : (
          <span className="text-xs text-slate-400">0</span>
        ),
    },
    { chave: 'ativo', cabecalho: 'Situação', render: (p) => <StatusBadge ativo={p.ativo} /> },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
              <Network className="h-5 w-5 text-red-600" aria-hidden="true" />
              Pontos de resposta
            </h1>
            <AjudaManual artigo="ouvidoria" secao="gestao" />
          </div>
          <p className="text-sm text-slate-500">Unidades, áreas centrais e unidades apuratórias que recebem e respondem manifestações.</p>
        </div>
        {podeCriar ? (
          <Button onClick={() => setEditando('novo')}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Novo ponto
          </Button>
        ) : null}
      </div>

      <label className="flex items-center gap-2 text-sm text-slate-600">
        <input type="checkbox" checked={mostrarInativos} onChange={(e) => setMostrarInativos(e.target.checked)} />
        Mostrar inativos
      </label>

      {isError ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(error)}
        </p>
      ) : null}

      <Tabela
        colunas={colunas}
        dados={lista}
        chaveLinha={(p) => p.id}
        carregando={isLoading}
        aoClicarLinha={podeEditar ? (p) => setEditando(p) : undefined}
        dicaLinha="Editar ponto"
        vazio={<div className="py-8 text-center text-sm text-slate-400">Nenhum ponto de resposta. Sem ele, não há para onde encaminhar.</div>}
      />

      {editando ? <ModalPonto ponto={editando === 'novo' ? null : editando} aoFechar={() => setEditando(null)} /> : null}
    </div>
  );
}

// ---- Modal criar/editar ----

type MembroEdicao = SalvarMembroRequest & { nome: string };

function ModalPonto({ ponto, aoFechar }: { ponto: PontoRespostaDto | null; aoFechar: () => void }) {
  const salvar = useSalvarPontoResposta();
  const [nome, setNome] = useState(ponto?.nome ?? '');
  const [tipo, setTipo] = useState<OuvidoriaTipoPontoResposta>(ponto?.tipo ?? 'Unidade');
  const [unidadeId, setUnidadeId] = useState(ponto?.unidadeId ?? '');
  const [prazoDias, setPrazoDias] = useState(ponto?.prazoDias ? String(ponto.prazoDias) : '');
  const [ativo, setAtivo] = useState(ponto?.ativo ?? true);
  const [membros, setMembros] = useState<MembroEdicao[]>(
    (ponto?.membros ?? []).map((m) => ({ usuarioId: m.usuarioId, nome: m.nome, titular: m.titular })),
  );
  const [erro, setErro] = useState<string | null>(null);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!nome.trim()) return setErro('Informe o nome do ponto.');
    if (tipo === 'Unidade' && !unidadeId) return setErro('Ponto do tipo Unidade precisa da unidade.');
    try {
      await salvar.mutateAsync({
        id: ponto?.id ?? null,
        payload: {
          nome: nome.trim(),
          tipo,
          unidadeId: tipo === 'Unidade' ? unidadeId : unidadeId || null,
          prazoDias: prazoDias ? Number(prazoDias) : null,
          ativo,
          membros: membros.map(({ usuarioId, titular }) => ({ usuarioId, titular })),
        },
      });
      notificar(ponto ? 'Ponto atualizado.' : 'Ponto criado.', 'sucesso');
      aoFechar();
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo={ponto ? 'Editar ponto de resposta' : 'Novo ponto de resposta'} largura="lg">
      <form onSubmit={enviar} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="Nome" htmlFor="pr-nome" required>
            <Input id="pr-nome" value={nome} onChange={(e) => setNome(e.target.value)} maxLength={200} autoFocus />
          </Campo>
          <Campo label="Tipo" htmlFor="pr-tipo" required dica={tipo === 'Apuracao' ? 'Recebe denúncias habilitadas, na versão pseudonimizada.' : undefined}>
            <Select id="pr-tipo" value={tipo} onChange={(e) => setTipo(e.target.value as OuvidoriaTipoPontoResposta)}>
              {TIPOS_PONTO.map((t) => (
                <option key={t} value={t}>
                  {ROTULO_TIPO_PONTO[t]}
                </option>
              ))}
            </Select>
          </Campo>
          <Campo label="Unidade" htmlFor="pr-unidade" required={tipo === 'Unidade'} dica={tipo === 'Unidade' ? 'Uma unidade só pode ter um ponto.' : 'Opcional.'}>
            <SeletorUnidade id="pr-unidade" value={unidadeId} onChange={setUnidadeId} rotuloVazio={tipo === 'Unidade' ? 'Selecione' : 'Nenhuma'} />
          </Campo>
          <Campo label="Prazo próprio (dias)" htmlFor="pr-prazo" dica="Em branco: usa a configuração geral por prioridade.">
            <Input id="pr-prazo" type="number" min={1} max={90} value={prazoDias} onChange={(e) => setPrazoDias(e.target.value)} />
          </Campo>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />
          Ativo (aparece para encaminhamento)
        </label>

        <fieldset className="space-y-2 rounded-lg border border-slate-200 p-3">
          <legend className="px-1 text-sm font-medium text-slate-700">Membros</legend>
          <p className="text-xs text-slate-500">
            Quem vê e responde o que for encaminhado a este ponto (precisa do módulo &quot;Ouvidoria — ponto de resposta&quot; no perfil). O titular é
            a referência para cobranças.
          </p>
          <BuscaUsuario
            excluir={membros.map((m) => m.usuarioId)}
            aoEscolher={(u) => setMembros([...membros, { usuarioId: u.id, nome: u.nome, titular: membros.length === 0 }])}
          />
          {membros.length === 0 ? (
            <p className="text-sm text-slate-400">Nenhum membro. Sem membros, ninguém recebe o encaminhamento.</p>
          ) : (
            <ul className="divide-y divide-slate-100">
              {membros.map((m) => (
                <li key={m.usuarioId} className="flex items-center justify-between gap-2 py-1.5 text-sm">
                  <span className="text-slate-800">{m.nome}</span>
                  <span className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => setMembros(membros.map((x) => ({ ...x, titular: x.usuarioId === m.usuarioId ? !x.titular : x.titular })))}
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs ring-1 ring-inset ${
                        m.titular ? 'bg-amber-50 text-amber-800 ring-amber-600/30' : 'bg-white text-slate-500 ring-slate-300 hover:bg-slate-50'
                      }`}
                      aria-pressed={m.titular}
                      title={m.titular ? 'Titular — clique para tornar membro comum' : 'Clique para tornar titular'}
                    >
                      <Star className="h-3 w-3" aria-hidden="true" /> {m.titular ? 'Titular' : 'Membro'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setMembros(membros.filter((x) => x.usuarioId !== m.usuarioId))}
                      className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600"
                      aria-label={`Remover ${m.nome}`}
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </fieldset>

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

// ---- Busca de usuário (mesma API da tela de Usuários) ----

function useDebounce<T>(valor: T, ms = 300): T {
  const [d, setD] = useState(valor);
  useEffect(() => {
    const t = setTimeout(() => setD(valor), ms);
    return () => clearTimeout(t);
  }, [valor, ms]);
  return d;
}

function BuscaUsuario({ excluir, aoEscolher }: { excluir: string[]; aoEscolher: (u: { id: string; nome: string }) => void }) {
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo.trim(), 300);
  const busca = useListarUsuarios(debounced.length >= 2 ? { busca: debounced, limite: 10 } : undefined);
  const resultados = debounced.length >= 2 ? (busca.data ?? []).filter((u) => u.ativo && !excluir.includes(u.id)) : [];

  return (
    <div className="relative">
      <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" aria-hidden="true" />
      <Input
        value={termo}
        onChange={(e) => setTermo(e.target.value)}
        placeholder="Adicionar membro: nome ou CPF do usuário (mín. 2 caracteres)"
        className="pl-9"
        aria-label="Buscar usuário para adicionar como membro"
      />
      {debounced.length >= 2 ? (
        <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-auto rounded-lg border border-slate-200 bg-white shadow-lg" role="listbox">
          {busca.isFetching && resultados.length === 0 ? <li className="px-3 py-2 text-sm text-slate-500">Buscando…</li> : null}
          {!busca.isFetching && resultados.length === 0 ? <li className="px-3 py-2 text-sm text-slate-500">Nenhum usuário ativo encontrado.</li> : null}
          {resultados.map((u) => (
            <li key={u.id}>
              <button
                type="button"
                role="option"
                aria-selected={false}
                className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-slate-50"
                onClick={() => {
                  aoEscolher({ id: u.id, nome: u.nomeCompleto });
                  setTermo('');
                }}
              >
                <span className="text-slate-800">{u.nomeCompleto}</span>
                <span className="text-xs text-slate-400">{u.email ?? ''}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
