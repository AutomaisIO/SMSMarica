import { useEffect, useMemo, useState } from 'react';
import { Save, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useConfiguracaoOperadores, useSalvarOperadoresHabilitados } from '../api/queries';
import { diaBr, numero } from '../lib/formato';
import { Carregando } from './Blocos';

type Filtro = 'todos' | 'marcados' | 'desmarcados';

/**
 * Quem entra nas estatísticas. A lista são TODOS os logins que já autorizaram algo no SISREG (lidos
 * do histórico importado); só os marcados contam. Começa tudo desmarcado de propósito: um terço das
 * autorizações é de login de unidade marcando a própria agenda, e isso não é regulação.
 */
export function ConfiguracaoOperadoresAba({ podeEditar }: { podeEditar: boolean }) {
  const q = useConfiguracaoOperadores(true);
  const salvar = useSalvarOperadoresHabilitados();

  const [marcados, setMarcados] = useState<Set<string>>(new Set());
  const [busca, setBusca] = useState('');
  const [filtro, setFiltro] = useState<Filtro>('todos');
  const [minimo, setMinimo] = useState(0);
  const [aviso, setAviso] = useState<string | null>(null);

  useEffect(() => {
    if (q.data) setMarcados(new Set(q.data.operadores.filter((o) => o.habilitado).map((o) => o.login)));
  }, [q.data]);

  const original = useMemo(
    () => new Set(q.data?.operadores.filter((o) => o.habilitado).map((o) => o.login) ?? []),
    [q.data],
  );
  const alterado = marcados.size !== original.size || [...marcados].some((l) => !original.has(l));

  const visiveis = useMemo(() => {
    const termo = busca.trim().toUpperCase();
    return (q.data?.operadores ?? []).filter(
      (o) =>
        o.autorizacoes >= minimo &&
        (filtro === 'todos' || (filtro === 'marcados') === marcados.has(o.login)) &&
        (!termo || o.login.includes(termo) || (o.usuarioNome ?? '').toUpperCase().includes(termo)),
    );
  }, [q.data, busca, filtro, minimo, marcados]);

  function alternar(login: string) {
    setMarcados((s) => {
      const n = new Set(s);
      if (n.has(login)) n.delete(login);
      else n.add(login);
      return n;
    });
  }

  function marcarVisiveis(valor: boolean) {
    setMarcados((s) => {
      const n = new Set(s);
      for (const o of visiveis) {
        if (valor) n.add(o.login);
        else n.delete(o.login);
      }
      return n;
    });
  }

  async function aoSalvar() {
    setAviso(null);
    try {
      const r = await salvar.mutateAsync([...marcados]);
      setAviso(`Salvo: ${numero(r.habilitados)} login(s) nas estatísticas.`);
    } catch (e) {
      setAviso(extrairMensagemDeErro(e));
    }
  }

  if (q.isPending) return <Carregando />;
  if (q.isError) {
    return (
      <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {extrairMensagemDeErro(q.error)}
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <p className="max-w-4xl text-sm text-gray-600">
        Todos os logins que já aparecem como <strong>Op. autorizador</strong> nos agendamentos importados do
        SISREG. Marque os da <strong>regulação</strong>: só eles entram nas abas Equipe e Individual. Para
        mostrar o nome em vez do login — e somar os logins da mesma pessoa — associe-os ao usuário em{' '}
        <strong>Cadastros → Usuários</strong>, campo "Logins no SISREG".
      </p>

      <div className="flex flex-wrap items-end gap-3">
        <label className="relative min-w-64 flex-1 text-xs text-gray-600">
          Buscar login ou nome
          <Search className="pointer-events-none absolute bottom-2.5 left-2.5 h-4 w-4 text-gray-400" />
          <Input value={busca} onChange={(e) => setBusca(e.target.value)} className="mt-1 pl-8" />
        </label>
        <label className="text-xs text-gray-600">
          Mostrar
          <Select value={filtro} onChange={(e) => setFiltro(e.target.value as Filtro)} className="mt-1">
            <option value="todos">Todos</option>
            <option value="marcados">Só marcados</option>
            <option value="desmarcados">Só desmarcados</option>
          </Select>
        </label>
        <label className="text-xs text-gray-600">
          Com pelo menos
          <Select value={String(minimo)} onChange={(e) => setMinimo(Number(e.target.value))} className="mt-1">
            <option value="0">qualquer volume</option>
            <option value="10">10 autorizações</option>
            <option value="100">100 autorizações</option>
            <option value="1000">1.000 autorizações</option>
          </Select>
        </label>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-gray-700">
          <strong>{numero(marcados.size)}</strong> marcado(s) · {numero(visiveis.length)} de{' '}
          {numero(q.data.operadores.length)} na lista
        </span>
        {podeEditar ? (
          <>
            <Button type="button" variante="ghost" onClick={() => marcarVisiveis(true)}>
              Marcar os da lista
            </Button>
            <Button type="button" variante="ghost" onClick={() => marcarVisiveis(false)}>
              Desmarcar os da lista
            </Button>
            <Button type="button" onClick={aoSalvar} disabled={!alterado || salvar.isPending} className="ml-auto">
              <Save className="h-4 w-4" />
              {salvar.isPending ? 'Salvando…' : 'Salvar'}
            </Button>
          </>
        ) : (
          <span className="ml-auto text-xs text-gray-500">Só leitura — sem permissão de edição na Configuração do SISREG.</span>
        )}
      </div>
      {aviso ? <p className="text-sm text-gray-700">{aviso}</p> : null}

      <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
        <table className="w-full min-w-[44rem] text-left text-sm">
          <thead className="bg-gray-50 text-xs text-gray-500">
            <tr>
              <th className="w-10 px-3 py-2" />
              <th className="px-3 py-2 font-medium">Login no SISREG</th>
              <th className="px-3 py-2 font-medium">Usuário associado</th>
              <th className="px-3 py-2 text-right font-medium">Autorizações</th>
              <th className="px-3 py-2 font-medium">Primeira</th>
              <th className="px-3 py-2 font-medium">Última</th>
            </tr>
          </thead>
          <tbody>
            {visiveis.map((o) => (
              <tr key={o.login} className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-3 py-1.5">
                  <input
                    type="checkbox"
                    checked={marcados.has(o.login)}
                    onChange={() => alternar(o.login)}
                    disabled={!podeEditar}
                    aria-label={`Incluir ${o.login}`}
                  />
                </td>
                <td className="px-3 py-1.5 font-mono text-xs text-gray-900">{o.login}</td>
                <td className="px-3 py-1.5 text-xs text-gray-700">{o.usuarioNome ?? <span className="text-gray-400">—</span>}</td>
                <td className="px-3 py-1.5 text-right text-xs tabular-nums text-gray-700">{numero(o.autorizacoes)}</td>
                <td className="px-3 py-1.5 text-xs text-gray-500">{diaBr(o.primeiraAutorizacao)}</td>
                <td className="px-3 py-1.5 text-xs text-gray-500">{diaBr(o.ultimaAutorizacao)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {visiveis.length === 0 ? <p className="p-6 text-center text-sm text-gray-500">Nenhum login neste filtro.</p> : null}
      </div>
    </div>
  );
}
