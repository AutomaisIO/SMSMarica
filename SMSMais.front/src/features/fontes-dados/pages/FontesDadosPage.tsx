import { useState } from 'react';
import { DatabaseZap, Edit2, Eraser, Plus, Server, Trash2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { useFontesDados, useRemoverFonteDados } from '@/features/fontes-dados/api/fontesDadosApi';
import { FonteDadosModal } from '@/features/fontes-dados/components/FonteDadosModal';
import { NovoAgenteModal } from '@/features/fontes-dados/components/NovoAgenteModal';
import { CATEGORIAS, rotuloTipo } from '@/features/fontes-dados/lib/rotulos';
import type { FonteDados } from '@/features/fontes-dados/types';

/**
 * Sistema → Fontes de dados (ADR-0076): o cadastro único de toda fonte de onde a plataforma lê dado.
 * Aqui mora a CONEXÃO; o que cada recurso faz com a fonte (agenda, cadência, conhecimento) fica na
 * tela do recurso, que só escolhe a fonte.
 */
export function FontesDadosPage() {
  const fontes = useFontesDados();
  const remover = useRemoverFonteDados();
  const podeIncluir = usePermissao('FontesDados', 'Inclusao');
  const podeEditar = usePermissao('FontesDados', 'Edicao');
  const podeExcluir = usePermissao('FontesDados', 'Exclusao');

  const [modalAberto, setModalAberto] = useState(false);
  const [emEdicao, setEmEdicao] = useState<FonteDados | undefined>(undefined);
  const [agenteAberto, setAgenteAberto] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  function abrir(fonte?: FonteDados) {
    setEmEdicao(fonte);
    setModalAberto(true);
  }

  function aoRemover(f: FonteDados) {
    const uso = f.usadaPor.length ? `\n\nUsada por: ${f.usadaPor.join('; ')}.` : '';
    const pergunta =
      f.origem === 'base'
        ? `Remover a base "${f.nome}"? Quem a usa para de ler dela.${uso}`
        : `Limpar o login de "${f.nome}"? Quem o usa para até um login novo ser gravado. A agenda dos motores é mantida.${uso}`;
    if (!window.confirm(pergunta)) return;
    setErro(null);
    remover.mutate(f.chave, { onError: (err) => setErro(extrairMensagemDeErro(err)) });
  }

  const lista = fontes.data ?? [];

  return (
    <div className="space-y-8">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
            <DatabaseZap className="h-6 w-6 text-primary-600" />
            Fontes de dados
            <AjudaManual artigo="fontes-de-dados" />
          </h1>
          <p className="mt-1 max-w-3xl text-sm text-gray-600">
            De onde a plataforma lê dado. Aqui se cadastra a conexão — endereço, login, senha, agente. O que
            cada recurso faz com a fonte (agenda, cadência, conhecimento da IA) fica na tela do recurso.
            Senhas nunca são exibidas.
          </p>
        </div>
        <div className="flex gap-2">
          {podeEditar ? (
            <Button variante="outline" onClick={() => setAgenteAberto(true)}>
              <Server className="mr-2 h-4 w-4" />
              Novo agente
            </Button>
          ) : null}
          {podeIncluir ? (
            <Button onClick={() => abrir()}>
              <Plus className="mr-2 h-4 w-4" />
              Nova base
            </Button>
          ) : null}
        </div>
      </header>

      {erro ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div> : null}
      {fontes.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(fontes.error)}
        </div>
      ) : null}
      {fontes.isPending ? <p className="text-sm text-gray-500">Carregando…</p> : null}

      {CATEGORIAS.map((c) => {
        const daCategoria = lista.filter((f) => f.categoria === c.id);
        if (!daCategoria.length) return null;
        return (
          <section key={c.id} className="space-y-3">
            <div>
              <h2 className="text-lg font-semibold text-gray-900">{c.titulo}</h2>
              <p className="text-sm text-gray-500">{c.descricao}</p>
            </div>
            <ul className="divide-y divide-gray-100 rounded-xl border border-gray-200 bg-white shadow-sm">
              {daCategoria.map((f) => (
                <LinhaFonte
                  key={f.chave}
                  fonte={f}
                  aoEditar={podeEditar ? () => abrir(f) : undefined}
                  aoRemover={podeExcluir && (f.origem === 'base' || (f.loginDefinido && f.chave !== 'sisreg-api')) ? () => aoRemover(f) : undefined}
                />
              ))}
            </ul>
          </section>
        );
      })}

      <FonteDadosModal aberto={modalAberto} aoFechar={() => setModalAberto(false)} fonte={emEdicao} podeTestar={podeEditar} />
      <NovoAgenteModal aberto={agenteAberto} aoFechar={() => setAgenteAberto(false)} />
    </div>
  );
}

