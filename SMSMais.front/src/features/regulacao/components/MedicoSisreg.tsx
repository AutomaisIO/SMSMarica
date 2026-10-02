import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, UserPlus } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';

import {
  buscarMedicosLocais,
  criarMedicoLocal,
  formatarCpf,
  parecidosMedicoLocal,
  type MedicoLocal,
  type MedicoLocalParecido,
} from '../api/medicosLocaisApi';

/**
 * "Procurar médico já usado no SISREG" — sobre os campos de CPF e nome do profissional
 * solicitante.
 *
 * <p>O SISREG não tem lista de médicos: cada ficha leva o CPF e o nome digitados. Este cadastro é
 * nosso, montado das fichas já importadas (sem duplicar — o mesmo médico escrito de vários jeitos
 * vira um só). Escolher aqui só PREENCHE os dois campos, como texto; nada é escrito no SISREG.</p>
 *
 * <p>"Incluir médico" grava direto no nosso cadastro (não há pendência, porque não há o que
 * cadastrar no SISREG), depois de conferir se ele já existe com outro jeito de escrever.</p>
 */
export function MedicoSisreg({
  aoEscolher,
}: {
  /** Devolve CPF (pode vir vazio, se ainda não o temos) e nome para os campos do formulário. */
  aoEscolher: (cpf: string, nome: string) => void;
}) {
  const [termo, setTermo] = useState('');
  const [aberto, setAberto] = useState(false);
  const atrasado = useDebounce(termo.trim(), 300);
  const busca = useQuery({
    queryKey: ['regulacao', 'medicos-locais', 'Sisreg', atrasado],
    queryFn: () => buscarMedicosLocais('Sisreg', atrasado, 12),
    enabled: atrasado.length >= 3,
    staleTime: 60_000,
  });

  function usar(m: MedicoLocal) {
    aoEscolher(m.cpf ?? '', m.nome);
    setTermo('');
  }

  return (
    <div className="mb-3 rounded-md border border-slate-200 bg-slate-50 p-3">
      <Campo label="Procurar médico já usado no SISREG" htmlFor="medico-sisreg-busca">
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 size-4 text-slate-400" />
          <Input
            id="medico-sisreg-busca"
            className="pl-8"
            placeholder="Nome (por palavras, acha abreviado) ou começo do CPF"
            value={termo}
            onChange={(e) => setTermo(e.target.value)}
            autoComplete="off"
          />
        </div>
      </Campo>
      {atrasado.length >= 3 ? (
        <ul className="mt-1 max-h-64 divide-y divide-slate-100 overflow-auto rounded border border-slate-200 bg-white">
          {busca.isLoading ? <li className="px-3 py-2 text-sm text-slate-500">Procurando…</li> : null}
          {(busca.data ?? []).map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => usar(m)}
                className="flex w-full items-baseline justify-between gap-3 px-3 py-1.5 text-left text-sm hover:bg-primary-50"
              >
                <span className="min-w-0">
                  <span className="text-slate-900">{m.nome}</span>
                  <span className="block text-xs text-slate-500">
                    {m.cpf ? `CPF ${formatarCpf(m.cpf)}` : 'sem CPF — digite no campo'}
                    {m.numeroConselho ? ` · ${m.conselho} ${m.numeroConselho}${m.ufConselho ? `/${m.ufConselho}` : ''}` : ''}
                  </span>
                </span>
                <span className="shrink-0 text-xs text-slate-400">
                  {m.ocorrencias > 0 ? `${m.ocorrencias.toLocaleString('pt-BR')} pedidos` : 'incluído aqui'}
                </span>
              </button>
            </li>
          ))}
          {!busca.isLoading && (busca.data ?? []).length === 0 ? (
            <li className="px-3 py-2 text-sm text-slate-500">Ninguém com “{atrasado}”.</li>
          ) : null}
        </ul>
      ) : null}
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="mt-2 inline-flex items-center gap-1 text-xs font-medium text-primary-700 hover:underline"
      >
        <UserPlus className="size-3.5" /> Não achou? Incluir médico
      </button>
      <IncluirMedicoSisreg
        aberto={aberto}
        aoFechar={() => setAberto(false)}
        aoEscolher={(m) => {
          usar(m);
          setAberto(false);
        }}
      />
    </div>
  );
}

