import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Merge, Pencil, RefreshCw, Search, Stethoscope } from 'lucide-react';

import {
  atualizarMedicoLocal,
  atualizarMedicosDasFichas,
  buscarMedicosLocais,
  formatarCpf,
  juntarMedicosLocais,
  possiveisRepetidos,
  type MedicoLocal,
} from '@/features/regulacao/api/medicosLocaisApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';

const CHAVE = ['regulacao', 'medicos-locais', 'Sisreg'];

/**
 * Médicos do SISREG — o cadastro que é SÓ nosso.
 *
 * <p>O SISREG não tem cadastro de médico solicitante: cada ficha traz o nome (e às vezes o CPF)
 * digitado. Este cadastro junta o que as fichas já importadas trazem, sem duplicar, para a Nova
 * Solicitação aproveitar. Nada aqui escreve no SISREG.</p>
 */
export function SisregMedicosPage() {
  const [aba, setAba] = useState<'lista' | 'repetidos'>('lista');
  const [termo, setTermo] = useState('');
  const [editando, setEditando] = useState<MedicoLocal | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const atrasado = useDebounce(termo.trim(), 300);
  const queryClient = useQueryClient();

  const lista = useQuery({
    queryKey: [...CHAVE, 'busca', atrasado],
    queryFn: () => buscarMedicosLocais('Sisreg', atrasado, 100),
    staleTime: 60_000,
  });

  const ler = useMutation({
    mutationFn: () => atualizarMedicosDasFichas('Sisreg'),
    onSuccess: (r) => {
      setAviso(
        `Fichas lidas. ${r.criados.toLocaleString('pt-BR')} médico(s) novo(s), ${r.atualizados.toLocaleString('pt-BR')} completado(s) — ${r.total.toLocaleString('pt-BR')} no cadastro.`,
      );
      void queryClient.invalidateQueries({ queryKey: CHAVE });
    },
    onError: (e) => setAviso(extrairMensagemDeErro(e)),
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <Stethoscope className="size-5 text-slate-500" />
        <h1 className="text-xl font-semibold text-slate-900">Médicos do SISREG</h1>
        <AjudaManual artigo="sisreg-medicos" />
        <Button className="ml-auto" variante="outline" tamanho="sm" onClick={() => ler.mutate()} disabled={ler.isPending}>
          <RefreshCw className={`size-4 ${ler.isPending ? 'animate-spin' : ''}`} />
          {ler.isPending ? 'Lendo as fichas…' : 'Ler as fichas de novo'}
        </Button>
      </div>
      <p className="text-xs text-slate-500">
        Cadastro nosso: o SISREG não tem lista de médicos, só o CPF e o nome digitados em cada ficha. Aqui o mesmo
        médico escrito de vários jeitos vira um só, e a Nova Solicitação o usa para preencher os campos. Nada é
        escrito no SISREG.
      </p>
      {aviso ? <p className="rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700">{aviso}</p> : null}

      <div className="flex gap-1 border-b border-slate-200">
        {(
          [
            ['lista', 'Médicos'],
            ['repetidos', 'Possíveis repetidos'],
          ] as const
        ).map(([id, rotulo]) => (
          <button
            key={id}
            type="button"
            onClick={() => setAba(id)}
            className={`-mb-px border-b-2 px-3 py-1.5 text-sm ${
              aba === id ? 'border-primary-600 font-medium text-primary-700' : 'border-transparent text-slate-600'
            }`}
          >
            {rotulo}
          </button>
        ))}
      </div>

      {aba === 'lista' ? (
        <section className="space-y-3">
          <div className="relative max-w-md">
            <Search className="absolute left-2.5 top-2.5 size-4 text-slate-400" />
            <Input
              className="pl-8"
              placeholder="Nome (por palavras, acha abreviado) ou começo do CPF"
              value={termo}
              onChange={(e) => setTermo(e.target.value)}
            />
          </div>
          {!atrasado ? <p className="text-xs text-slate-500">Os mais usados nas fichas primeiro.</p> : null}
          {lista.isLoading ? <p className="text-sm text-slate-500">Carregando…</p> : null}
          <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
            {(lista.data ?? []).map((m) => (
              <li key={m.id} className="flex items-start justify-between gap-3 px-3 py-2 text-sm">
                <LinhaMedico m={m} />
                <Button variante="ghost" tamanho="sm" onClick={() => setEditando(m)} title="Corrigir">
                  <Pencil className="size-4" />
                </Button>
              </li>
            ))}
            {!lista.isLoading && (lista.data ?? []).length === 0 ? (
              <li className="px-3 py-4 text-center text-sm text-slate-500">
                {atrasado
                  ? `Ninguém com “${atrasado}”. Na Nova Solicitação, “Incluir médico” acrescenta.`
                  : 'Cadastro vazio. Use “Ler as fichas de novo” para montá-lo das fichas importadas.'}
              </li>
            ) : null}
          </ul>
        </section>
      ) : (
        <Repetidos />
      )}

      {editando ? <EditarMedico m={editando} aoFechar={() => setEditando(null)} /> : null}
    </div>
  );
}

function LinhaMedico({ m }: { m: MedicoLocal }) {
  return (
    <span className="min-w-0">
      <span className="text-slate-900">{m.nome}</span>
      <span className="block text-xs text-slate-500">
        {m.cpf ? `CPF ${formatarCpf(m.cpf)}` : 'sem CPF'}
        {m.numeroConselho ? ` · ${m.conselho} ${m.numeroConselho}${m.ufConselho ? `/${m.ufConselho}` : ''}` : ''}
        {' · '}
        {m.ocorrencias > 0 ? `${m.ocorrencias.toLocaleString('pt-BR')} pedidos` : m.origem === 'Executantes' ? 'executante' : 'incluído na plataforma'}
      </span>
      {m.grafias.length > 0 ? (
        <span className="block text-xs text-slate-400">também escrito: {m.grafias.join(' · ')}</span>
      ) : null}
    </span>
  );
}

function Repetidos() {
  const queryClient = useQueryClient();
  const [erro, setErro] = useState<string | null>(null);
  // Juntar apaga um dos cadastros: o primeiro clique só pede confirmação.
  const [confirmando, setConfirmando] = useState<string | null>(null);
  const pares = useQuery({ queryKey: [...CHAVE, 'repetidos'], queryFn: () => possiveisRepetidos('Sisreg') });
  const juntar = useMutation({
    mutationFn: ({ destino, origem }: { destino: string; origem: string }) => juntarMedicosLocais(destino, origem),
    onSuccess: () => {
      setErro(null);
      setConfirmando(null);
      void queryClient.invalidateQueries({ queryKey: CHAVE });
    },
    onError: (e) => setErro(extrairMensagemDeErro(e)),
  });

  return (
    <section className="space-y-3">
      <p className="text-xs text-slate-500">
        Nomes que podem ser o mesmo médico, mas que a leitura das fichas não juntou sozinha (por exemplo, “ANA M
        SOUZA” pode ser a ANA MARIA ou a ANA MARTA). Se for o mesmo, junte no nome certo: as outras grafias e os
        pedidos passam para ele. Cadastros com CPFs diferentes não aparecem aqui — são pessoas diferentes.
      </p>
      {erro ? <p className="text-sm text-red-700">{erro}</p> : null}
      {pares.isLoading ? <p className="text-sm text-slate-500">Procurando…</p> : null}
      <ul className="space-y-2">
        {(pares.data ?? []).map((p) => (
          <li key={`${p.a.id}-${p.b.id}`} className="grid gap-2 rounded-lg border border-slate-200 bg-white p-3 sm:grid-cols-2">
            {[
              [p.a, p.b],
              [p.b, p.a],
            ].map(([fica, sai]) => (
              <div key={fica.id} className="flex items-start justify-between gap-2">
                <LinhaMedico m={fica} />
                {confirmando === `${fica.id}-${sai.id}` ? (
                  <Button
                    variante="danger"
                    tamanho="sm"
                    disabled={juntar.isPending}
                    onClick={() => juntar.mutate({ destino: fica.id, origem: sai.id })}
                  >
                    <Merge className="size-4" /> Confirmar: “{sai.nome}” deixa de existir
                  </Button>
                ) : (
                  <Button
                    variante="secundaria"
                    tamanho="sm"
                    onClick={() => setConfirmando(`${fica.id}-${sai.id}`)}
                    title={`Juntar “${sai.nome}” em “${fica.nome}”`}
                  >
                    <Merge className="size-4" /> Fica este
                  </Button>
                )}
              </div>
            ))}
          </li>
        ))}
        {!pares.isLoading && (pares.data ?? []).length === 0 ? (
          <li className="text-sm text-slate-500">Nenhum possível repetido.</li>
        ) : null}
      </ul>
    </section>
  );
}

function EditarMedico({ m, aoFechar }: { m: MedicoLocal; aoFechar: () => void }) {
  const queryClient = useQueryClient();
  const [nome, setNome] = useState(m.nome);
  const [cpf, setCpf] = useState(m.cpf ?? '');
  const [conselho, setConselho] = useState(m.conselho ?? 'CRM');
  const [numero, setNumero] = useState(m.numeroConselho ?? '');
  const [uf, setUf] = useState(m.ufConselho ?? 'RJ');
  const salvar = useMutation({
    mutationFn: () =>
      atualizarMedicoLocal(m.id, {
        sistema: 'Sisreg',
        nome,
        cpf: cpf.trim() || null,
        conselho: numero.trim() ? conselho : null,
        numeroConselho: numero.trim() || null,
        ufConselho: numero.trim() ? uf : null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CHAVE });
      aoFechar();
    },
  });

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Corrigir médico" descricao="O nome antigo continua achando o médico na busca.">
      <div className="space-y-3">
        <Campo label="Nome completo *" htmlFor="editar-medico-nome">
          <Input id="editar-medico-nome" value={nome} onChange={(e) => setNome(e.target.value.toUpperCase())} maxLength={300} />
        </Campo>
        <Campo label="CPF" htmlFor="editar-medico-cpf">
          <Input id="editar-medico-cpf" value={cpf} inputMode="numeric" onChange={(e) => setCpf(e.target.value)} maxLength={14} />
        </Campo>
        <div className="flex gap-2">
          <Campo label="Conselho" htmlFor="editar-medico-conselho">
            <Input
              id="editar-medico-conselho"
              value={conselho}
              onChange={(e) => setConselho(e.target.value.toUpperCase())}
              maxLength={20}
              className="w-24"
            />
          </Campo>
          <Campo label="Número" htmlFor="editar-medico-numero" className="flex-1">
            <Input id="editar-medico-numero" value={numero} onChange={(e) => setNumero(e.target.value)} maxLength={30} />
          </Campo>
          <Campo label="UF" htmlFor="editar-medico-uf">
            <Input
              id="editar-medico-uf"
              value={uf}
              onChange={(e) => setUf(e.target.value.toUpperCase())}
              maxLength={2}
              className="w-16"
            />
          </Campo>
        </div>
        {salvar.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(salvar.error)}</p> : null}
        <div className="flex justify-end gap-2">
          <Button variante="outline" onClick={aoFechar}>
            Cancelar
          </Button>
          <Button onClick={() => salvar.mutate()} disabled={salvar.isPending}>
            Salvar
          </Button>
        </div>
      </div>
    </Modal>
  );
}