function LinhaFonte({ fonte: f, aoEditar, aoRemover }: { fonte: FonteDados; aoEditar?: () => void; aoRemover?: () => void }) {
  return (
    <li className="flex flex-wrap items-start justify-between gap-3 px-4 py-3">
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium text-gray-900">{f.nome}</span>
          <Selo cor={f.ativo ? 'verde' : 'cinza'}>{f.ativo ? 'Ativa' : 'Inativa'}</Selo>
          {!f.configurada ? <Selo cor="ambar">Sem login</Selo> : null}
          {f.viaAgente ? (
            <Selo cor={f.agenteConectado ? 'verde' : 'ambar'}>{f.agenteConectado ? 'Agente conectado' : 'Agente desconectado'}</Selo>
          ) : null}
          {f.ambiente && f.ambiente.toUpperCase() !== 'PRODUCAO' ? <Selo cor="cinza">Treinamento</Selo> : null}
        </div>
        <div className="truncate text-xs text-gray-500">
          {rotuloTipo(f.tipo)}
          {f.slug ? (
            <>
              {' · '}
              <code>{f.slug}</code>
            </>
          ) : null}
          {' · '}
          {resumoConexao(f)}
        </div>
        {f.usadaPor.length ? (
          <div className="text-xs text-gray-600">
            <span className="font-medium">Usada por:</span> {f.usadaPor.join(' · ')}
          </div>
        ) : (
          <div className="text-xs text-gray-400">Nenhum recurso usa esta fonte.</div>
        )}
      </div>
      <div className="flex items-center gap-2">
        {aoEditar ? (
          <button
            type="button"
            onClick={aoEditar}
            className="inline-flex items-center gap-1 rounded-md border border-primary-300 bg-primary-50 px-2.5 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100"
          >
            <Edit2 className="h-3.5 w-3.5" />
            Editar
          </button>
        ) : null}
        {aoRemover ? (
          <button
            type="button"
            onClick={aoRemover}
            className="inline-flex items-center gap-1 rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-50"
          >
            {f.origem === 'base' ? <Trash2 className="h-3.5 w-3.5" /> : <Eraser className="h-3.5 w-3.5" />}
            {f.origem === 'base' ? 'Remover' : 'Limpar login'}
          </button>
        ) : null}
      </div>
    </li>
  );
}

function resumoConexao(f: FonteDados): string {
  if (f.viaAgente) return 'credenciais no agente, no servidor da unidade';
  if (f.host) return `${f.host}${f.porta ? `:${f.porta}` : ''}${f.servico ? ` / ${f.servico}` : ''}`;
  if (f.baseUrl) return f.baseUrl;
  if (f.urlPadrao) return `${f.urlPadrao} (padrão)`;
  return 'sem endereço';
}

function Selo({ cor, children }: { cor: 'verde' | 'ambar' | 'cinza'; children: React.ReactNode }) {
  const classes = {
    verde: 'bg-green-100 text-green-700',
    ambar: 'bg-amber-100 text-amber-800',
    cinza: 'bg-gray-100 text-gray-600',
  }[cor];
  return <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${classes}`}>{children}</span>;
}