function IncluirMedicoSisreg({
  aberto,
  aoFechar,
  aoEscolher,
}: {
  aberto: boolean;
  aoFechar: () => void;
  aoEscolher: (m: MedicoLocal) => void;
}) {
  const [nome, setNome] = useState('');
  const [cpf, setCpf] = useState('');
  const [conselho, setConselho] = useState('CRM');
  const [numero, setNumero] = useState('');
  const [uf, setUf] = useState('RJ');
  const [parecidos, setParecidos] = useState<MedicoLocalParecido[] | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  function fechar() {
    setNome('');
    setCpf('');
    setConselho('CRM');
    setNumero('');
    setUf('RJ');
    setParecidos(null);
    setErro(null);
    aoFechar();
  }

  async function conferir() {
    setErro(null);
    setOcupado(true);
    try {
      setParecidos(await parecidosMedicoLocal('Sisreg', nome, cpf.trim() || null));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function incluir() {
    setErro(null);
    setOcupado(true);
    try {
      const m = await criarMedicoLocal({
        sistema: 'Sisreg',
        nome,
        cpf: cpf.trim() || null,
        conselho: numero.trim() ? conselho : null,
        numeroConselho: numero.trim() || null,
        ufConselho: numero.trim() ? uf : null,
      });
      setNome('');
      setCpf('');
      setNumero('');
      setParecidos(null);
      aoEscolher(m);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  const nomeValido = nome.trim().split(/\s+/).length >= 2;

  return (
    <Modal
      aberto={aberto}
      aoFechar={fechar}
      titulo="Incluir médico"
      descricao="Fica no nosso cadastro de médicos do SISREG e já preenche a solicitação. Nada é escrito no SISREG."
    >
      <div className="space-y-3">
        <Campo label="Nome completo *" htmlFor="medico-sisreg-nome">
          <Input
            id="medico-sisreg-nome"
            value={nome}
            onChange={(e) => {
              setNome(e.target.value.toUpperCase());
              setParecidos(null);
            }}
            maxLength={300}
          />
        </Campo>
        <Campo label="CPF (se tiver)" htmlFor="medico-sisreg-cpf">
          <Input
            id="medico-sisreg-cpf"
            value={cpf}
            inputMode="numeric"
            onChange={(e) => {
              setCpf(e.target.value);
              setParecidos(null);
            }}
            maxLength={14}
          />
        </Campo>
        <div className="flex gap-2">
          <Campo label="Conselho" htmlFor="medico-sisreg-conselho">
            <Input
              id="medico-sisreg-conselho"
              value={conselho}
              onChange={(e) => setConselho(e.target.value.toUpperCase())}
              maxLength={20}
              className="w-24"
            />
          </Campo>
          <Campo label="Número" htmlFor="medico-sisreg-numero" className="flex-1">
            <Input id="medico-sisreg-numero" value={numero} onChange={(e) => setNumero(e.target.value)} maxLength={30} />
          </Campo>
          <Campo label="UF" htmlFor="medico-sisreg-uf">
            <Input
              id="medico-sisreg-uf"
              value={uf}
              onChange={(e) => setUf(e.target.value.toUpperCase())}
              maxLength={2}
              className="w-16"
            />
          </Campo>
        </div>

        {parecidos === null ? (
          <Button onClick={conferir} disabled={!nomeValido || ocupado}>
            <Search className="size-4" /> Conferir se já existe
          </Button>
        ) : (
          <div className="space-y-2 rounded-md border border-slate-200 p-3">
            {parecidos.length === 0 ? (
              <p className="text-sm text-slate-600">Nenhum médico parecido no cadastro.</p>
            ) : (
              <>
                <p className="text-sm font-medium text-slate-800">Pode ser um destes?</p>
                <ul className="space-y-1">
                  {parecidos.map((p) => (
                    <li key={p.medico.id} className="flex items-center justify-between gap-2 text-sm">
                      <span className="min-w-0">
                        <span className="text-slate-900">{p.medico.nome}</span>
                        <span className="block text-xs text-slate-500">
                          {p.motivo}
                          {p.medico.cpf ? ` · CPF ${formatarCpf(p.medico.cpf)}` : ''}
                          {p.medico.ocorrencias > 0 ? ` · ${p.medico.ocorrencias.toLocaleString('pt-BR')} pedidos` : ''}
                        </span>
                      </span>
                      <Button
                        variante="secundaria"
                        tamanho="sm"
                        // O cadastro não tinha CPF e quem incluía digitou: vai para a solicitação.
                        onClick={() => aoEscolher({ ...p.medico, cpf: p.medico.cpf ?? (cpf.replace(/\D/g, '') || null) })}
                      >
                        É este
                      </Button>
                    </li>
                  ))}
                </ul>
              </>
            )}
            <Button onClick={incluir} disabled={ocupado}>
              <UserPlus className="size-4" />
              {parecidos.length === 0 ? 'Incluir' : 'Nenhum destes — incluir'}
            </Button>
          </div>
        )}

        {erro && <p className="text-sm text-red-700">{erro}</p>}
      </div>
    </Modal>
  );
}
